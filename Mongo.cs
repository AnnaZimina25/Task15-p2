using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;


namespace WebRedisMongo
{
    class Mongo
    {
        const string MongoDBConnectionString = "mongodb://localhost:27017";
        const string databaseName = "formData";
        const string collectionName = "personData";

        public class Person
        {
            public ObjectId Id { get; set; }
            public string FirstName { get; set; }

            public string LastName { get; set; }
        }

        public static async Task<List<string>> TableDataOutput()
        {
            var listResult = new List<string>();
            var client = new MongoClient(MongoDBConnectionString);
            var database = client.GetDatabase(databaseName);
            var personData = database.GetCollection<Person>(collectionName);
            
            var mongoData = await personData.FindAsync<Person>(Builders<Person>.Filter.Empty);
            List<Person> resultData = await mongoData.ToListAsync();
            
            foreach (var data in resultData)
            {
                listResult.Add(data.FirstName);
                listResult.Add(data.LastName);
            }
            
                return listResult;
        }
    }
}
