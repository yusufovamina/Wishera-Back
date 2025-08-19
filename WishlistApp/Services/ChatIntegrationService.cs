using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WishlistApp.Services
{
    public class ChatIntegrationService : IChatIntegrationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ChatIntegrationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _httpClient.BaseAddress = new Uri(_configuration["ChatServiceUrl"] ?? throw new InvalidOperationException("ChatServiceUrl is not configured"));
        }

        public async Task<string> GetUserTokenAsync(string userId)
        {
            var requestBody = new { userId };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/chat/token", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(responseContent))
            {
                return doc.RootElement.GetProperty("token").GetString()!;
            }
        }

        public async Task UpsertUserAsync(string userId)
        {
            var requestBody = new { userId };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/chat/user", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task CreateChannelAsync(string channelType, string channelId, string createdByUserId, IEnumerable<string> memberIds)
        {
            var requestBody = new { channelType, channelId, createdByUserId, memberIds };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/chat/channel", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task SendMessageAsync(string channelType, string channelId, string senderUserId, string text)
        {
            var requestBody = new { channelType, channelId, senderUserId, text };
            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/chat/message", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
