using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WisheraApp.DTO;
using WisheraApp.Services;

namespace WisheraApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/users")] // Add lowercase route for compatibility
    public class UsersController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public UsersController(IUserServiceClient userServiceClient, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _userServiceClient = userServiceClient;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        private string GetAuthToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                return string.Empty;
            }
            return authHeader.Substring(7); // Remove "Bearer " prefix
        }

        private string? GetCurrentUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"Extracted UserId from JWT: '{userId}'");
            return userId;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserProfileDTO>> GetUserProfile(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var profile = await _userServiceClient.GetUserProfileAsync(id, currentUserId);
                return Ok(profile);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"GetUserProfile timeout, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(id, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"GetUserProfile RabbitMQ not available, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(id, ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserProfile error, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(id, ex.Message);
            }
        }

        private async Task<ActionResult<UserProfileDTO>> TryHttpFallback(string userId, string originalError)
        {
            try
            {
                return await GetUserProfileViaHttpAsync(userId);
            }
            catch (Exception httpEx)
            {
                Console.WriteLine($"HTTP fallback failed: {httpEx.Message}");
                return StatusCode(502, new { message = "Service temporarily unavailable. The user-service may not be running or is not accessible.", error = httpEx.Message, originalError = originalError });
            }
        }

        private async Task<ActionResult<UserProfileDTO>> GetUserProfileViaHttpAsync(string userId)
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? _configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";
            
            var token = GetAuthToken();
            var httpClient = _httpClientFactory.CreateClient("UserService");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Users/{userId}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            
            var response = await httpClient.SendAsync(request);
            
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = "User not found" });
            }
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Unauthorized(new { message = "Authentication failed" });
            }
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var profile = System.Text.Json.JsonSerializer.Deserialize<UserProfileDTO>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (profile == null)
            {
                return StatusCode(500, new { message = "Failed to deserialize user profile" });
            }
            
            return Ok(profile);
        }

        [HttpGet("profile")]
        public async Task<ActionResult<UserProfileDTO>> GetCurrentUserProfile()
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var profile = await _userServiceClient.GetUserProfileAsync(currentUserId, currentUserId);
                return Ok(profile);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"GetCurrentUserProfile timeout, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(currentUserId, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"GetCurrentUserProfile RabbitMQ not available, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(currentUserId, ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCurrentUserProfile error, trying HTTP fallback: {ex.Message}");
                return await TryHttpFallback(currentUserId, ex.Message);
            }
        }

        [HttpPut("profile")]
        public async Task<ActionResult<UserProfileDTO>> UpdateProfile(UpdateUserProfileDTO updateDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var profile = await _userServiceClient.UpdateUserProfileAsync(currentUserId, updateDto);
                return Ok(profile);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("avatar")]
        public async Task<ActionResult<string>> UpdateAvatar(IFormFile file)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var avatarUrl = await _userServiceClient.UpdateAvatarAsync(currentUserId, file);
                return Ok(new { avatarUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("follow/{id}")]
        public async Task<ActionResult<bool>> FollowUser(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _userServiceClient.FollowUserAsync(currentUserId, id);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("unfollow/{id}")]
        public async Task<ActionResult<bool>> UnfollowUser(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _userServiceClient.UnfollowUserAsync(currentUserId, id);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<UserSearchDTO>>> SearchUsers(
            [FromQuery] string query,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var users = await _userServiceClient.SearchUsersAsync(query, currentUserId, page, pageSize);
                return Ok(users);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"SearchUsers timeout, trying HTTP fallback: {ex.Message}");
                return await TryHttpSearchFallback(query, page, pageSize, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"SearchUsers RabbitMQ not available, trying HTTP fallback: {ex.Message}");
                return await TryHttpSearchFallback(query, page, pageSize, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SearchUsers error, trying HTTP fallback: {ex.Message}");
                return await TryHttpSearchFallback(query, page, pageSize, ex.Message);
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> TryHttpSearchFallback(string query, int page, int pageSize, string originalError)
        {
            try
            {
                return await SearchUsersViaHttpAsync(query, page, pageSize);
            }
            catch (Exception httpEx)
            {
                Console.WriteLine($"HTTP fallback failed: {httpEx.Message}");
                return StatusCode(502, new { message = "Service temporarily unavailable.", error = httpEx.Message, originalError = originalError });
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> SearchUsersViaHttpAsync(string query, int page, int pageSize)
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? _configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";

            var token = GetAuthToken();
            var httpClient = _httpClientFactory.CreateClient("UserService");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Users/search?query={Uri.EscapeDataString(query)}&page={page}&pageSize={pageSize}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { message = $"Error from user-service: {response.ReasonPhrase}", details = errorContent });
            }

            var content = await response.Content.ReadAsStringAsync();
            var users = System.Text.Json.JsonSerializer.Deserialize<List<UserSearchDTO>>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(users);
        }

        [HttpGet("{id}/followers")]
        public async Task<ActionResult<List<UserSearchDTO>>> GetFollowers(
            string id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var followers = await _userServiceClient.GetFollowersAsync(id, currentUserId, page, pageSize);
                return Ok(followers);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"GetFollowers timeout, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowersFallback(id, page, pageSize, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"GetFollowers RabbitMQ not available, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowersFallback(id, page, pageSize, ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFollowers error, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowersFallback(id, page, pageSize, ex.Message);
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> TryHttpFollowersFallback(string userId, int page, int pageSize, string originalError)
        {
            try
            {
                return await GetFollowersViaHttpAsync(userId, page, pageSize);
            }
            catch (Exception httpEx)
            {
                Console.WriteLine($"HTTP fallback failed: {httpEx.Message}");
                return StatusCode(502, new { message = "Service temporarily unavailable.", error = httpEx.Message, originalError = originalError });
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> GetFollowersViaHttpAsync(string userId, int page, int pageSize)
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? _configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";

            var token = GetAuthToken();
            var httpClient = _httpClientFactory.CreateClient("UserService");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Users/{userId}/followers?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = "User not found" });
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { message = $"Error from user-service: {response.ReasonPhrase}", details = errorContent });
            }

            var content = await response.Content.ReadAsStringAsync();
            var followers = System.Text.Json.JsonSerializer.Deserialize<List<UserSearchDTO>>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(followers);
        }

        [HttpGet("{id}/following")]
        public async Task<ActionResult<List<UserSearchDTO>>> GetFollowing(
            string id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var following = await _userServiceClient.GetFollowingAsync(id, currentUserId, page, pageSize);
                return Ok(following);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"GetFollowing timeout, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowingFallback(id, page, pageSize, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"GetFollowing RabbitMQ not available, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowingFallback(id, page, pageSize, ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFollowing error, trying HTTP fallback: {ex.Message}");
                return await TryHttpFollowingFallback(id, page, pageSize, ex.Message);
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> TryHttpFollowingFallback(string userId, int page, int pageSize, string originalError)
        {
            try
            {
                return await GetFollowingViaHttpAsync(userId, page, pageSize);
            }
            catch (Exception httpEx)
            {
                Console.WriteLine($"HTTP fallback failed: {httpEx.Message}");
                return StatusCode(502, new { message = "Service temporarily unavailable.", error = httpEx.Message, originalError = originalError });
            }
        }
        
        private async Task<ActionResult<List<UserSearchDTO>>> GetFollowingViaHttpAsync(string userId, int page, int pageSize)
        {
            var userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")
                ?? _configuration["UserServiceUrl"]
                ?? "https://wishera-user-service.onrender.com";

            var token = GetAuthToken();
            var httpClient = _httpClientFactory.CreateClient("UserService");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{userServiceUrl}/api/Users/{userId}/following?page={page}&pageSize={pageSize}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var response = await httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = "User not found" });
            }
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { message = $"Error from user-service: {response.ReasonPhrase}", details = errorContent });
            }

            var content = await response.Content.ReadAsStringAsync();
            var following = System.Text.Json.JsonSerializer.Deserialize<List<UserSearchDTO>>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(following);
        }
    }
} 