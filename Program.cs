using System;
using System.Threading.Tasks;
using WebRedisMongo;

namespace WebRedisMongo
{
    class Program
    {
        public static void Main()
        {
            WebServer.HttpServer server = new WebServer.HttpServer(80);
            server.Start();
        }
    }
}
