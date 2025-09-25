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
        options.WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001",
            "http://127.0.0.1:3000",
            "http://127.0.0.1:3001")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
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

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

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
        DateTimeOffset sentAtValue;
        if (sentVal is BsonDateTime bdt)
        {
            sentAtValue = bdt.ToUniversalTime();
        }
        else if (sentVal.IsString && DateTimeOffset.TryParse(sentVal.AsString, out var parsed))
        {
            sentAtValue = parsed.ToUniversalTime();
        }
        else
        {
            sentAtValue = DateTimeOffset.MinValue;
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

        return new
        {
            id = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
            conversationId = d.GetValue("conversationId", BsonNull.Value).IsBsonNull ? string.Empty : d["conversationId"].AsString,
            senderUserId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
            recipientUserId = d.GetValue("recipientUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["recipientUserId"].AsString,
            text = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
            sentAt = sentAtValue,
            reactions = reactions
        };
    });

    return Results.Ok(items);
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
        return Results.BadRequest(new { message = "messageId is required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);
    var filter = Builders<BsonDocument>.Filter.Eq("messageId", body.MessageId);
    var result = await collection.DeleteOneAsync(filter);
    return result.DeletedCount > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound(new { deleted = false });
});
app.Run();

public record EditRequest(string MessageId, string NewText);
public record DeleteRequest(string MessageId);
