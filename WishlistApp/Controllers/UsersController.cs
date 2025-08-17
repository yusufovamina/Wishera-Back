using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WishlistApp.DTO;
using WishlistApp.Services;

namespace WishlistApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        private string GetCurrentUserId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"Extracted UserId from JWT: '{userId}'");
            return userId;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserProfileDTO>> GetUserProfile(string id)
        {
            try
            {
                var profile = await _userService.GetUserProfileAsync(id, GetCurrentUserId());
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
            try
            {
                var profile = await _userService.UpdateUserProfileAsync(GetCurrentUserId(), updateDto);
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
            try
            {
                var avatarUrl = await _userService.UpdateAvatarAsync(GetCurrentUserId(), file);
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
            try
            {
                var result = await _userService.FollowUserAsync(GetCurrentUserId(), id);
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
            try
            {
                var result = await _userService.UnfollowUserAsync(GetCurrentUserId(), id);
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
            try
            {
                var users = await _userService.SearchUsersAsync(query, GetCurrentUserId(), page, pageSize);
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
            try
            {
                var followers = await _userService.GetFollowersAsync(id, GetCurrentUserId(), page, pageSize);
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
            try
            {
                var following = await _userService.GetFollowingAsync(id, GetCurrentUserId(), page, pageSize);
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