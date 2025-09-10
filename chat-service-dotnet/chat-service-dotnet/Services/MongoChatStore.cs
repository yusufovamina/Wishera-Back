using ChatService.Api.Hubs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ChatService.Api.Services
{
    public class MongoMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("messageId")]
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        public string ConversationId { get; set; } = string.Empty;
        public string SenderUserId { get; set; } = string.Empty;
        public string RecipientUserId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DeliveredAt { get; set; }
        public DateTimeOffset? ReadAt { get; set; }
        public string? ClientMessageId { get; set; }
    }

    public class MongoChatStore : IChatStore
    {
        private readonly IMongoCollection<MongoMessage> _collection;

        public MongoChatStore(IConfiguration configuration)
        {
            var connectionString = configuration["ChatMongo:ConnectionString"]
                ?? configuration.GetConnectionString("MongoDB")
                ?? throw new InvalidOperationException("ChatMongo:ConnectionString or ConnectionStrings:MongoDB is not configured");
            var databaseName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
            var collectionName = configuration["ChatMongo:Collection"] ?? "messages";

            var client = new MongoClient(connectionString);
            var db = client.GetDatabase(databaseName);
            _collection = db.GetCollection<MongoMessage>(collectionName);

            // Ensure indexes
            var indexKeys = Builders<MongoMessage>.IndexKeys
                .Ascending(x => x.ConversationId)
                .Ascending(x => x.SentAt);
            _collection.Indexes.CreateOne(new CreateIndexModel<MongoMessage>(indexKeys));

            var textIndex = Builders<MongoMessage>.IndexKeys.Text(x => x.Text);
            _collection.Indexes.CreateOne(new CreateIndexModel<MongoMessage>(textIndex));
        }

        public async Task AppendAsync(ChatMessage message)
        {
            var doc = new MongoMessage
            {
                MessageId = message.Id,
                ConversationId = message.ConversationId,
                SenderUserId = message.SenderUserId,
                RecipientUserId = message.RecipientUserId,
                Text = message.Text,
                SentAt = message.SentAt,
                DeliveredAt = message.DeliveredAt,
                ReadAt = message.ReadAt,
                ClientMessageId = message.ClientMessageId
            };
            await _collection.InsertOneAsync(doc);
        }

        public async Task<IEnumerable<ChatMessage>> GetHistoryAsync(string conversationId, int page, int pageSize)
        {
            var cursor = await _collection.Find(x => x.ConversationId == conversationId)
                .SortBy(x => x.SentAt)
                .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
                .Limit(Math.Max(1, pageSize))
                .ToListAsync();
            return cursor.Select(MapToDomain);
        }

        public async Task<bool> EditMessageAsync(string conversationId, string messageId, string newText)
        {
            var update = Builders<MongoMessage>.Update.Set(x => x.Text, newText);
            var result = await _collection.UpdateOneAsync(x => x.ConversationId == conversationId && x.MessageId == messageId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteMessageAsync(string conversationId, string messageId)
        {
            var result = await _collection.DeleteOneAsync(x => x.ConversationId == conversationId && x.MessageId == messageId);
            return result.DeletedCount > 0;
        }

        public async Task<int> MarkReadAsync(string conversationId, string userId, IEnumerable<string> messageIds)
        {
            var filter = Builders<MongoMessage>.Filter.And(
                Builders<MongoMessage>.Filter.Eq(x => x.ConversationId, conversationId),
                Builders<MongoMessage>.Filter.In(x => x.MessageId, messageIds.ToArray()),
                Builders<MongoMessage>.Filter.Eq(x => x.RecipientUserId, userId),
                Builders<MongoMessage>.Filter.Eq(x => x.ReadAt, null)
            );
            var update = Builders<MongoMessage>.Update.Set(x => x.ReadAt, DateTimeOffset.UtcNow);
            var result = await _collection.UpdateManyAsync(filter, update);
            return (int)result.ModifiedCount;
        }

        public async Task<IEnumerable<ChatMessage>> SearchAsync(string conversationId, string? queryText, DateTimeOffset? from, DateTimeOffset? to, int page, int pageSize)
        {
            var builder = Builders<MongoMessage>.Filter;
            var filter = builder.Eq(x => x.ConversationId, conversationId);
            if (from.HasValue) filter &= builder.Gte(x => x.SentAt, from.Value);
            if (to.HasValue) filter &= builder.Lte(x => x.SentAt, to.Value);
            if (!string.IsNullOrWhiteSpace(queryText))
            {
                // Use $text if index present; fallback to regex
                filter &= builder.Text(queryText);
            }

            var cursor = await _collection.Find(filter)
                .SortBy(x => x.SentAt)
                .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
                .Limit(Math.Max(1, pageSize))
                .ToListAsync();
            return cursor.Select(MapToDomain);
        }

        private static ChatMessage MapToDomain(MongoMessage m) => new ChatMessage
        {
            Id = m.MessageId,
            ConversationId = m.ConversationId,
            SenderUserId = m.SenderUserId,
            RecipientUserId = m.RecipientUserId,
            Text = m.Text,
            SentAt = m.SentAt,
            DeliveredAt = m.DeliveredAt,
            ReadAt = m.ReadAt,
            ClientMessageId = m.ClientMessageId
        };
    }
}


