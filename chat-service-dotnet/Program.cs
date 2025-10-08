using Microsoft.AspNetCore.Mvc;
using ChatService.Api.Hubs;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Caching.Distributed;
using ChatService.Api;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Bind Stream configuration
builder.Services.Configure<ChatService.Api.StreamSettings>(
    builder.Configuration.GetSection("Stream"));

// Register Stream client factory
builder.Services.AddSingleton<ChatService.Api.IStreamClientFactory, ChatService.Api.StreamClientFactoryWrapper>();

// Register chat service (Stream REST API helpers remain available)
builder.Services.AddScoped<ChatService.Api.IChatService, ChatService.Api.StreamChatService>();

// Redis cache (for preferences)
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
    ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "wishera:";
});

// In-memory chat store for SignalR hub
// Replace in-memory store with MongoDB-backed store
builder.Services.AddSingleton<ChatService.Api.Hubs.IChatStore, ChatService.Api.Services.MongoChatStore>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyMethod()
            .AllowAnyHeader()
            .SetIsOriginAllowed(_ => true)
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll"); // Use the CORS policy

// Enable raw WebSockets for custom chat protocol
app.UseWebSockets();
app.UseStaticFiles();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Preferences: wallpaper
app.MapGet("/api/chat/preferences/wallpaper", async (
    [FromServices] IDistributedCache cache,
    [FromQuery] string me,
    [FromQuery] string peer) =>
{
    if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(peer))
    {
        return Results.BadRequest(new { message = "me and peer are required" });
    }
    var key = $"chat:pref:wallpaper:{me}:{peer}";
    var url = await cache.GetStringAsync(key);
    return Results.Ok(new { url = string.IsNullOrEmpty(url) ? null : url });
});

app.MapPost("/api/chat/preferences/wallpaper", async (
    [FromServices] IDistributedCache cache,
    [FromBody] WallpaperPref body) =>
{
    if (string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer))
    {
        return Results.BadRequest(new { saved = false, message = "me and peer are required" });
    }
    var key = $"chat:pref:wallpaper:{body.me}:{body.peer}";
    if (string.IsNullOrWhiteSpace(body.url))
    {
        await cache.RemoveAsync(key);
        return Results.Ok(new { saved = true });
    }
    await cache.SetStringAsync(key, body.url);
    return Results.Ok(new { saved = true });
});

// SignalR hub endpoints
app.MapHub<ChatService.Api.Hubs.ChatHub>("/hubs/chat");

// Raw WebSocket endpoint (non-SignalR)
app.Map("/ws/chat", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var store = context.RequestServices.GetRequiredService<ChatService.Api.Hubs.IChatStore>();
        await ChatService.Api.WebSockets.ChatWebSocketHandler.HandleAsync(context, webSocket, store);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

// History & search REST endpoints for convenience
app.MapGet("/api/chat/history", async (
    [FromServices] ChatService.Api.Hubs.IChatStore store,
    [FromQuery] string userA,
    [FromQuery] string userB,
    [FromQuery] int page = 0,
    [FromQuery] int pageSize = 20) =>
{
    var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userA, userB);
    var items = await store.GetHistoryAsync(conversationId, page, pageSize);
    return Results.Ok(items);
});

app.MapGet("/api/chat/search", async (
    [FromServices] ChatService.Api.Hubs.IChatStore store,
    [FromQuery] string userA,
    [FromQuery] string userB,
    [FromQuery] string? q,
    [FromQuery] DateTimeOffset? from,
    [FromQuery] DateTimeOffset? to,
    [FromQuery] int page = 0,
    [FromQuery] int pageSize = 20) =>
{
    var conversationId = ChatService.Api.Hubs.ChatHub.GetConversationId(userA, userB);
    var items = await store.SearchAsync(conversationId, q, from, to, page, pageSize);
    return Results.Ok(items);
});

// Media upload endpoint (Cloudinary)
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
    if (!isImage && !isVideo)
    {
        return Results.BadRequest(new { message = "Only images or videos are allowed" });
    }
    try
    {
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
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
        else
        {
            var uploadParams = new CloudinaryDotNet.Actions.VideoUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream())
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "video" });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

app.Run();

namespace ChatService.Api
{
    using Microsoft.AspNetCore.Mvc;
    using StreamChat.Clients;
    using StreamChat.Models;

    public record WallpaperPref(string me, string peer, string? url);

    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        public record TokenRequest(string userId, int? expiresInMinutes);
        public record UpsertUserRequest(string userId, Dictionary<string, object>? customData);
        public record ChannelRequest(string channelType, string? channelId, string createdByUserId, string[] memberIds, Dictionary<string, object>? customData);
        public record MessageRequest(string channelType, string channelId, string senderUserId, string text, Dictionary<string, object>? customData);

        [HttpPost("token")]
        public ActionResult<object> CreateToken([FromBody] TokenRequest request)
        {
            var expires = request.expiresInMinutes.HasValue ? DateTimeOffset.UtcNow.AddMinutes(request.expiresInMinutes.Value) : null as DateTimeOffset?;
            var token = _chatService.CreateUserToken(request.userId, expires);
            return Ok(new { token });
        }

        [HttpPost("user")]
        public async Task<IActionResult> UpsertUser([FromBody] UpsertUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.userId))
            {
                return BadRequest(new { message = "User ID is required." });
            }

            await _chatService.UpsertUserAsync(request.userId, request.customData);
            return NoContent();
        }

        [HttpPost("channel")]
        public async Task<ActionResult<object>> CreateOrJoinChannel([FromBody] ChannelRequest request)
        {
            var actualChannelId = request.channelId ?? Guid.NewGuid().ToString();

            await _chatService.UpsertUserAsync(request.createdByUserId);
            foreach (var member in request.memberIds)
            {
                await _chatService.UpsertUserAsync(member);
            }
            await _chatService.CreateOrJoinChannelAsync(request.channelType, actualChannelId, request.createdByUserId, request.memberIds, request.customData);
            return Ok(new { channelId = actualChannelId }); // Return the generated ID
        }

        [HttpPost("message")]
        public async Task<ActionResult<object>> SendMessage([FromBody] MessageRequest request)
        {
            if (string.IsNullOrEmpty(request.senderUserId))
            {
                return BadRequest(new { message = "Sender User ID cannot be null or empty." });
            }

            // Ensure the sender user exists in Stream Chat before sending message
            await _chatService.UpsertUserAsync(request.senderUserId);

            var messageId = await _chatService.SendMessageAsync(request.channelType, request.channelId, request.senderUserId, request.text, request.customData);
            return Ok(new { messageId });
        }
    }

    public class StreamSettings
    {
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
    }

    public interface IStreamClientFactory
    {
        StreamChat.Clients.IUserClient GetUserClient();
        StreamChat.Clients.IChannelClient GetChannelClient();
        StreamChat.Clients.IMessageClient GetMessageClient();
    }

    public class StreamClientFactoryWrapper : IStreamClientFactory
    {
        private readonly StreamChat.Clients.StreamClientFactory _factory;

        public StreamClientFactoryWrapper(IConfiguration configuration)
        {
            var apiKey = configuration["Stream:ApiKey"]
                ?? throw new InvalidOperationException("Stream ApiKey is not configured");
            var apiSecret = configuration["Stream:ApiSecret"]
                ?? throw new InvalidOperationException("Stream ApiSecret is not configured");
            _factory = new StreamChat.Clients.StreamClientFactory(apiKey, apiSecret);
        }

        public StreamChat.Clients.IUserClient GetUserClient() => _factory.GetUserClient();
        public StreamChat.Clients.IChannelClient GetChannelClient() => _factory.GetChannelClient();
        public StreamChat.Clients.IMessageClient GetMessageClient() => _factory.GetMessageClient();
    }

    public interface IChatService
    {
        string CreateUserToken(string userId, DateTimeOffset? expiration = null);
        Task UpsertUserAsync(string userId, Dictionary<string, object>? customData = null);
        Task CreateOrJoinChannelAsync(string channelType, string? channelId, string createdByUserId, IEnumerable<string> memberIds, Dictionary<string, object>? customData = null);
        Task<string> SendMessageAsync(string channelType, string channelId, string senderUserId, string text, Dictionary<string, object>? customData = null);
    }

    public class StreamChatService : IChatService
    {
        private readonly IStreamClientFactory _clientFactory;

        public StreamChatService(IStreamClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public string CreateUserToken(string userId, DateTimeOffset? expiration = null)
        {
            var token = expiration.HasValue
                ? _clientFactory.GetUserClient().CreateToken(userId, expiration)
                : _clientFactory.GetUserClient().CreateToken(userId);
            return token;
        }

        public async Task UpsertUserAsync(string userId, Dictionary<string, object>? customData = null)
        {
            var user = new UserRequest { Id = userId };
            if (customData != null)
            {
                foreach (var kv in customData)
                {
                    user.SetData(kv.Key, kv.Value);
                }
            }
            await _clientFactory.GetUserClient().UpsertAsync(user);
        }

        public async Task CreateOrJoinChannelAsync(string channelType, string? channelId, string createdByUserId, IEnumerable<string> memberIds, Dictionary<string, object>? customData = null)
        {
            var channelData = new StreamChat.Models.ChannelRequest
            {
                // Removed: Id = channelId, // ChannelId is passed as a separate argument to GetOrCreateAsync
                CreatedBy = new StreamChat.Models.UserRequest { Id = createdByUserId }
            };
            if (customData != null)
            {
                foreach (var kv in customData)
                {
                    channelData.SetData(kv.Key, kv.Value);
                }
            }

            await _clientFactory.GetChannelClient().GetOrCreateAsync(channelType, channelId, new StreamChat.Models.ChannelGetRequest
            {
                Data = channelData
            });

            if (memberIds.Any())
            {
                await _clientFactory.GetChannelClient().AddMembersAsync(channelType, channelId, memberIds.ToArray());
            }
        }

        public async Task<string> SendMessageAsync(string channelType, string channelId, string senderUserId, string text, Dictionary<string, object>? customData = null)
        {
            if (string.IsNullOrEmpty(senderUserId))
            {
                throw new ArgumentNullException(nameof(senderUserId), "Sender User ID cannot be null or empty when sending a message.");
            }

            var message = new StreamChat.Models.MessageRequest
            {
                Id = Guid.NewGuid().ToString(), // Assign a unique ID to the message
                Text = text,
                User = new StreamChat.Models.UserRequest { Id = senderUserId }
            };
            if (customData != null)
            {
                foreach (var kv in customData)
                {
                    message.SetData(kv.Key, kv.Value);
                }
            }

            var response = await _clientFactory.GetMessageClient().SendMessageAsync(channelType, channelId, message, senderUserId);
            return response.Message.Id;
        }
    }
}
