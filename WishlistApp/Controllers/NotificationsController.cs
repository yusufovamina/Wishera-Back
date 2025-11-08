using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WisheraApp.Services;

namespace WisheraApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public NotificationsController(IUserServiceClient userServiceClient, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _userServiceClient = userServiceClient;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        private string GetAuthToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                throw new UnauthorizedAccessException("Authorization token is missing");
            }
            return authHeader.Substring(7); // Remove "Bearer " prefix
        }

        [HttpGet]
        public async Task<ActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                    ?? _configuration["UserServiceUrl"]
                    ?? "https://wishera-user-service.onrender.com";
                
                var token = GetAuthToken();
                var httpClient = _httpClientFactory.CreateClient("UserService");
                var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Notifications?page={page}&pageSize={pageSize}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching notifications: {ex.Message}");
                return StatusCode(502, new { message = "Unable to fetch notifications. Please try again later.", error = ex.Message });
            }
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                    ?? _configuration["UserServiceUrl"]
                    ?? "https://wishera-user-service.onrender.com";
                
                var token = GetAuthToken();
                var httpClient = _httpClientFactory.CreateClient("UserService");
                var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Notifications/unread-count");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching unread count: {ex.Message}");
                return StatusCode(502, new { message = "Unable to fetch unread count. Please try again later.", error = ex.Message });
            }
        }

        [HttpGet("birthdays")]
        public async Task<ActionResult> GetUpcomingBirthdays([FromQuery] int daysAhead = 7)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                    ?? _configuration["UserServiceUrl"]
                    ?? "https://wishera-user-service.onrender.com";
                
                var token = GetAuthToken();
                var httpClient = _httpClientFactory.CreateClient("UserService");
                var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Notifications/birthdays?daysAhead={daysAhead}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching birthdays: {ex.Message}");
                return StatusCode(502, new { message = "Unable to fetch birthdays. Please try again later.", error = ex.Message });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<ActionResult> MarkAsRead(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                    ?? _configuration["UserServiceUrl"]
                    ?? "https://wishera-user-service.onrender.com";
                
                var token = GetAuthToken();
                var httpClient = _httpClientFactory.CreateClient("UserService");
                var request = new HttpRequestMessage(HttpMethod.Put, $"{userServiceUrl}/api/Notifications/{id}/read");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking notification as read: {ex.Message}");
                return StatusCode(502, new { message = "Unable to mark notification as read. Please try again later.", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteNotification(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                    ?? _configuration["UserServiceUrl"]
                    ?? "https://wishera-user-service.onrender.com";
                
                var token = GetAuthToken();
                var httpClient = _httpClientFactory.CreateClient("UserService");
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{userServiceUrl}/api/Notifications/{id}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                var response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    return Content(content, "application/json");
                }
                
                return StatusCode((int)response.StatusCode, content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting notification: {ex.Message}");
                return StatusCode(502, new { message = "Unable to delete notification. Please try again later.", error = ex.Message });
            }
        }
    }
}

