using MongoDB.Driver;
using MongoDB.Bson;

namespace PresentationLayer
{
    public class MongoSetupHostedService : IHostedService
    {
        private readonly IMongoClient mongoClient;
        private readonly IConfiguration configuration;

        public MongoSetupHostedService(IMongoClient mongoClient, IConfiguration configuration)
        {
            this.mongoClient = mongoClient;
            this.configuration = configuration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
            var collectionName = configuration["ChatMongo:Collection"] ?? "messages";

            var db = mongoClient.GetDatabase(dbName);

            // Create collection if it doesn't exist
            var existing = await db.ListCollectionNames().ToListAsync(cancellationToken);
            if (!existing.Contains(collectionName))
            {
                await db.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken);
            }

            var collection = db.GetCollection<BsonDocument>(collectionName);

            // Ensure indexes: conversationId+sentAt, and text on text
            var indexKeys = Builders<BsonDocument>.IndexKeys
                .Ascending("conversationId")
                .Ascending("sentAt");
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(indexKeys), cancellationToken: cancellationToken);

            var textIndex = Builders<BsonDocument>.IndexKeys.Text("text");
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(textIndex), cancellationToken: cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

