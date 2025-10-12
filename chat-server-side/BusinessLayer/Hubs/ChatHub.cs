using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.Collections.Concurrent;

namespace BusinessLayer.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private static readonly ConcurrentDictionary<string, string> activeUsers = new();

        public ChatHub(
            IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor;
        }

        public async Task SendMessageToAll(string userId, string message)
        {
            await Clients.Others.SendAsync("ReceiveMessage", userId, message);
        }

        public async Task SendMessageToUser(string userId, string message)
        {
            var sourceUserId = GetUserIdFromQuery();
            if (activeUsers.ContainsKey(userId))
            {
                var username = GetUsernameFromQuery();
                var messageId = Guid.NewGuid().ToString();
                var sentAt = DateTimeOffset.UtcNow;
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, sentAt }, username);
            }
            // Persist to Mongo if configured
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient != null && configuration != null)
                {
                    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                    var db = mongoClient.GetDatabase(dbName);
                    var collection = db.GetCollection<dynamic>(collectionName);
                    var conversationId = string.CompareOrdinal(sourceUserId, userId) < 0
                        ? $"{sourceUserId}:{userId}"
                        : $"{userId}:{sourceUserId}";
                    await collection.InsertOneAsync(new
                    {
                        messageId = Guid.NewGuid().ToString(),
                        conversationId,
                        senderUserId = sourceUserId,
                        recipientUserId = userId,
                        text = message,
                        sentAt = DateTimeOffset.UtcNow
                    });
                }
            }
            catch { }
        }

        public async Task SendMessageToUserWithMeta(string userId, string message, string? replyToMessageId = null, string? clientMessageId = null)
        {
            var sourceUserId = GetUserIdFromQuery();
            var messageId = Guid.NewGuid().ToString();
            var sentAt = DateTimeOffset.UtcNow;
            if (activeUsers.ContainsKey(userId))
            {
                var username = GetUsernameFromQuery();
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, replyToMessageId, clientMessageId, sentAt }, username);
            }
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient != null && configuration != null)
                {
                    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                    var db = mongoClient.GetDatabase(dbName);
                    var collection = db.GetCollection<dynamic>(collectionName);
                    var conversationId = string.CompareOrdinal(sourceUserId, userId) < 0
                        ? $"{sourceUserId}:{userId}"
                        : $"{userId}:{sourceUserId}";
                    await collection.InsertOneAsync(new
                    {
                        messageId = messageId,
                        conversationId,
                        senderUserId = sourceUserId,
                        recipientUserId = userId,
                        text = message,
                        replyToMessageId = replyToMessageId,
                        clientMessageId = clientMessageId,
                        sentAt = sentAt
                    });
                }
            }
            catch { }
        }

        public async Task<bool> EditMessage(string messageId, string newText)
        {
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient == null || configuration == null) return false;
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("messageId", messageId);
                var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("text", newText);
                var result = await collection.UpdateOneAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    // Notify participants if online (best effort)
                    var doc = await collection.Find(filter).FirstOrDefaultAsync();
                    string? senderId = null;
                    string? recipientId = null;
                    if (doc != null)
                    {
                        var sVal = doc.GetValue("senderUserId", MongoDB.Bson.BsonNull.Value);
                        if (!sVal.IsBsonNull) senderId = sVal.AsString;
                        var rVal = doc.GetValue("recipientUserId", MongoDB.Bson.BsonNull.Value);
                        if (!rVal.IsBsonNull) recipientId = rVal.AsString;
                    }
                    if (!string.IsNullOrEmpty(recipientId) && activeUsers.ContainsKey(recipientId))
                    {
                        await Clients.Client(activeUsers[recipientId]).SendAsync("MessageEdited", new { id = messageId, text = newText });
                    }
                    if (!string.IsNullOrEmpty(senderId) && activeUsers.ContainsKey(senderId))
                    {
                        await Clients.Client(activeUsers[senderId]).SendAsync("MessageEdited", new { id = messageId, text = newText });
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> DeleteMessage(string messageId)
        {
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient == null || configuration == null) return false;
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("messageId", messageId);
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                var result = await collection.DeleteOneAsync(filter);
                if (result.DeletedCount > 0)
                {
                    string? senderId = null;
                    string? recipientId = null;
                    if (doc != null)
                    {
                        var sVal = doc.GetValue("senderUserId", MongoDB.Bson.BsonNull.Value);
                        if (!sVal.IsBsonNull) senderId = sVal.AsString;
                        var rVal = doc.GetValue("recipientUserId", MongoDB.Bson.BsonNull.Value);
                        if (!rVal.IsBsonNull) recipientId = rVal.AsString;
                    }
                    if (!string.IsNullOrEmpty(recipientId) && activeUsers.ContainsKey(recipientId))
                    {
                        await Clients.Client(activeUsers[recipientId]).SendAsync("MessageDeleted", new { id = messageId });
                    }
                    if (!string.IsNullOrEmpty(senderId) && activeUsers.ContainsKey(senderId))
                    {
                        await Clients.Client(activeUsers[senderId]).SendAsync("MessageDeleted", new { id = messageId });
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        // Typing indicators
        public async Task StartTyping(string targetUserId)
        {
            var sourceUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(sourceUserId)) return;
            if (activeUsers.ContainsKey(targetUserId))
            {
                await Clients.Client(activeUsers[targetUserId]).SendAsync("Typing", new { userId = sourceUserId, isTyping = true });
            }
        }

        public async Task StopTyping(string targetUserId)
        {
            var sourceUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(targetUserId) || string.IsNullOrEmpty(sourceUserId)) return;
            if (activeUsers.ContainsKey(targetUserId))
            {
                await Clients.Client(activeUsers[targetUserId]).SendAsync("Typing", new { userId = sourceUserId, isTyping = false });
            }
        }

        // Reactions
        public async Task<bool> ReactToMessage(string messageId, string emoji)
        {
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient == null || configuration == null) return false;
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var userId = GetUserIdFromQuery();
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("messageId", messageId);
                var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set($"reactions.{emoji}.{userId}", true);
                var result = await collection.UpdateOneAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    var doc = await collection.Find(filter).FirstOrDefaultAsync();
                    if (doc != null)
                    {
                        string? senderId = null;
                        string? recipientId = null;
                        var sVal = doc.GetValue("senderUserId", MongoDB.Bson.BsonNull.Value);
                        if (!sVal.IsBsonNull) senderId = sVal.AsString;
                        var rVal = doc.GetValue("recipientUserId", MongoDB.Bson.BsonNull.Value);
                        if (!rVal.IsBsonNull) recipientId = rVal.AsString;
                        var payload = new { id = messageId, userId, emoji };
                        if (!string.IsNullOrEmpty(recipientId) && activeUsers.ContainsKey(recipientId))
                        {
                            await Clients.Client(activeUsers[recipientId]).SendAsync("MessageReactionUpdated", payload);
                        }
                        if (!string.IsNullOrEmpty(senderId) && activeUsers.ContainsKey(senderId))
                        {
                            await Clients.Client(activeUsers[senderId]).SendAsync("MessageReactionUpdated", payload);
                        }
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public async Task<bool> UnreactToMessage(string messageId, string emoji)
        {
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient == null || configuration == null) return false;
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var userId = GetUserIdFromQuery();
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("messageId", messageId);
                var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Unset($"reactions.{emoji}.{userId}");
                var result = await collection.UpdateOneAsync(filter, update);
                if (result.ModifiedCount > 0)
                {
                    var doc = await collection.Find(filter).FirstOrDefaultAsync();
                    if (doc != null)
                    {
                        string? senderId = null;
                        string? recipientId = null;
                        var sVal = doc.GetValue("senderUserId", MongoDB.Bson.BsonNull.Value);
                        if (!sVal.IsBsonNull) senderId = sVal.AsString;
                        var rVal = doc.GetValue("recipientUserId", MongoDB.Bson.BsonNull.Value);
                        if (!rVal.IsBsonNull) recipientId = rVal.AsString;
                        var payload = new { id = messageId, userId, emoji, removed = true };
                        if (!string.IsNullOrEmpty(recipientId) && activeUsers.ContainsKey(recipientId))
                        {
                            await Clients.Client(activeUsers[recipientId]).SendAsync("MessageReactionUpdated", payload);
                        }
                        if (!string.IsNullOrEmpty(senderId) && activeUsers.ContainsKey(senderId))
                        {
                            await Clients.Client(activeUsers[senderId]).SendAsync("MessageReactionUpdated", payload);
                        }
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public async Task<int> MarkMessagesRead(string peerUserId, IEnumerable<string> messageIds)
        {
            try
            {
                var httpContext = httpContextAccessor.HttpContext;
                var services = httpContext?.RequestServices;
                var mongoClient = services?.GetService(typeof(MongoDB.Driver.IMongoClient)) as MongoDB.Driver.IMongoClient;
                var configuration = services?.GetService(typeof(IConfiguration)) as IConfiguration;
                if (mongoClient == null || configuration == null) return 0;
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var userId = GetUserIdFromQuery();
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.And(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("recipientUserId", userId),
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("senderUserId", peerUserId),
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.In("messageId", messageIds.ToArray())
                );
                var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update.Set("readAt", DateTimeOffset.UtcNow);
                var result = await collection.UpdateManyAsync(filter, update);
                var count = (int)result.ModifiedCount;
                if (count > 0)
                {
                    if (activeUsers.ContainsKey(peerUserId))
                    {
                        await Clients.Client(activeUsers[peerUserId]).SendAsync("MessagesRead", new { byUserId = userId, messageIds = messageIds.ToArray() });
                    }
                    if (activeUsers.ContainsKey(userId))
                    {
                        await Clients.Client(activeUsers[userId]).SendAsync("MessagesRead", new { byUserId = userId, messageIds = messageIds.ToArray() });
                    }
                }
                return count;
            }
            catch { }
            return 0;
        }

        public async Task AddUser(string userId, string connectionId)
        {
            // Upsert connection id (thread-safe)
            activeUsers.AddOrUpdate(userId, connectionId, (_, __) => connectionId);
            await Clients.All.SendAsync("ReceiveActiveUsers", GetActiveUserIds());
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        public List<string> GetActiveUserIds()
        {
            // Enumerate a snapshot to avoid concurrent modification issues
            return activeUsers.Keys.ToArray().ToList();
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.All.SendAsync("ReceiveActiveUsers", GetActiveUserIds());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = GetConnectionId();
            foreach (var kv in activeUsers.ToArray())
            {
                if (kv.Value == connectionId)
                {
                    activeUsers.TryRemove(kv.Key, out _);
                }
            }
            await Clients.All.SendAsync("ReceiveActiveUsers", GetActiveUserIds());
            await base.OnDisconnectedAsync(exception);
        }

        private string GetUserIdFromQuery()
        {
            var httpContext = httpContextAccessor.HttpContext;
            var userIdString = httpContext?.Request?.Query["userId"].ToString();
            return string.IsNullOrWhiteSpace(userIdString) ? string.Empty : userIdString;
        }

        private string? GetUsernameFromQuery()
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.Request?.Query["username"].ToString();
        }
    }
}
