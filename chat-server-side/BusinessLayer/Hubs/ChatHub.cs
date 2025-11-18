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
        private readonly IMongoClient mongoClient;
        private readonly IConfiguration configuration;
        private static readonly ConcurrentDictionary<string, string> activeUsers = new();
        private static readonly ConcurrentDictionary<string, (string callerUserId, string calleeUserId)> activeCalls = new();

        public ChatHub(
            IHttpContextAccessor httpContextAccessor,
            IMongoClient mongoClient,
            IConfiguration configuration)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.mongoClient = mongoClient;
            this.configuration = configuration;
        }

        public async Task SendMessageToAll(string userId, string message)
        {
            await Clients.Others.SendAsync("ReceiveMessage", userId, message);
        }

        public async Task SendMessageToUser(string userId, string message)
        {
            var sourceUserId = GetUserIdFromQuery();
            // Handle special pin/unpin hint messages: broadcast to both participants and DO NOT persist
            var isPinHint = !string.IsNullOrEmpty(message) && (message.StartsWith("[[pin]]:") || message.StartsWith("[[unpin]]:"));
            if (isPinHint)
            {
                var username = GetUsernameFromQuery();
                var messageId = Guid.NewGuid().ToString();
                var sentAt = DateTimeOffset.UtcNow.ToString("O"); // ISO 8601 format with timezone
                if (activeUsers.ContainsKey(userId))
                {
                    await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, sentAt }, username);
                }
                if (!string.IsNullOrEmpty(sourceUserId) && activeUsers.ContainsKey(sourceUserId))
                {
                    await Clients.Client(activeUsers[sourceUserId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, sentAt }, username);
                }
                return; // Skip persistence for pin/unpin hints
            }
            if (activeUsers.ContainsKey(userId))
            {
                var username = GetUsernameFromQuery();
                var messageId = Guid.NewGuid().ToString();
                var sentAt = DateTimeOffset.UtcNow.ToString("O"); // ISO 8601 format with timezone
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, sentAt }, username);
            }
            // Persist to Mongo if configured
            try
            {
                if (mongoClient != null && configuration != null)
                {
                    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                    var db = mongoClient.GetDatabase(dbName);
                    var collection = db.GetCollection<dynamic>(collectionName);
                    var conversationId = string.CompareOrdinal(sourceUserId, userId) < 0
                        ? $"{sourceUserId}:{userId}"
                        : $"{userId}:{sourceUserId}";
                    // Do not persist pin/unpin hint messages
                    if (!isPinHint)
                    {
                        var messageId = Guid.NewGuid().ToString();
                        await collection.InsertOneAsync(new
                        {
                            messageId = messageId,
                            conversationId,
                            senderUserId = sourceUserId,
                            recipientUserId = userId,
                            text = message,
                            sentAt = DateTimeOffset.UtcNow
                        });
                        Console.WriteLine($"[ChatHub] Successfully saved message {messageId} from {sourceUserId} to {userId} in conversation {conversationId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] ERROR saving message from {sourceUserId} to {userId}: {ex.Message}");
                Console.WriteLine($"[ChatHub] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task SendMessageToUserWithMeta(string userId, string message, string? replyToMessageId = null, string? clientMessageId = null)
        {
            var sourceUserId = GetUserIdFromQuery();
            Console.WriteLine($"[ChatHub] SendMessageToUserWithMeta called: sourceUserId={sourceUserId}, userId={userId}, message={message?.Substring(0, Math.Min(50, message?.Length ?? 0))}");
            var messageId = clientMessageId ?? Guid.NewGuid().ToString();
            var sentAt = DateTimeOffset.UtcNow.ToString("O"); // ISO 8601 format with timezone
            // Handle special pin/unpin hint messages: broadcast to both participants and DO NOT persist
            var isPinHint = !string.IsNullOrEmpty(message) && (message.StartsWith("[[pin]]:") || message.StartsWith("[[unpin]]:"));
            if (isPinHint)
            {
                var username = GetUsernameFromQuery();
                if (activeUsers.ContainsKey(userId))
                {
                    await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, replyToMessageId, clientMessageId, sentAt }, username);
                }
                if (!string.IsNullOrEmpty(sourceUserId) && activeUsers.ContainsKey(sourceUserId))
                {
                    await Clients.Client(activeUsers[sourceUserId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, replyToMessageId, clientMessageId, sentAt }, username);
                }
                return; // Skip persistence for pin/unpin hints
            }
            if (activeUsers.ContainsKey(userId))
            {
                var username = GetUsernameFromQuery();
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, replyToMessageId, clientMessageId, sentAt }, username);
            }
            try
            {
                Console.WriteLine($"[ChatHub] Attempting to save message. mongoClient is null: {mongoClient == null}, configuration is null: {configuration == null}");
                
                if (mongoClient == null)
                {
                    Console.WriteLine($"[ChatHub] ERROR: MongoDB client is null. Cannot save message from {sourceUserId} to {userId}");
                    return;
                }
                
                if (configuration == null)
                {
                    Console.WriteLine($"[ChatHub] ERROR: Configuration is null. Cannot save message from {sourceUserId} to {userId}");
                    return;
                }
                
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var connectionString = configuration["ChatMongo:ConnectionString"] ?? configuration.GetConnectionString("MongoDB");
                Console.WriteLine($"[ChatHub] MongoDB config - Database: {dbName}, Collection: {collectionName}, ConnectionString configured: {!string.IsNullOrEmpty(connectionString)}");
                
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<dynamic>(collectionName);
                var conversationId = string.CompareOrdinal(sourceUserId, userId) < 0
                    ? $"{sourceUserId}:{userId}"
                    : $"{userId}:{sourceUserId}";
                
                Console.WriteLine($"[ChatHub] ConversationId: {conversationId}, isPinHint: {isPinHint}");
                
                // Do not persist pin/unpin hint messages
                if (!isPinHint)
                {
                    var messageDoc = new
                    {
                        messageId = messageId,
                        conversationId,
                        senderUserId = sourceUserId,
                        recipientUserId = userId,
                        text = message,
                        replyToMessageId = replyToMessageId,
                        clientMessageId = clientMessageId,
                        sentAt = sentAt
                    };
                    
                    Console.WriteLine($"[ChatHub] Inserting message document: {System.Text.Json.JsonSerializer.Serialize(messageDoc)}");
                    await collection.InsertOneAsync(messageDoc);
                    Console.WriteLine($"[ChatHub] ✓ Successfully saved message {messageId} from {sourceUserId} to {userId} in conversation {conversationId}");
                }
                else
                {
                    Console.WriteLine($"[ChatHub] Skipping persistence for pin/unpin hint message");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] ✗ ERROR saving message from {sourceUserId} to {userId}: {ex.Message}");
                Console.WriteLine($"[ChatHub] Exception type: {ex.GetType().Name}");
                Console.WriteLine($"[ChatHub] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[ChatHub] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        public async Task SendMessageToUserWithCustomData(string userId, string message, Dictionary<string, object>? customData = null, string? replyToMessageId = null, string? clientMessageId = null)
        {
            var sourceUserId = GetUserIdFromQuery();
            var messageId = clientMessageId ?? Guid.NewGuid().ToString();
            var sentAt = DateTimeOffset.UtcNow.ToString("O"); // ISO 8601 format with timezone
            var username = GetUsernameFromQuery();
            
            // Send to recipient
            if (activeUsers.ContainsKey(userId))
            {
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, customData, replyToMessageId, clientMessageId, sentAt }, username);
            }
            
            // Also send to sender (so they see their own message via SignalR)
            if (!string.IsNullOrEmpty(sourceUserId) && activeUsers.ContainsKey(sourceUserId))
            {
                await Clients.Client(activeUsers[sourceUserId]).SendAsync("ReceiveMessage", new { id = messageId, senderId = sourceUserId, text = message, customData, replyToMessageId, clientMessageId, sentAt }, username);
            }
            
            // Persist to Mongo if configured
            try
            {
                if (mongoClient != null && configuration != null)
                {
                    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                    var db = mongoClient.GetDatabase(dbName);
                    var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                    if (!string.IsNullOrEmpty(sourceUserId) && !string.IsNullOrEmpty(userId))
                    {
                        var conversationId = string.CompareOrdinal(sourceUserId, userId) < 0
                            ? $"{sourceUserId}:{userId}"
                            : $"{userId}:{sourceUserId}";
                        var doc = new MongoDB.Bson.BsonDocument
                        {
                            { "messageId", messageId },
                            { "conversationId", conversationId },
                            { "senderUserId", sourceUserId },
                            { "recipientUserId", userId },
                            { "text", message ?? string.Empty },
                            { "sentAt", MongoDB.Bson.BsonValue.Create(sentAt) },
                            { "deliveredAt", MongoDB.Bson.BsonNull.Value },
                            { "readAt", MongoDB.Bson.BsonNull.Value },
                            { "clientMessageId", clientMessageId ?? string.Empty }
                        };
                        
                        // Extract fields from customData and store at top level for easy retrieval
                        if (customData != null && customData.Count > 0)
                        {
                            Console.WriteLine($"[ChatHub] Processing customData with {customData.Count} fields");
                            var customDataBson = new MongoDB.Bson.BsonDocument();
                            foreach (var kvp in customData)
                            {
                                Console.WriteLine($"[ChatHub] Processing customData field: {kvp.Key} = {kvp.Value} (type: {kvp.Value?.GetType().Name})");
                                customDataBson.Add(kvp.Key, MongoDB.Bson.BsonValue.Create(kvp.Value));
                                
                                // Extract common fields to top level for easier querying
                                if (kvp.Key == "messageType" && kvp.Value != null)
                                {
                                    var messageTypeValue = kvp.Value.ToString();
                                    doc.Add("messageType", messageTypeValue);
                                    Console.WriteLine($"[ChatHub] Added messageType to document: {messageTypeValue}");
                                }
                                else if (kvp.Key == "audioUrl" && kvp.Value != null)
                                {
                                    var audioUrlValue = kvp.Value.ToString();
                                    doc.Add("audioUrl", audioUrlValue);
                                    Console.WriteLine($"[ChatHub] Added audioUrl to document: {audioUrlValue}");
                                }
                                else if (kvp.Key == "audioDuration" && kvp.Value != null)
                                {
                                    // Handle both int and double for duration
                                    double durationValue = 0;
                                    if (kvp.Value is int intDuration)
                                    {
                                        durationValue = intDuration;
                                        doc.Add("audioDuration", intDuration);
                                    }
                                    else if (kvp.Value is double doubleDuration)
                                    {
                                        durationValue = doubleDuration;
                                        doc.Add("audioDuration", doubleDuration);
                                    }
                                    else if (kvp.Value is long longDuration)
                                    {
                                        durationValue = (double)longDuration;
                                        doc.Add("audioDuration", durationValue);
                                    }
                                    else if (double.TryParse(kvp.Value.ToString(), out var parsedDuration))
                                    {
                                        durationValue = parsedDuration;
                                        doc.Add("audioDuration", parsedDuration);
                                    }
                                    else
                                    {
                                        doc.Add("audioDuration", MongoDB.Bson.BsonValue.Create(kvp.Value));
                                    }
                                    Console.WriteLine($"[ChatHub] Added audioDuration to document: {durationValue}");
                                }
                                else if (kvp.Key == "imageUrl" && kvp.Value != null)
                                {
                                    var imageUrlValue = kvp.Value.ToString();
                                    doc.Add("imageUrl", imageUrlValue);
                                    Console.WriteLine($"[ChatHub] Added imageUrl to document: {imageUrlValue}");
                                }
                            }
                            // Also store the full customData for completeness
                            doc.Add("customData", customDataBson);
                            Console.WriteLine($"[ChatHub] Added customData document with {customDataBson.ElementCount} fields");
                        }
                        else
                        {
                            // Default to text message type if no customData
                            doc.Add("messageType", "text");
                            Console.WriteLine($"[ChatHub] No customData, defaulting to text message type");
                        }
                        
                        // Log the final document structure for debugging
                        Console.WriteLine($"[ChatHub] Final document fields: {string.Join(", ", doc.Names)}");
                        if (doc.Contains("messageType"))
                        {
                            Console.WriteLine($"[ChatHub] Document messageType: {doc["messageType"]}");
                        }
                        if (doc.Contains("audioUrl"))
                        {
                            Console.WriteLine($"[ChatHub] Document audioUrl: {doc["audioUrl"]}");
                        }
                        if (doc.Contains("audioDuration"))
                        {
                            Console.WriteLine($"[ChatHub] Document audioDuration: {doc["audioDuration"]}");
                        }
                        
                        if (!string.IsNullOrEmpty(replyToMessageId))
                        {
                            doc.Add("replyToMessageId", replyToMessageId);
                        }
                        
                        await collection.InsertOneAsync(doc);
                        Console.WriteLine($"[ChatHub] Successfully saved custom message {messageId} from {sourceUserId} to {userId} in conversation {conversationId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] ERROR saving custom message from {sourceUserId} to {userId}: {ex.Message}");
                Console.WriteLine($"[ChatHub] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task<bool> EditMessage(string messageId, string newText)
        {
            try
            {
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
                Console.WriteLine($"[ChatHub] DeleteMessage called: messageId={messageId}");
                
                if (mongoClient == null || configuration == null)
                {
                    Console.WriteLine("[ChatHub] DeleteMessage: mongoClient or configuration is null");
                    return false;
                }
                
                var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
                var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
                var db = mongoClient.GetDatabase(dbName);
                var collection = db.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                var filter = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("messageId", messageId);
                
                Console.WriteLine($"[ChatHub] DeleteMessage: Looking for message with messageId={messageId}");
                var doc = await collection.Find(filter).FirstOrDefaultAsync();
                
                if (doc == null)
                {
                    Console.WriteLine($"[ChatHub] DeleteMessage: Message not found with messageId={messageId}");
                    return false;
                }
                
                Console.WriteLine($"[ChatHub] DeleteMessage: Found message, deleting...");
                var result = await collection.DeleteOneAsync(filter);
                
                if (result.DeletedCount > 0)
                {
                    Console.WriteLine($"[ChatHub] DeleteMessage: Successfully deleted message {messageId}");
                    
                    string? senderId = null;
                    string? recipientId = null;
                    if (doc != null)
                    {
                        var sVal = doc.GetValue("senderUserId", MongoDB.Bson.BsonNull.Value);
                        if (!sVal.IsBsonNull) senderId = sVal.AsString;
                        var rVal = doc.GetValue("recipientUserId", MongoDB.Bson.BsonNull.Value);
                        if (!rVal.IsBsonNull) recipientId = rVal.AsString;
                    }
                    
                    Console.WriteLine($"[ChatHub] DeleteMessage: senderId={senderId}, recipientId={recipientId}");
                    Console.WriteLine($"[ChatHub] DeleteMessage: activeUsers count={activeUsers.Count}");
                    
                    // Send event to both users if they're online
                    if (!string.IsNullOrEmpty(recipientId))
                    {
                        if (activeUsers.ContainsKey(recipientId))
                        {
                            Console.WriteLine($"[ChatHub] DeleteMessage: Sending MessageDeleted event to recipient {recipientId}");
                            await Clients.Client(activeUsers[recipientId]).SendAsync("MessageDeleted", new { id = messageId });
                        }
                        else
                        {
                            Console.WriteLine($"[ChatHub] DeleteMessage: Recipient {recipientId} is not in activeUsers");
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(senderId))
                    {
                        if (activeUsers.ContainsKey(senderId))
                        {
                            Console.WriteLine($"[ChatHub] DeleteMessage: Sending MessageDeleted event to sender {senderId}");
                            await Clients.Client(activeUsers[senderId]).SendAsync("MessageDeleted", new { id = messageId });
                        }
                        else
                        {
                            Console.WriteLine($"[ChatHub] DeleteMessage: Sender {senderId} is not in activeUsers");
                        }
                    }
                    
                    // Also send to the current user (caller) if they're not the sender or recipient
                    var currentUserId = GetUserIdFromQuery();
                    if (!string.IsNullOrEmpty(currentUserId) && currentUserId != senderId && currentUserId != recipientId)
                    {
                        if (activeUsers.ContainsKey(currentUserId))
                        {
                            Console.WriteLine($"[ChatHub] DeleteMessage: Sending MessageDeleted event to current user {currentUserId}");
                            await Clients.Client(activeUsers[currentUserId]).SendAsync("MessageDeleted", new { id = messageId });
                        }
                    }
                    
                    return true;
                }
                else
                {
                    Console.WriteLine($"[ChatHub] DeleteMessage: No message deleted (DeletedCount={result.DeletedCount})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] DeleteMessage: Exception: {ex.Message}");
                Console.WriteLine($"[ChatHub] DeleteMessage: Stack trace: {ex.StackTrace}");
                return false;
            }
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
                // Set both readAt timestamp and read boolean field for persistence
                var update = MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Update
                    .Set("readAt", DateTimeOffset.UtcNow)
                    .Set("read", true);
                var result = await collection.UpdateManyAsync(filter, update);
                var count = (int)result.ModifiedCount;
                Console.WriteLine($"[ChatHub] MarkMessagesRead: Marked {count} messages as read for user {userId} from peer {peerUserId}");
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
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] MarkMessagesRead error: {ex.Message}");
                Console.WriteLine($"[ChatHub] Stack trace: {ex.StackTrace}");
            }
            return 0;
        }

        public async Task AddUser(string userId, string connectionId)
        {
            // Upsert connection id (thread-safe)
            activeUsers.AddOrUpdate(userId, connectionId, (_, __) => connectionId);
            await Clients.All.SendAsync("receiveactiveusers", GetActiveUserIds());
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
            var connectionId = GetConnectionId();
            
            // Try to get userId from query string using Context.GetHttpContext()
            string? userId = null;
            try
            {
                var httpContext = Context.GetHttpContext();
                if (httpContext != null)
                {
                    userId = httpContext.Request?.Query["userId"].ToString();
                    Console.WriteLine($"[ChatHub] OnConnectedAsync: Got userId from Context.GetHttpContext(): '{userId}'");
                    
                    // Store in Context.Items for later retrieval
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        Context.Items["userId"] = userId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] OnConnectedAsync: Error getting HttpContext: {ex.Message}");
            }
            
            // Fallback to GetUserIdFromQuery
            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = GetUserIdFromQuery();
            }
            
            Console.WriteLine($"[ChatHub] OnConnectedAsync: userId={userId}, connectionId={connectionId}");
            
            if (!string.IsNullOrEmpty(userId))
            {
                activeUsers.AddOrUpdate(userId, connectionId, (_, __) => connectionId);
                Console.WriteLine($"[ChatHub] Registered user {userId} with connection {connectionId}. Active users count: {activeUsers.Count}");
            }
            else
            {
                Console.WriteLine($"[ChatHub] WARNING: userId is empty in OnConnectedAsync. Cannot register user.");
                Console.WriteLine($"[ChatHub] Connection URL: {Context.GetHttpContext()?.Request?.Path}");
                Console.WriteLine($"[ChatHub] Query string: {Context.GetHttpContext()?.Request?.QueryString}");
            }
            
            await Clients.All.SendAsync("receiveactiveusers", GetActiveUserIds());
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
            await Clients.All.SendAsync("receiveactiveusers", GetActiveUserIds());
            await base.OnDisconnectedAsync(exception);
        }

        private string GetUserIdFromQuery()
        {
            // Try multiple ways to get userId from SignalR connection
            string? result = null;
            
            // Method 1: Try HttpContext query string
            var httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                var userIdString = httpContext.Request?.Query["userId"].ToString();
                if (!string.IsNullOrWhiteSpace(userIdString))
                {
                    result = userIdString;
                    Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Found in HttpContext.Query: '{result}'");
                    return result;
                }
            }
            
            // Method 2: Try Context.GetHttpContext() (SignalR's way)
            try
            {
                var context = Context.GetHttpContext();
                if (context != null)
                {
                    var userIdString = context.Request?.Query["userId"].ToString();
                    if (!string.IsNullOrWhiteSpace(userIdString))
                    {
                        result = userIdString;
                        Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Found in Context.GetHttpContext().Query: '{result}'");
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Context.GetHttpContext() failed: {ex.Message}");
            }
            
            // Method 3: Try Context.UserIdentifier
            result = Context.UserIdentifier;
            if (!string.IsNullOrWhiteSpace(result))
            {
                Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Found in Context.UserIdentifier: '{result}'");
                return result;
            }
            
            // Method 4: Try to get from connection items/custom data
            try
            {
                if (Context.Items.TryGetValue("userId", out var userIdObj) && userIdObj is string userIdStr)
                {
                    result = userIdStr;
                    Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Found in Context.Items: '{result}'");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatHub] GetUserIdFromQuery: Context.Items access failed: {ex.Message}");
            }
            
            Console.WriteLine($"[ChatHub] GetUserIdFromQuery: WARNING - Could not find userId in any location");
            return string.Empty;
        }

        private string? GetUsernameFromQuery()
        {
            var httpContext = httpContextAccessor.HttpContext;
            return httpContext?.Request?.Query["username"].ToString();
        }

        // Call signaling methods
        [HubMethodName("InitiateCall")]
        public async Task InitiateCall(string calleeUserId, string callType, string callId)
        {
            var callerUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(callerUserId) || string.IsNullOrEmpty(calleeUserId)) return;
            
            var finalCallId = callId;
            
            // Store call state for later use in SendCallSignal
            activeCalls.AddOrUpdate(finalCallId, (callerUserId, calleeUserId), (_, __) => (callerUserId, calleeUserId));
            
            var payload = new { 
                callerUserId, 
                calleeUserId, 
                callType, // "audio" or "video"
                callId = finalCallId,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

            // Send to both participants if they're online
            if (activeUsers.ContainsKey(calleeUserId))
            {
                await Clients.Client(activeUsers[calleeUserId]).SendAsync("callinitiated", payload);
            }
            if (activeUsers.ContainsKey(callerUserId))
            {
                await Clients.Client(activeUsers[callerUserId]).SendAsync("callinitiated", payload);
            }
        }

        [HubMethodName("AcceptCall")]
        public async Task AcceptCall(string callerUserId, string callId)
        {
            var calleeUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(callerUserId) || string.IsNullOrEmpty(calleeUserId)) return;
            
            var payload = new { 
                callerUserId, 
                calleeUserId, 
                callId,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

            // Send to both participants if they're online
            if (activeUsers.ContainsKey(callerUserId))
            {
                await Clients.Client(activeUsers[callerUserId]).SendAsync("callaccepted", payload);
            }
            if (activeUsers.ContainsKey(calleeUserId))
            {
                await Clients.Client(activeUsers[calleeUserId]).SendAsync("callaccepted", payload);
            }
        }

        [HubMethodName("RejectCall")]
        public async Task RejectCall(string callerUserId, string callId)
        {
            var calleeUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(callerUserId) || string.IsNullOrEmpty(calleeUserId)) return;
            
            // Remove call state when call is rejected
            activeCalls.TryRemove(callId, out _);
            
            var payload = new { 
                callerUserId, 
                calleeUserId, 
                callId,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

            // Send to both participants if they're online
            if (activeUsers.ContainsKey(callerUserId))
            {
                await Clients.Client(activeUsers[callerUserId]).SendAsync("callrejected", payload);
            }
            if (activeUsers.ContainsKey(calleeUserId))
            {
                await Clients.Client(activeUsers[calleeUserId]).SendAsync("callrejected", payload);
            }
        }

        [HubMethodName("EndCall")]
        public async Task EndCall(string otherUserId, string callId)
        {
            var currentUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(otherUserId)) return;
            
            // Remove call state when call ends
            activeCalls.TryRemove(callId, out _);
            
            var payload = new { 
                callerUserId = currentUserId, 
                calleeUserId = otherUserId, 
                callId,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

            // Send to both participants if they're online
            if (activeUsers.ContainsKey(otherUserId))
            {
                await Clients.Client(activeUsers[otherUserId]).SendAsync("callended", payload);
            }
            if (activeUsers.ContainsKey(currentUserId))
            {
                await Clients.Client(activeUsers[currentUserId]).SendAsync("callended", payload);
            }
        }

        [HubMethodName("SendCallSignal")]
        public async Task SendCallSignal(string otherUserId, string callId, string signalType, object signalData)
        {
            var currentUserId = GetUserIdFromQuery();
            if (string.IsNullOrEmpty(currentUserId) || string.IsNullOrEmpty(otherUserId)) return;
            
            // Determine caller and callee based on signal type and call state:
            // - "offer": currentUserId is the caller (sending offer to callee)
            // - "answer": currentUserId is the callee (sending answer to caller), so otherUserId is the caller
            // - "ice-candidate": Use stored call state to determine caller/callee
            string callerUserId;
            string calleeUserId;
            
            if (signalType == "offer")
            {
                // Offer is sent by caller to callee
                callerUserId = currentUserId;
                calleeUserId = otherUserId;
            }
            else if (signalType == "answer")
            {
                // Answer is sent by callee to caller
                callerUserId = otherUserId; // The recipient of answer is the original caller
                calleeUserId = currentUserId; // The sender of answer is the callee
            }
            else // "ice-candidate"
            {
                // For ICE candidates, use stored call state to determine caller/callee
                if (activeCalls.TryGetValue(callId, out var callState))
                {
                    callerUserId = callState.callerUserId;
                    calleeUserId = callState.calleeUserId;
                }
                else
                {
                    // Fallback: if we don't have call state, infer from direction
                    // Caller sends ICE to callee (otherUserId is callee)
                    // Callee sends ICE to caller (otherUserId is caller)
                    // Since callee typically sends to caller (data.callerUserId), assume otherUserId is caller
                    // This is not perfect but better than nothing
                    callerUserId = otherUserId;
                    calleeUserId = currentUserId;
                    Console.WriteLine($"[ChatHub] SendCallSignal: No call state found for callId {callId}, using fallback");
                }
            }
            
            var payload = new { 
                callerUserId, 
                calleeUserId, 
                callId,
                signalType, // "offer", "answer", "ice-candidate"
                signalData,
                timestamp = DateTimeOffset.UtcNow.ToString("O")
            };

            // Send to the other participant if they're online
            if (activeUsers.ContainsKey(otherUserId))
            {
                await Clients.Client(activeUsers[otherUserId]).SendAsync("callsignal", payload);
                Console.WriteLine($"[ChatHub] SendCallSignal: Sent {signalType} signal from {currentUserId} to {otherUserId} for call {callId}");
            }
            else
            {
                Console.WriteLine($"[ChatHub] SendCallSignal: User {otherUserId} is not online. Active users: {string.Join(", ", activeUsers.Keys)}");
            }
        }
    }
}

