using Newtonsoft.Json;
using ServiceStack.Redis;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WebRedisMongo
{
    class Redis
    {
        public static string rKey = "dataList";

        public class PersonRedis
        {
            public string FirstName { get; set; }

            public string LastName { get; set; }
        }

        public static async Task RedisDataInput(List<string> contentList)
        {

            string serialized = JsonConvert.SerializeObject(new PersonRedis() { FirstName = contentList[0].ToString(), LastName = contentList[1].ToString() });

            Console.WriteLine($"{serialized}");

            using var redis = ConnectionMultiplexer.Connect("localhost");
            IDatabase db = redis.GetDatabase();

            await db.ListRightPushAsync(rKey, serialized);
        }
    }
}
