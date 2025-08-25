using MongoDB.Driver;
using WishlistApp.Models;

namespace user_service.Services
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Relationship> Relationships => _database.GetCollection<Relationship>("Relationships");
        // Other collections are not needed in user-service directly
        // public IMongoCollection<Wishlist> Wishlists => _database.GetCollection<Wishlist>("Wishlists");
        // public IMongoCollection<Like> Likes => _database.GetCollection<Like>("Likes");
        // public IMongoCollection<Comment> Comments => _database.GetCollection<Comment>("Comments");
        // public IMongoCollection<FeedEvent> Feed => _database.GetCollection<FeedEvent>("Feed");
        // public IMongoCollection<Gift> Gifts => _database.GetCollection<Gift>("Gifts");
    }
}
