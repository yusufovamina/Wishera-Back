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
            try
            {
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";

                var db = mongoClient.GetDatabase(dbName);

                // Create collection if it doesn't exist
                try
                {
                    var existing = await db.ListCollectionNames().ToListAsync(cancellationToken);
                    if (!existing.Contains(collectionName))
                    {
                        await db.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not check/create collection: {ex.Message}");
                }

                var collection = db.GetCollection<BsonDocument>(collectionName);

                // Ensure indexes: conversationId+sentAt, and text on text
                try
                {
                    var indexKeys = Builders<BsonDocument>.IndexKeys
                        .Ascending("conversationId")
                        .Ascending("sentAt");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(indexKeys), 
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not create conversationId index (may already exist): {ex.Message}");
                }

                try
                {
                    var textIndex = Builders<BsonDocument>.IndexKeys.Text("text");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(textIndex), 
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not create text index (may already exist): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MongoSetupHostedService: {ex.Message}");
                // Don't throw - allow service to start even if MongoDB setup fails
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

