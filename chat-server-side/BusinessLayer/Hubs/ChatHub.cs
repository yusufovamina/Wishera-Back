using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace BusinessLayer.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private static readonly Dictionary<string, string> activeUsers = new();

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
                await Clients.Client(activeUsers[userId]).SendAsync("ReceiveMessage", new { senderId = sourceUserId, text = message }, username);
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

        public async Task AddUser(string userId, string connectionId)
        {
            // Upsert connection id to avoid duplicate-key exceptions on reconnects
            activeUsers[userId] = connectionId;
            await Clients.All.SendAsync("ReceiveActiveUsers", GetActiveUserIds());
        }

        public string GetConnectionId()
        {
            return Context.ConnectionId;
        }

        public List<string> GetActiveUserIds()
        {
            return activeUsers.Keys.ToList();
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.All.SendAsync("ReceiveActiveUsers", GetActiveUserIds());
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = GetConnectionId();

            var toRemove = activeUsers.FirstOrDefault(kv => kv.Value == connectionId);
            if (!toRemove.Equals(default(KeyValuePair<string, string>)))
            {
                activeUsers.Remove(toRemove.Key);
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
