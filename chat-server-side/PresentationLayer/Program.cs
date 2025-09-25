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

    var items = cursor.Select(d => new
    {
        id = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
        conversationId = d.GetValue("conversationId", BsonNull.Value).IsBsonNull ? string.Empty : d["conversationId"].AsString,
        senderUserId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
        recipientUserId = d.GetValue("recipientUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["recipientUserId"].AsString,
        text = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
        sentAt = d.GetValue("sentAt", BsonNull.Value).IsBsonNull ? DateTimeOffset.MinValue : d["sentAt"].ToUniversalTime(),
    });

    return Results.Ok(items);
});

app.Run();
