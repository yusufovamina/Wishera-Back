using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WisheraApp.DTO;
using user_service.Models;

namespace WisheraApp.Services
{
    public interface IEventServiceClient
    {
        Task<EventDTO> CreateEventAsync(string userId, CreateEventDTO createEventDto, string token);
        Task<EventDTO?> GetEventByIdAsync(string eventId, string currentUserId, string token);
        Task<EventListDTO> GetUserEventsAsync(string userId, string currentUserId, int page, int pageSize, string token);
        Task<EventListDTO> GetInvitedEventsAsync(string userId, int page, int pageSize, string token);
        Task<EventListDTO> GetMyInvitationsAsync(string userId, int page, int pageSize, string token);
        Task<EventDTO> UpdateEventAsync(string eventId, string userId, UpdateEventDTO updateDto, string token);
        Task<bool> CancelEventAsync(string eventId, string userId, string token);
        Task<bool> DeleteEventAsync(string eventId, string userId, string token);
        Task<List<EventInvitationDTO>> GetEventInvitationsAsync(string eventId, string userId, string token);
        Task<EventInvitationDTO> RespondToInvitationAsync(string invitationId, string userId, RespondToInvitationDTO responseDto, string token);
    }

    public class EventServiceClient : IEventServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public EventServiceClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";
            _httpClient.BaseAddress = new Uri(userServiceUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<EventDTO> CreateEventAsync(string userId, CreateEventDTO createEventDto, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/Events")
            {
                Content = new StringContent(JsonSerializer.Serialize(createEventDto), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventDTO>(content)!;
        }

        public async Task<EventDTO?> GetEventByIdAsync(string eventId, string currentUserId, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Events/{eventId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventDTO>(content);
        }

        public async Task<EventListDTO> GetUserEventsAsync(string userId, string currentUserId, int page, int pageSize, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Events/my-events?page={page}&pageSize={pageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventListDTO>(content)!;
        }

        public async Task<EventListDTO> GetInvitedEventsAsync(string userId, int page, int pageSize, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Events/invited-events?page={page}&pageSize={pageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventListDTO>(content)!;
        }

        public async Task<EventListDTO> GetMyInvitationsAsync(string userId, int page, int pageSize, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Events/my-invitations?page={page}&pageSize={pageSize}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventListDTO>(content)!;
        }

        public async Task<EventDTO> UpdateEventAsync(string eventId, string userId, UpdateEventDTO updateDto, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/Events/{eventId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(updateDto), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventDTO>(content)!;
        }

        public async Task<bool> CancelEventAsync(string eventId, string userId, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/api/Events/{eventId}/cancel");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            return result.TryGetProperty("success", out var success) && success.GetBoolean();
        }

        public async Task<bool> DeleteEventAsync(string eventId, string userId, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/Events/{eventId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(content);
            return result.TryGetProperty("success", out var success) && success.GetBoolean();
        }

        public async Task<List<EventInvitationDTO>> GetEventInvitationsAsync(string eventId, string userId, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/Events/{eventId}/invitations");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<EventInvitationDTO>>(content)!;
        }

        public async Task<EventInvitationDTO> RespondToInvitationAsync(string invitationId, string userId, RespondToInvitationDTO responseDto, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/Events/invitations/{invitationId}/respond")
            {
                Content = new StringContent(JsonSerializer.Serialize(responseDto), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<EventInvitationDTO>(content)!;
        }
    }
}

