using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace WebRedisMongo
{
    class WebServer
    {
        public class HttpServer // класс сервера
        {
            public const string version = "HTTP/1.0";
            public const string serverName = "myServer/1.1";
            readonly TcpListener listener;
            bool running = false;

            public HttpServer(int port) // конструктор класса
            {
                listener = new TcpListener(IPAddress.Any, port);
            }

            public void Start() // запускает дополнительный поток
            {
                Thread thread = new Thread(new ThreadStart(Run));
                thread.Start();
            }

            private void Run()
            {
                listener.Start(); // запуск ожидания запросов
                running = true;

                Console.WriteLine("Server is running.");

                while (running) // бесконечная работа сервера
                {
                    Console.WriteLine("Waiting for connection...");
                    TcpClient client = listener.AcceptTcpClient(); // ловим клиента
                    Console.WriteLine("Client connected.");

                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(client);
                }
            }

            private async void HandleClient(object oClient)
            {
                var client = oClient as TcpClient;

                StreamReader reader = new StreamReader(client.GetStream()); // поток для считывания

                string message = "";

                while (reader.Peek() != -1) // проверка есть ли что считывать в сообщении
                {
                    message += (char)reader.Read(); // считываем сообщение посимвольно
                }

                Console.WriteLine("REQUEST: \r\n" + message); // вывели полученное от клиента собщение

                Request request = Request.GetRequest(message); // получили запрос

                if (request != null)
                {
                    Response response = await Response.From(request); // передали запрос 
                    response.Post(client.GetStream());
                }
                else
                {
                    Console.WriteLine("Request is null.");
                }

                client.Close();
            }


        }

        public class Request // класс запроса
        {
            public string URL { get; set; }
            public string Type { get; set; }
            public string Host { get; set; }
            public string Content { get; set; }

            public string Form { get; set; }

            private Request(String type, string url, string host, string content = null, string form = null)
            {
                this.Type = type;
                this.URL = url;
                this.Host = host;
                this.Content = content;
                this.Form = form;
            }

            public static string ContentFromGETRequest(string url)
            {
                string content = null;
                string[] urlMas = url.Split('?');

                if (urlMas.Length == 2)
                {
                    content = urlMas[1];
                }
                else
                {
                    Console.WriteLine("Empty Request");
                }


                return content;
            }

            public static string ContentFromPOSTRequest(string[] messageLines)
            {
                int contentSize = 0;
                bool isMultipart = IsMultipart(messageLines);
                string content = null;
              

                for (int i = 2; i < messageLines.Length; i++)
                {
                    if (messageLines[i].StartsWith("Content-Length:"))
                    {
                        string[] strContentLength = messageLines[i].Split(" ");

                        if (Int32.TryParse(strContentLength[1], out int size))
                        {
                            contentSize = size;
                        }
                        else
                        {
                            Console.WriteLine("Attempted conversion of '{0}' failed.", strContentLength[1]);
                        }

                        break;
                    }
                }

                if (contentSize > 0) // проверяем есть ли контетнт
                {
                    if (isMultipart)
                    {
                        string boundary = GetBoundary(messageLines);

                        ArrayList contentList = new ArrayList();

                        if (boundary.Length > 0)
                        {
                            int contentStartLine = -1;

                            for (int i = 2; i < messageLines.Length; i++)
                            {
                                if (messageLines[i].Equals(boundary))
                                {
                                    contentStartLine = i;
                                    break;
                                }
                            }

                            int contentEndLine = -1;

                            if (contentStartLine > 0)
                            {
                                for (int i = contentStartLine; i < messageLines.Length; i++)
                                {
                                    if (messageLines[i].Equals(boundary + "--"))
                                    {
                                        contentEndLine = i;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("400 Bad Request. (contentStartLine =< 0)");
                                return null;
                            }

                            if (contentEndLine > 0)
                            {
                                for (int i = contentStartLine; i < contentEndLine - 1; i++)
                                {
                                    if (messageLines[i].Length == 0)
                                    {
                                        i += 1;

                                        while (!(messageLines[i].StartsWith(boundary)))
                                        {
                                            contentList.Add(messageLines[i]);
                                            i++;
                                        }

                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("400 Bad Request.ContentEndLine =< 0");
                                return null;
                            }


                            StringBuilder sb = new StringBuilder();

                            for (int i = 0; i < contentList.Count; i++)
                            {
                                sb.Append(contentList[i] + "\r\n");
                            }

                            content = sb.ToString();
                        }
                        else
                        {
                            Console.WriteLine("400 Bad Request. Boundary is empty");
                            return null;
                        }
                    }
                    else
                    {
                        for (int i = 2; i < messageLines.Length; i++)
                        {
                            if (messageLines[i].Length == 0)
                            {
                                var contentIndex = i + 1;

                                StringBuilder sb = new StringBuilder();

                                for (int j = contentIndex; j < messageLines.Length; j++)
                                {
                                    sb.AppendLine(messageLines[j]);
                                }

                                content = sb.ToString();
                                break;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("400 Bad Request. ContentSize < 0");
                    return null;
                }
                Console.WriteLine($"DataFromPOSTRequest: {content}");
                return content;
            }

            public static bool IsMultipart(string[] messageLines) // проверка типа пост запроса
            {
                return messageLines.Any(s => s.StartsWith("Content-Type: multipart/form-data; boundary="));
            }

            public static string GetBoundary(string[] messageLines) // метод поиска делителя для multipart post запроса
            {

                string boundary = string.Empty;

                for (int i = 2; i < messageLines.Length; i++)
                {
                    if (messageLines[i].StartsWith("Content-Type: multipart/form-data; boundary="))
                    {
                        string[] boundaryStr = messageLines[i].Split("=");
                        boundary = "--" + boundaryStr[1];
                        break;
                    }
                }

                return boundary;
            }

            public static Request GetRequest(string message)
            {
                if (String.IsNullOrEmpty(message)) // проверили строку
                {
                    return null;
                }

                Console.WriteLine(message);
                string[] messageLines = message.Split("\r\n"); // разделили всё сообщение от браузера на массив строк
                string[] typeUrlVersion = messageLines[0].Split(' ');

                if (typeUrlVersion.Length == 3)
                {
                    string type = typeUrlVersion[0];
                    string url = typeUrlVersion[1];
                    string version = typeUrlVersion[2];
                    string form = null;

                    if (IsMultipart(messageLines))
                    {
                        form = "isMultipart";
                    }

                    if (version.StartsWith("HTTP/"))
                    {
                        string[] hostStr = messageLines[1].Split(' ');
                        string host = hostStr[1];

                        string content = "";

                        switch (type)
                        {
                            case "GET":
                                content = ContentFromGETRequest(url);
                                break;

                            case "POST":
                                content = ContentFromPOSTRequest(messageLines);
                                break;

                            default:
                                Console.WriteLine("400 Bad Request.");
                                break;
                        }

                        Console.WriteLine("TYPE: {0}, URL: {1}, HOST: {2}", type, url, host);
                        return new Request(type, url.Split("?")[0], host, content, form);

                    }
                    else
                    {
                        Console.WriteLine("400 Bad Request.");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("400 Bad Request.");
                    return null;
                }

            }

        }

        public class Response // класс ответа
        {
            readonly byte[] data;
            readonly string status;
            readonly string mime;

            private Response(byte[] data, string status, string mime)
            {
                this.data = data;
                this.status = status;
                this.mime = mime;
            }

            public static async Task<Response> From(Request request) //инициализирует экземпляр класса
            {
                string path = request.URL.TrimEnd('/'); //ссылка из запроса без слеша
                byte[] dataAnswer;
                string status = "200 OK";
                string mime = "text/html";
                
                string html1 = @"C:\Users\a_zimina\Documents\Work\Tasks\Task14\WebToRedis\Form.html"; // html файл1
                string html2 = @"C:\Users\a_zimina\Documents\Work\Tasks\Task14\WebToRedis\HTMLPage1.html"; // html файл2

                StringBuilder sb = new StringBuilder(); // строитель строк для сбора и передечи данных на вывод
                sb.Append(File.ReadAllText(html1));

                List<string> contentList = GetStringData(request);

                if (contentList.Count > 0)
                {
                    await Redis.RedisDataInput(contentList);
                }
                Thread.Sleep(100);

                List<string> contentListFromBD = await Mongo.TableDataOutput();


                if (contentListFromBD.Count > 0)
                {
                    sb.Append(File.ReadAllText(html2));

                    int i = 0; int j = 1;
                    do
                    {
                        sb.Append("<tr><td>" + contentListFromBD[i] + "</td><td>" + contentListFromBD[j] + "</td></tr>");
                        i += 2;
                        j += 2;
                    } while (j <= contentListFromBD.Count);

                    sb.Append("</table></body></html>");
                }


                string answer = sb.ToString();

                dataAnswer = Encoding.UTF8.GetBytes(answer);


                return new Response(dataAnswer, status, mime);
            }

            public static List<string> GetStringData(Request request)
            {
                List<string> contentList = new List<string>();

                if (request.Content != null)
                {

                    if (request.Form.Equals("isMultipart"))
                    {
                        contentList.AddRange(request.Content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
                    }
                    else
                    {
                        string[] contentLines = request.Content.Split('&');

                        foreach (string content in contentLines)
                        {
                            string[] parameters = content.Split('=');

                            if (parameters.Length == 2)
                            {
                                contentList.Add(parameters[1]);
                            }

                        }
                    }
                }

                return contentList;
            }

            public void Post(NetworkStream stream)
            {
                using (StreamWriter writer = new StreamWriter(stream, leaveOpen: true))
                {
                    writer.Write($"{HttpServer.version} {status}\r\nDate: {DateTime.UtcNow:r}\r\nServer: {HttpServer.serverName}\r\nContent-Length: {data.Length}\r\nContent-Type: {mime}; charset=utf-8\r\nConnection: Closed\r\n\r\n");
                }

                stream.Write(data, 0, data.Length);

                stream.Flush();
            }
        }
    }
}
