using MongoDB.Driver;
using auth_service.Models;

namespace auth_service.Services
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
    }
}
