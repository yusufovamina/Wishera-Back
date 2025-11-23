using BusinessLayer.Hubs;
using BusinessLayer.Services.FileServices.Implementations;
using BusinessLayer.Services.FileServices.Interfaces;
using BusinessLayer.Services.PrivateMessageServices.Implementations;
using BusinessLayer.Services.PrivateMessageServices.Interfaces;
using BusinessLayer.Services.UserService.Implementations;
using BusinessLayer.Services.UserService.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using PresentationLayer;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Messaging-only: no persistence or auth required for delivery



// No repositories required for pure in-memory messaging

// Configure MongoDB (used for message persistence if needed)
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var mongoUrl = Environment.GetEnvironmentVariable("MONGO_URL")
        ?? Environment.GetEnvironmentVariable("MONGODB_URI")
        ?? cfg["ChatMongo:ConnectionString"]
        ?? cfg.GetConnectionString("MongoDB");
    if (string.IsNullOrWhiteSpace(mongoUrl))
    {
        throw new InvalidOperationException("MongoDB connection string is not configured. Set ChatMongo:ConnectionString or MONGO_URL.");
    }
    return new MongoClient(mongoUrl);
});

builder.Services.AddHttpClient();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddTransient<GlobalExceptionHandlingMiddleware>();


builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(c =>
{
    c.AddDefaultPolicy(options =>
    {
        // SignalR requires AllowCredentials, which cannot be used with AllowAnyOrigin
        // So we must specify origins even in development
        options.WithOrigins(
            "http://localhost:3000",      // Web frontend
            "http://localhost:3001",      // Web frontend alt
            "http://localhost:8081",      // React Native Metro bundler / Expo web
            "http://localhost:8082",      // Expo web alternative port
            "http://localhost:19000",     // Expo development
            "http://localhost:19001",     // Expo development alt
            "http://localhost:19002",     // Expo development alt 2
            "http://localhost:19006",     // Expo tunnel
            "http://127.0.0.1:3000",      // iOS simulator web
            "http://127.0.0.1:3001",      // iOS simulator web alt
            "http://127.0.0.1:8081",      // iOS simulator
            "http://127.0.0.1:8082",      // iOS simulator alt
            "http://127.0.0.1:19000",     // iOS simulator Expo
            "http://127.0.0.1:19001",     // iOS simulator Expo alt
            "http://127.0.0.1:19002",     // iOS simulator Expo alt 2
            "http://127.0.0.1:19006",     // iOS simulator Expo tunnel
            "http://10.0.2.2:8081",       // Android emulator
            "http://10.0.2.2:19000",      // Android emulator Expo
            "http://10.0.2.2:19006"       // Android emulator Expo tunnel
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});


builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Ensure Mongo collection and indexes exist at startup
builder.Services.AddHostedService<MongoSetupHostedService>();

var app = builder.Build();

app.UseRouting();
app.UseCors();

// Serve static wallpapers from wwwroot
app.UseStaticFiles();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Map SignalR hub - must be before other routes
app.MapHub<ChatHub>("/chat");

// Minimal history API backed by Mongo used by ChatHub
app.MapGet("/api/chat/history", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userA,
    [FromQuery] string userB,
    [FromQuery] int page,
    [FromQuery] int pageSize) =>
{
    if (string.IsNullOrWhiteSpace(userA) || string.IsNullOrWhiteSpace(userB))
    {
        return Results.BadRequest(new { message = "userA and userB are required" });
    }
    var a = userA;
    var b = userB;
    var conversationId = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);

    var filter = Builders<BsonDocument>.Filter.Eq("conversationId", conversationId);
    var cursor = await collection.Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Ascending("sentAt"))
        .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
        .Limit(Math.Max(1, pageSize))
        .ToListAsync();

    var items = cursor.Select(d =>
    {
        var sentVal = d.GetValue("sentAt", BsonNull.Value);
        DateTimeOffset sentAtValue = DateTimeOffset.MinValue;
        if (sentVal is BsonDateTime bdt)
        {
            sentAtValue = bdt.ToUniversalTime();
        }
        else if (sentVal.IsString && DateTimeOffset.TryParse(sentVal.AsString, out var parsed))
        {
            sentAtValue = parsed.ToUniversalTime();
        }

        // Fallback: if sentAt is missing or invalid (MinValue), try to extract from _id
        if (sentAtValue == DateTimeOffset.MinValue || sentAtValue == default)
        {
            var idVal = d.GetValue("_id", BsonNull.Value);
            if (idVal is BsonObjectId objectId)
            {
                sentAtValue = objectId.Value.CreationTime.ToUniversalTime();
            }
            else if (idVal.IsString && MongoDB.Bson.ObjectId.TryParse(idVal.AsString, out var oid))
            {
                sentAtValue = oid.CreationTime.ToUniversalTime();
            }
        }

        // Extract reactions as emoji -> [userIds]
        Dictionary<string, string[]> reactions = new();
        var reactionsVal = d.GetValue("reactions", BsonNull.Value);
        if (reactionsVal is BsonDocument reactionsDoc)
        {
            foreach (var emojiEl in reactionsDoc.Elements)
            {
                var emoji = emojiEl.Name;
                if (emojiEl.Value is BsonDocument usersDoc)
                {
                    var users = usersDoc.Elements
                        .Where(e => e.Value.IsBoolean && e.Value.AsBoolean)
                        .Select(e => e.Name)
                        .ToArray();
                    reactions[emoji] = users;
                }
            }
        }
        else if (reactionsVal is BsonArray reactionsArr)
        {
            // Legacy shape: [{ userId, emoji }]
            foreach (var el in reactionsArr)
            {
                if (el is BsonDocument rd)
                {
                    var emoji = rd.GetValue("emoji", BsonNull.Value).IsBsonNull ? null : rd["emoji"].AsString;
                    var userId = rd.GetValue("userId", BsonNull.Value).IsBsonNull ? null : rd["userId"].AsString;
                    if (!string.IsNullOrEmpty(emoji) && !string.IsNullOrEmpty(userId))
                    {
                        if (!reactions.ContainsKey(emoji)) reactions[emoji] = Array.Empty<string>();
                        var list = reactions[emoji].ToList();
                        if (!list.Contains(userId)) list.Add(userId);
                        reactions[emoji] = list.ToArray();
                    }
                }
            }
        }

         // Extract replyToMessageId
         string? replyToMessageId = null;
         var replyToVal = d.GetValue("replyToMessageId", BsonNull.Value);
         if (!replyToVal.IsBsonNull && replyToVal.IsString)
         {
             replyToMessageId = replyToVal.AsString;
         }

         // Try to get messageType, audioUrl, audioDuration from top level first, then from customData
         string? messageType = d.GetValue("messageType", BsonNull.Value).IsBsonNull 
             ? (d.GetValue("customData", BsonNull.Value).IsBsonNull || !d["customData"].IsBsonDocument 
                 ? "text" 
                 : (d["customData"].AsBsonDocument.GetValue("messageType", BsonNull.Value).IsBsonNull 
                     ? "text" 
                     : d["customData"].AsBsonDocument["messageType"].AsString))
             : d["messageType"].AsString;
         
         string? audioUrl = d.GetValue("audioUrl", BsonNull.Value).IsBsonNull
             ? (d.GetValue("customData", BsonNull.Value).IsBsonNull || !d["customData"].IsBsonDocument
                 ? null
                 : (d["customData"].AsBsonDocument.GetValue("audioUrl", BsonNull.Value).IsBsonNull
                     ? null
                     : d["customData"].AsBsonDocument["audioUrl"].AsString))
             : d["audioUrl"].AsString;
         
         // Helper function to safely convert BsonValue to double (handles both int and double)
         double? GetAudioDuration(BsonValue? value)
         {
             if (value == null || value.IsBsonNull) return null;
             if (value.IsInt32) return (double)value.AsInt32;
             if (value.IsInt64) return (double)value.AsInt64;
             if (value.IsDouble) return value.AsDouble;
             if (double.TryParse(value.ToString(), out var parsed)) return parsed;
             return null;
         }
         
         var audioDurationVal = d.GetValue("audioDuration", BsonNull.Value);
         double? audioDuration = GetAudioDuration(audioDurationVal);
         
         // If not found at top level, check customData
         if (audioDuration == null && !d.GetValue("customData", BsonNull.Value).IsBsonNull && d["customData"].IsBsonDocument)
         {
             var customAudioDuration = d["customData"].AsBsonDocument.GetValue("audioDuration", BsonNull.Value);
             audioDuration = GetAudioDuration(customAudioDuration);
         }

         // Extract call-specific fields for call messages
         string? callType = null;
         string? callStatus = null;
         int? callDuration = null;
         
         if (messageType == "call")
         {
             var callTypeVal = d.GetValue("callType", BsonNull.Value);
             if (!callTypeVal.IsBsonNull && callTypeVal.IsString)
             {
                 callType = callTypeVal.AsString;
             }
             
             var callStatusVal = d.GetValue("callStatus", BsonNull.Value);
             if (!callStatusVal.IsBsonNull && callStatusVal.IsString)
             {
                 callStatus = callStatusVal.AsString;
             }
             
             var callDurationVal = d.GetValue("callDuration", BsonNull.Value);
             if (!callDurationVal.IsBsonNull)
             {
                 if (callDurationVal.IsInt32)
                 {
                     callDuration = callDurationVal.AsInt32;
                 }
                 else if (callDurationVal.IsInt64)
                 {
                     callDuration = (int)callDurationVal.AsInt64;
                 }
                 else if (callDurationVal.IsDouble)
                 {
                     callDuration = (int)callDurationVal.AsDouble;
                 }
             }
         }

         // Determine read status: message is read if read field is true OR readAt exists and is not null
         var readVal = d.GetValue("read", BsonNull.Value);
         var readAtVal = d.GetValue("readAt", BsonNull.Value);
         bool isRead = false;
         if (!readVal.IsBsonNull && readVal.IsBoolean)
         {
             isRead = readVal.AsBoolean;
         }
         else if (!readAtVal.IsBsonNull)
         {
             // If readAt exists, consider the message as read
             isRead = true;
         }

         // Build result object
         var result = new
         {
             id = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
             conversationId = d.GetValue("conversationId", BsonNull.Value).IsBsonNull ? string.Empty : d["conversationId"].AsString,
             senderUserId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
             recipientUserId = d.GetValue("recipientUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["recipientUserId"].AsString,
             text = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
             sentAt = sentAtValue,
             reactions = reactions,
             replyToMessageId = replyToMessageId,
             messageType = messageType,
             audioUrl = audioUrl,
             audioDuration = audioDuration,
             callType = callType,
             callStatus = callStatus,
             callDuration = callDuration,
             read = isRead,
             isRead = isRead // Include both for compatibility
         };

         return result;
    });

    return Results.Ok(items);
});

// Alternative history endpoint with path parameters (for mobile app compatibility)
app.MapGet("/api/chat/history/{userId}/{peerUserId}", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    string userId,
    string peerUserId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50) =>
{
    if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(peerUserId))
    {
        return Results.BadRequest(new { message = "userId and peerUserId are required" });
    }
    var a = userId;
    var b = peerUserId;
    var conversationId = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);

    var filter = Builders<BsonDocument>.Filter.Eq("conversationId", conversationId);
    var cursor = await collection.Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Ascending("sentAt"))
        .Skip(Math.Max(0, page - 1) * Math.Max(1, pageSize))
        .Limit(Math.Max(1, pageSize))
        .ToListAsync();

    var items = cursor.Select(d =>
    {
        var sentVal = d.GetValue("sentAt", BsonNull.Value);
        DateTimeOffset sentAtValue = DateTimeOffset.MinValue;
        if (sentVal is BsonDateTime bdt)
        {
            sentAtValue = bdt.ToUniversalTime();
        }
        else if (sentVal.IsString && DateTimeOffset.TryParse(sentVal.AsString, out var parsed))
        {
            sentAtValue = parsed.ToUniversalTime();
        }

        // Fallback: if sentAt is missing or invalid (MinValue), try to extract from _id
        if (sentAtValue == DateTimeOffset.MinValue || sentAtValue == default)
        {
            var idVal = d.GetValue("_id", BsonNull.Value);
            if (idVal is BsonObjectId objectId)
            {
                sentAtValue = objectId.Value.CreationTime.ToUniversalTime();
            }
            else if (idVal.IsString && MongoDB.Bson.ObjectId.TryParse(idVal.AsString, out var oid))
            {
                sentAtValue = oid.CreationTime.ToUniversalTime();
            }
        }

        // Extract reactions as emoji -> [userIds]
        Dictionary<string, string[]> reactions = new();
        var reactionsVal = d.GetValue("reactions", BsonNull.Value);
        if (reactionsVal is BsonDocument reactionsDoc)
        {
            foreach (var emojiEl in reactionsDoc.Elements)
            {
                var emoji = emojiEl.Name;
                if (emojiEl.Value is BsonDocument usersDoc)
                {
                    var users = usersDoc.Elements
                        .Where(e => e.Value.IsBoolean && e.Value.AsBoolean)
                        .Select(e => e.Name)
                        .ToArray();
                    reactions[emoji] = users;
                }
            }
        }
        else if (reactionsVal is BsonArray reactionsArr)
        {
            // Legacy shape: [{ userId, emoji }]
            foreach (var el in reactionsArr)
            {
                if (el is BsonDocument rd)
                {
                    var emoji = rd.GetValue("emoji", BsonNull.Value).IsBsonNull ? null : rd["emoji"].AsString;
                    var userId = rd.GetValue("userId", BsonNull.Value).IsBsonNull ? null : rd["userId"].AsString;
                    if (!string.IsNullOrEmpty(emoji) && !string.IsNullOrEmpty(userId))
                    {
                        if (!reactions.ContainsKey(emoji)) reactions[emoji] = Array.Empty<string>();
                        var list = reactions[emoji].ToList();
                        if (!list.Contains(userId)) list.Add(userId);
                        reactions[emoji] = list.ToArray();
                    }
                }
            }
        }

        // Extract messageType, audioUrl, audioDuration from top level first, then from customData
        var messageTypeVal = d.GetValue("messageType", BsonNull.Value);
        var audioUrlVal = d.GetValue("audioUrl", BsonNull.Value);
        var audioDurationVal = d.GetValue("audioDuration", BsonNull.Value);
        
        string? messageType = messageTypeVal.IsBsonNull 
            ? (d.GetValue("customData", BsonNull.Value).IsBsonNull || !d["customData"].IsBsonDocument 
                ? "text" 
                : (d["customData"].AsBsonDocument.GetValue("messageType", BsonNull.Value).IsBsonNull 
                    ? "text" 
                    : d["customData"].AsBsonDocument["messageType"].AsString))
            : messageTypeVal.AsString;
        
        string? audioUrl = audioUrlVal.IsBsonNull
            ? (d.GetValue("customData", BsonNull.Value).IsBsonNull || !d["customData"].IsBsonDocument
                ? null
                : (d["customData"].AsBsonDocument.GetValue("audioUrl", BsonNull.Value).IsBsonNull
                    ? null
                    : d["customData"].AsBsonDocument["audioUrl"].AsString))
            : audioUrlVal.AsString;
        
        // Helper function to safely convert BsonValue to double (handles both int and double)
        double? GetAudioDuration(BsonValue? value)
        {
            if (value == null || value.IsBsonNull) return null;
            if (value.IsInt32) return (double)value.AsInt32;
            if (value.IsInt64) return (double)value.AsInt64;
            if (value.IsDouble) return value.AsDouble;
            if (double.TryParse(value.ToString(), out var parsed)) return parsed;
            return null;
        }
        
        double? audioDuration = GetAudioDuration(audioDurationVal);
        
        // If not found at top level, check customData
        if (audioDuration == null && !d.GetValue("customData", BsonNull.Value).IsBsonNull && d["customData"].IsBsonDocument)
        {
            var customAudioDuration = d["customData"].AsBsonDocument.GetValue("audioDuration", BsonNull.Value);
            audioDuration = GetAudioDuration(customAudioDuration);
        }
        
        // Log voice messages for debugging
        if (messageType == "voice")
        {
            Console.WriteLine($"[Program] Voice message retrieved - ID: {d.GetValue("messageId", BsonNull.Value)}, audioUrl: {audioUrl ?? "null"}, audioDuration: {audioDuration?.ToString() ?? "null"}");
        }
        
        // Extract call-specific fields for call messages
        string? callType = null;
        string? callStatus = null;
        int? callDuration = null;
        
        if (messageType == "call")
        {
            var callTypeVal = d.GetValue("callType", BsonNull.Value);
            if (!callTypeVal.IsBsonNull && callTypeVal.IsString)
            {
                callType = callTypeVal.AsString;
            }
            
            var callStatusVal = d.GetValue("callStatus", BsonNull.Value);
            if (!callStatusVal.IsBsonNull && callStatusVal.IsString)
            {
                callStatus = callStatusVal.AsString;
            }
            
            var callDurationVal = d.GetValue("callDuration", BsonNull.Value);
            if (!callDurationVal.IsBsonNull)
            {
                if (callDurationVal.IsInt32)
                {
                    callDuration = callDurationVal.AsInt32;
                }
                else if (callDurationVal.IsInt64)
                {
                    callDuration = (int)callDurationVal.AsInt64;
                }
                else if (callDurationVal.IsDouble)
                {
                    callDuration = (int)callDurationVal.AsDouble;
                }
            }
        }
        
        var imageUrl = d.GetValue("imageUrl", BsonNull.Value).IsBsonNull ? null : d["imageUrl"].AsString;
        var replyToMessageId = d.GetValue("replyToMessageId", BsonNull.Value).IsBsonNull ? null : d["replyToMessageId"].AsString;
        
        // Determine read status from the requesting user's perspective
        // If the requesting user is the sender, the message is always "read" from their perspective
        // If the requesting user is the recipient, check if read field is true OR readAt exists
        var senderUserId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString;
        bool isRead = false;
        if (senderUserId == userId)
        {
            // User sent this message, so it's always "read" from their perspective
            isRead = true;
        }
        else
        {
            // User received this message, check if they've read it
            var readVal = d.GetValue("read", BsonNull.Value);
            var readAtVal = d.GetValue("readAt", BsonNull.Value);
            if (!readVal.IsBsonNull && readVal.IsBoolean)
            {
                isRead = readVal.AsBoolean;
            }
            else if (!readAtVal.IsBsonNull)
            {
                // If readAt exists, consider the message as read
                isRead = true;
            }
        }
        
        return new
        {
            id = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
            conversationId = d.GetValue("conversationId", BsonNull.Value).IsBsonNull ? string.Empty : d["conversationId"].AsString,
            userId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
            senderId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
            username = d.GetValue("senderName", BsonNull.Value).IsBsonNull ? string.Empty : d["senderName"].AsString,
            senderName = d.GetValue("senderName", BsonNull.Value).IsBsonNull ? string.Empty : d["senderName"].AsString,
            text = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
            message = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
            createdAt = sentAtValue.ToString("O"), // ISO 8601 format string
            sentAt = sentAtValue.ToString("O"), // ISO 8601 format string
            messageType = messageType,
            audioUrl = audioUrl,
            audioDuration = audioDuration,
            imageUrl = imageUrl,
            replyToMessageId = replyToMessageId,
            reactions = reactions,
            callType = callType,
            callStatus = callStatus,
            callDuration = callDuration,
            read = isRead,
            isRead = isRead // Include both for compatibility
        };
    });

    return Results.Ok(items);
});

// Get all conversations for a user (returns all users they've messaged)
app.MapGet("/api/chat/conversations/{userId}", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    string userId) =>
{
    try
    {
        if (mongoClient == null || configuration == null)
        {
            return Results.Ok(new List<object>());
        }

        var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
        var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
        var db = mongoClient.GetDatabase(dbName);
        var collection = db.GetCollection<BsonDocument>(collectionName);

        // Find all messages where the user is either sender or recipient
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("senderUserId", userId),
            Builders<BsonDocument>.Filter.Eq("recipientUserId", userId)
        );

        // Get distinct conversation IDs
        var conversationIds = await collection.DistinctAsync<string>("conversationId", filter);
        var conversationIdList = await conversationIds.ToListAsync();

        // Extract unique user IDs from conversation IDs
        var partnerUserIds = new HashSet<string>();
        foreach (var conversationId in conversationIdList)
        {
            var parts = conversationId.Split(':');
            if (parts.Length == 2)
            {
                var otherUserId = parts[0] == userId ? parts[1] : parts[0];
                if (otherUserId != userId)
                {
                    partnerUserIds.Add(otherUserId);
                }
            }
        }

        // For each partner, get the last message
        var conversations = new List<object>();
        foreach (var partnerId in partnerUserIds)
        {
            var conversationId = string.CompareOrdinal(userId, partnerId) < 0
                ? $"{userId}:{partnerId}"
                : $"{partnerId}:{userId}";

            // Get the last message for this conversation
            var conversationFilter = Builders<BsonDocument>.Filter.Eq("conversationId", conversationId);
            var lastMessageDoc = await collection
                .Find(conversationFilter)
                .Sort(Builders<BsonDocument>.Sort.Descending("sentAt"))
                .Limit(1)
                .FirstOrDefaultAsync();

            if (lastMessageDoc != null)
            {
                var sentVal = lastMessageDoc.GetValue("sentAt", BsonNull.Value);
                DateTimeOffset sentAtValue = DateTimeOffset.MinValue;
                if (sentVal is BsonDateTime bdt)
                {
                    sentAtValue = bdt.ToUniversalTime();
                }
                else if (sentVal.IsString && DateTimeOffset.TryParse(sentVal.AsString, out var parsed))
                {
                    sentAtValue = parsed.ToUniversalTime();
                }

                var messageType = lastMessageDoc.GetValue("messageType", BsonNull.Value).IsBsonNull
                    ? "text"
                    : lastMessageDoc["messageType"].AsString;

                conversations.Add(new
                {
                    userId = partnerId,
                    lastMessage = new
                    {
                        id = lastMessageDoc.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : lastMessageDoc["messageId"].AsString,
                        text = lastMessageDoc.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : lastMessageDoc["text"].AsString,
                        messageType = messageType,
                        sentAt = sentAtValue != DateTimeOffset.MinValue ? sentAtValue.ToString("O") : null,
                        senderUserId = lastMessageDoc.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : lastMessageDoc["senderUserId"].AsString
                    }
                });
            }
        }

        // Sort by last message time (most recent first)
        conversations = conversations.OrderByDescending(c =>
        {
            var lastMsg = ((dynamic)c).lastMessage;
            var sentAt = lastMsg?.sentAt?.ToString() ?? "";
            if (DateTimeOffset.TryParse(sentAt, out DateTimeOffset date))
            {
                return date;
            }
            return DateTimeOffset.MinValue;
        }).ToList();

        return Results.Ok(conversations);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Program] Error getting conversations: {ex.Message}");
        Console.WriteLine($"[Program] Stack trace: {ex.StackTrace}");
        return Results.StatusCode(500);
    }
});

// Edit a message text
app.MapPost("/api/chat/message/edit", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] EditRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body?.MessageId) || string.IsNullOrWhiteSpace(body?.NewText))
    {
        return Results.BadRequest(new { message = "messageId and newText are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);
    var filter = Builders<BsonDocument>.Filter.Eq("messageId", body.MessageId);
    var update = Builders<BsonDocument>.Update.Set("text", body.NewText);
    var result = await collection.UpdateOneAsync(filter, update);
    return result.ModifiedCount > 0 ? Results.Ok(new { updated = true }) : Results.NotFound(new { updated = false });
});

// Delete a message
app.MapPost("/api/chat/message/delete", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] DeleteRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body?.MessageId))
    {
        Console.WriteLine("[Program] Delete message: MessageId is required but was null or empty");
        return Results.BadRequest(new { message = "messageId is required" });
    }
    
    Console.WriteLine($"[Program] Delete message request received: MessageId={body.MessageId}");
    
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);
    
    // Try both messageId and id fields (in case messages were stored with different field names)
    var filter = Builders<BsonDocument>.Filter.Or(
        Builders<BsonDocument>.Filter.Eq("messageId", body.MessageId),
        Builders<BsonDocument>.Filter.Eq("id", body.MessageId)
    );
    
    // First check if message exists
    var existingMessage = await collection.Find(filter).FirstOrDefaultAsync();
    if (existingMessage == null)
    {
        Console.WriteLine($"[Program] Delete message: Message with MessageId={body.MessageId} not found in database");
        // Try to find any message with similar ID for debugging
        var allMessages = await collection.Find(Builders<BsonDocument>.Filter.Empty).Limit(5).ToListAsync();
        Console.WriteLine($"[Program] Sample message IDs in database: {string.Join(", ", allMessages.Select(m => m.GetValue("messageId", "N/A").ToString()).Take(5))}");
    }
    
    var result = await collection.DeleteOneAsync(filter);
    Console.WriteLine($"[Program] Delete message result: DeletedCount={result.DeletedCount}, MessageId={body.MessageId}");
    
    return result.DeletedCount > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound(new { deleted = false, messageId = body.MessageId });
});

var defaultWallpapers = new List<WallpaperCatalogItem>
{
    new("abstract-aurora","Abstract Aurora","Flowing teal/purple gradients; great for dark mode.","abstract",true,true,"/wallpapers/abstract-aurora.svg"),
    new("minimal-mist","Minimal Mist","Soft off‑white with subtle grain; ideal for light mode.","minimal",false,true,"/wallpapers/minimal-mist.svg"),
    new("midnight-grid","Midnight Grid","Faint geometric grid over deep navy.","geometric",true,false,"/wallpapers/midnight-grid.svg"),
    new("desert-dunes","Desert Dunes","Warm sandy waves; cozy neutral.","nature",true,true,"/wallpapers/desert-dunes.svg"),
    new("neon-shapes","Neon Shapes","Playful geometric shapes with neon accents.","geometric",true,true,"/wallpapers/neon-shapes.svg"),
    new("forest-blur","Forest Blur","Defocused green woodland bokeh; calming.","nature",true,true,"/wallpapers/forest-blur.svg"),
    new("carbon-fiber","Carbon Fiber","Textured dark diagonal weave; industrial.","abstract",true,false,"/wallpapers/carbon-fiber.svg"),
    new("pastel-fade","Pastel Fade","Pastel rainbow gradient; cheerful.","abstract",false,true,"/wallpapers/pastel-fade.svg"),
    new("ocean-lowpoly","Ocean Low‑Poly","Polygonal sea tones; crisp depth.","geometric",true,true,"/wallpapers/ocean-lowpoly.svg"),
    new("paper-texture","Paper Texture","Light tactile paper; classic readability.","minimal",false,true,"/wallpapers/paper-texture.svg")
};

// List all wallpapers (default + custom)
app.MapGet("/api/chat/wallpapers", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string? userId) =>
{
    var allWallpapers = new List<WallpaperCatalogItem>(defaultWallpapers);
    
    // Add custom wallpapers if userId is provided
    if (!string.IsNullOrWhiteSpace(userId))
    {
        var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
        var db = mongoClient.GetDatabase(dbName);
        var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

        var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
        var customWallpapers = await wallpapers.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("uploadedAt"))
            .ToListAsync();

        var customWallpaperItems = customWallpapers.Select(doc => new WallpaperCatalogItem(
            doc["id"].AsString,
            doc["name"].AsString,
            doc["description"].AsString,
            doc["category"].AsString,
            doc["supportsDark"].AsBoolean,
            doc["supportsLight"].AsBoolean,
            doc["previewUrl"].AsString
        )).ToList();

        allWallpapers.AddRange(customWallpaperItems);
    }

    return Results.Ok(allWallpapers);
});

// Preferences storage in Mongo: collection chat_wallpaper_prefs
app.MapGet("/api/chat/preferences/wallpaper", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string me,
    [FromQuery] string peer) =>
{
    if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(peer))
    {
        return Results.BadRequest(new { message = "me and peer are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var prefs = db.GetCollection<BsonDocument>("chat_wallpaper_prefs");
    var key = string.CompareOrdinal(me, peer) < 0 ? $"{me}:{peer}" : $"{peer}:{me}";
    var doc = await prefs.Find(Builders<BsonDocument>.Filter.Eq("key", key)).FirstOrDefaultAsync();
    if (doc == null)
    {
        return Results.Ok(new { wallpaperId = (string?)null, opacity = 0.25, wallpaperUrl = (string?)null });
    }
    var wid = doc.GetValue("wallpaperId", BsonNull.Value).IsBsonNull ? null : doc["wallpaperId"].AsString;
    var opacity = doc.GetValue("opacity", 0.25).ToDouble();
    var wallpaperUrl = doc.GetValue("wallpaperUrl", BsonNull.Value).IsBsonNull ? null : doc["wallpaperUrl"].AsString;
    
    // If no URL is stored but we have a wallpaperId, try to resolve it
    if (string.IsNullOrEmpty(wallpaperUrl) && !string.IsNullOrEmpty(wid))
    {
        // Check if it's a custom wallpaper
        if (wid.StartsWith("custom_"))
        {
            var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");
            var wallpaperDoc = await wallpapers.Find(Builders<BsonDocument>.Filter.Eq("id", wid)).FirstOrDefaultAsync();
            if (wallpaperDoc != null)
            {
                wallpaperUrl = wallpaperDoc["previewUrl"].AsString;
            }
        }
        else
        {
            // It's a default wallpaper, construct the URL
            var defaultWallpaper = defaultWallpapers.FirstOrDefault(w => w.id == wid);
            if (defaultWallpaper != null)
            {
                wallpaperUrl = defaultWallpaper.previewUrl;
            }
        }
    }
    
    return Results.Ok(new { wallpaperId = wid, opacity, wallpaperUrl });
});

app.MapPost("/api/chat/preferences/wallpaper", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] SaveWallpaperPref body) =>
{
    if (string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer))
    {
        return Results.BadRequest(new { saved = false, message = "me and peer are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var prefs = db.GetCollection<BsonDocument>("chat_wallpaper_prefs");
    var key = string.CompareOrdinal(body.me, body.peer) < 0 ? $"{body.me}:{body.peer}" : $"{body.peer}:{body.me}";

    if (body.wallpaperId == null)
    {
        await prefs.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("key", key));
        return Results.Ok(new { saved = true });
    }

    // Resolve wallpaper URL if not provided
    var wallpaperUrl = body.wallpaperUrl;
    if (string.IsNullOrEmpty(wallpaperUrl))
    {
        if (body.wallpaperId.StartsWith("custom_"))
        {
            var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");
            var wallpaperDoc = await wallpapers.Find(Builders<BsonDocument>.Filter.Eq("id", body.wallpaperId)).FirstOrDefaultAsync();
            if (wallpaperDoc != null)
            {
                wallpaperUrl = wallpaperDoc["previewUrl"].AsString;
            }
        }
        else
        {
            var defaultWallpaper = defaultWallpapers.FirstOrDefault(w => w.id == body.wallpaperId);
            if (defaultWallpaper != null)
            {
                wallpaperUrl = defaultWallpaper.previewUrl;
            }
        }
    }

    var update = Builders<BsonDocument>.Update
        .Set("key", key)
        .Set("wallpaperId", body.wallpaperId)
        .Set("opacity", Math.Clamp(body.opacity ?? 0.25, 0, 1))
        .Set("wallpaperUrl", wallpaperUrl ?? "");
    await prefs.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Update.Combine(update),
        new UpdateOptions { IsUpsert = true });
    return Results.Ok(new { saved = true });
});

// Media upload endpoint → Cloudinary
app.MapPost("/api/chat/upload-media", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Form content required" });
    }
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file provided" });
    }
    var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
    var isImage = contentType.StartsWith("image/");
    var isVideo = contentType.StartsWith("video/");
    var isAudio = contentType.StartsWith("audio/");
    if (!isImage && !isVideo && !isAudio)
    {
        return Results.BadRequest(new { message = "Only images, videos, or audio files are allowed" });
    }
    try
    {
        // Prefer images via Cloudinary image API; videos and audio via Cloudinary video API
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        // Fallback: support connection URL in appsettings: Cloudinary:Url = cloudinary://<apiKey>:<apiSecret>@<cloudName>
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            return Results.Problem("Cloudinary is not configured", statusCode: 500);
        }
        var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);
        if (isImage)
        {
            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream()),
                Transformation = new CloudinaryDotNet.Transformation().Width(1600).Crop("limit").Quality(80)
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "image" });
        }
        else if (isVideo)
        {
            var uploadParams = new CloudinaryDotNet.Actions.VideoUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream())
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "video" });
        }
        else // isAudio - Cloudinary handles audio files via VideoUploadParams
        {
            var uploadParams = new CloudinaryDotNet.Actions.VideoUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream())
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            var audioUrl = res.SecureUrl.ToString();
            // Return both url and audioUrl for compatibility
            return Results.Ok(new { url = audioUrl, audioUrl = audioUrl, mediaType = "audio" });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// === Custom Wallpaper Upload and Management ===
// Upload custom wallpaper to Cloudinary
app.MapPost("/api/chat/upload-wallpaper", async (
    HttpRequest request,
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Form content required" });
    }
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    var userId = form["userId"].FirstOrDefault();
    var name = form["name"].FirstOrDefault();
    var description = form["description"].FirstOrDefault();
    var category = form["category"].FirstOrDefault();
    var supportsDark = bool.TryParse(form["supportsDark"].FirstOrDefault(), out var dark) && dark;
    var supportsLight = bool.TryParse(form["supportsLight"].FirstOrDefault(), out var light) && light;

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file provided" });
    }
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required" });
    }

    var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
    if (!contentType.StartsWith("image/"))
    {
        return Results.BadRequest(new { message = "Only images are allowed for wallpapers" });
    }

    // Validate file size (max 10MB)
    if (file.Length > 10 * 1024 * 1024)
    {
        return Results.BadRequest(new { message = "File size must be less than 10MB" });
    }

    try
    {
        // Upload to Cloudinary
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            return Results.Problem("Cloudinary is not configured", statusCode: 500);
        }

        var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);

        var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
        {
            File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream()),
            PublicId = $"wallpapers/custom/{userId}_{Guid.NewGuid()}",
            Transformation = new CloudinaryDotNet.Transformation().Width(1920).Height(1080).Crop("fill").Quality(85),
            Folder = "wallpapers/custom"
        };

        var res = await cloudinary.UploadAsync(uploadParams);
        if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);

        // Save wallpaper metadata to MongoDB
        var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
        var db = mongoClient.GetDatabase(dbName);
        var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

        var wallpaperId = $"custom_{Guid.NewGuid()}";
        var wallpaperDoc = new BsonDocument
        {
            { "id", wallpaperId },
            { "name", name ?? "Custom Wallpaper" },
            { "description", description ?? "User uploaded wallpaper" },
            { "category", category ?? "custom" },
            { "supportsDark", supportsDark },
            { "supportsLight", supportsLight },
            { "previewUrl", res.SecureUrl.ToString() },
            { "cloudinaryPublicId", res.PublicId },
            { "userId", userId },
            { "uploadedAt", DateTime.UtcNow },
            { "isCustom", true }
        };

        await wallpapers.InsertOneAsync(wallpaperDoc);

        return Results.Ok(new { 
            wallpaperId, 
            url = res.SecureUrl.ToString(),
            name = name ?? "Custom Wallpaper",
            description = description ?? "User uploaded wallpaper",
            category = category ?? "custom",
            supportsDark,
            supportsLight
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// Get custom wallpapers for a user
app.MapGet("/api/chat/custom-wallpapers", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userId) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required" });
    }

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

    var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
    var customWallpapers = await wallpapers.Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Descending("uploadedAt"))
        .ToListAsync();

    var result = customWallpapers.Select(doc => new WallpaperCatalogItem(
        doc["id"].AsString,
        doc["name"].AsString,
        doc["description"].AsString,
        doc["category"].AsString,
        doc["supportsDark"].AsBoolean,
        doc["supportsLight"].AsBoolean,
        doc["previewUrl"].AsString
    )).ToList();

    return Results.Ok(result);
});

// Delete custom wallpaper
app.MapDelete("/api/chat/custom-wallpapers/{wallpaperId}", async (
    string wallpaperId,
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userId) =>
{
    if (string.IsNullOrWhiteSpace(wallpaperId) || string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "wallpaperId and userId are required" });
    }

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

    // Find the wallpaper and verify ownership
    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("id", wallpaperId),
        Builders<BsonDocument>.Filter.Eq("userId", userId)
    );

    var wallpaper = await wallpapers.Find(filter).FirstOrDefaultAsync();
    if (wallpaper == null)
    {
        return Results.NotFound(new { message = "Wallpaper not found or access denied" });
    }

    try
    {
        // Delete from Cloudinary
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }

        if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new CloudinaryDotNet.Cloudinary(account);

            var deletionParams = new CloudinaryDotNet.Actions.DeletionParams(wallpaper["cloudinaryPublicId"].AsString);
            await cloudinary.DestroyAsync(deletionParams);
        }

        // Delete from MongoDB
        await wallpapers.DeleteOneAsync(filter);

        return Results.Ok(new { deleted = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// === Chat pins persistence (offline-safe) ===
app.MapGet("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string me,
    [FromQuery] string peer) =>
{
    if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(peer))
    {
        return Results.BadRequest(new { message = "me and peer are required" });
    }
    var a = me;
    var b = peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var cursor = await pins.Find(Builders<BsonDocument>.Filter.Eq("key", key))
        .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
        .ToListAsync();

    var items = cursor.Select((d, idx) =>
    {
        var createdAtVal = d.GetValue("createdAt", BsonNull.Value);
        DateTimeOffset createdAtValue;
        if (createdAtVal is BsonDateTime bdt)
        {
            createdAtValue = bdt.ToUniversalTime();
        }
        else if (createdAtVal.IsString && DateTimeOffset.TryParse(createdAtVal.AsString, out var parsed))
        {
            createdAtValue = parsed.ToUniversalTime();
        }
        else
        {
            createdAtValue = DateTimeOffset.UtcNow;
        }

        return new
        {
            id = d.GetValue("_id", BsonNull.Value).IsBsonNull ? $"local-{idx}" : d["_id"].ToString(),
            conversationId = key,
            messageId = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
            scope = d.GetValue("scope", "global").AsString,
            pinnedByUserId = d.GetValue("pinnedByUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["pinnedByUserId"].AsString,
            createdAt = createdAtValue
        };
    });

    return Results.Ok(items);
});

app.MapPost("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] PinRequest body) =>
{
    if (body == null || string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer) || string.IsNullOrWhiteSpace(body.messageId))
    {
        return Results.BadRequest(new { pinned = false, message = "me, peer, and messageId are required" });
    }
    var a = body.me;
    var b = body.peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Filter.Eq("messageId", body.messageId)
    );
    var update = Builders<BsonDocument>.Update
        .SetOnInsert("key", key)
        .SetOnInsert("messageId", body.messageId)
        .Set("scope", string.IsNullOrWhiteSpace(body.scope) ? "global" : body.scope)
        .Set("pinnedByUserId", body.me)
        .SetOnInsert("createdAt", DateTimeOffset.UtcNow);
    var options = new UpdateOptions { IsUpsert = true };
    await pins.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Combine(update), options);

    return Results.Ok(new { pinned = true, id = body.messageId });
});

app.MapDelete("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] PinRequest body) =>
{
    if (body == null || string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer) || string.IsNullOrWhiteSpace(body.messageId))
    {
        return Results.BadRequest(new { unpinned = false, message = "me, peer, and messageId are required" });
    }
    var a = body.me;
    var b = body.peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Filter.Eq("messageId", body.messageId)
    );
    await pins.DeleteOneAsync(filter);

    return Results.Ok(new { unpinned = true });
});
app.Run();

// ===== Types (must follow top-level statements) =====
public record WallpaperCatalogItem(
    string id,
    string name,
    string description,
    string category,
    bool supportsDark,
    bool supportsLight,
    string previewUrl
);
public record SaveWallpaperPref(string me, string peer, string? wallpaperId, double? opacity, string? wallpaperUrl = null);
public record EditRequest(string MessageId, string NewText);
public record DeleteRequest(string MessageId);
public record PinRequest(string me, string peer, string messageId, string scope);
