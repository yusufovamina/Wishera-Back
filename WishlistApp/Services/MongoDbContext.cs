// Services/MongoDbContext.cs
using MongoDB.Driver;
using WishlistApp.Models;

namespace WishlistApp.Services
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var client = new MongoClient(configuration.GetConnectionString("MongoDB"));
            _database = client.GetDatabase("WishlistApp");
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Gift> Gifts => _database.GetCollection<Gift>("Gifts");
    }
}