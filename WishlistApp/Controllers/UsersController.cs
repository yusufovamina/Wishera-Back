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
    public class UsersController : ControllerBase
    {
        private readonly IUserServiceClient _userServiceClient;

        public UsersController(IUserServiceClient userServiceClient)
        {
            _userServiceClient = userServiceClient;
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
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
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
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
} 