using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using user_service.Services;
using WisheraApp.DTO;

namespace user_service.Controllers
{
	[ApiController]
	[Authorize]
	[Route("api/[controller]")]
	public class UsersController : ControllerBase
	{
		private readonly IUserService _userService;

		public UsersController(IUserService userService)
		{
			_userService = userService;
		}

		private string? GetCurrentUserId() 
		{
			var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			Console.WriteLine($"GetCurrentUserId - Raw claim: '{User.FindFirst(ClaimTypes.NameIdentifier)?.Value}', Processed: '{userId}'");
			return userId;
		}

		[HttpGet("{id}")]
		public async Task<ActionResult<UserProfileDTO>> GetById(string id)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				var profile = await _userService.GetUserProfileAsync(id, currentUserId);
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
		catch (Exception ex)
		{
			Console.WriteLine($"GetById error: {ex.Message}");
			Console.WriteLine($"Stack trace: {ex.StackTrace}");
			return StatusCode(500, new { message = "An error occurred while fetching user profile", error = ex.Message });
		}
		}

		[HttpGet("profile")]
		public async Task<ActionResult<UserProfileDTO>> GetProfile()
		{
			try
			{
				var userId = GetCurrentUserId() ?? string.Empty;
				Console.WriteLine($"GetProfile - UserId: '{userId}', Length: {userId.Length}");
				var profile = await _userService.GetUserProfileAsync(userId, userId);
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
		public async Task<ActionResult<UserProfileDTO>> UpdateProfile([FromBody] UpdateUserProfileDTO updateDto)
		{
			try
			{
				var userId = GetCurrentUserId() ?? string.Empty;
				var profile = await _userService.UpdateUserProfileAsync(userId, updateDto);
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
		public async Task<ActionResult<object>> UploadAvatar(IFormFile file)
		{
			try
			{
				var userId = GetCurrentUserId() ?? string.Empty;
				var url = await _userService.UpdateAvatarAsync(userId, file);
				return Ok(new { avatarUrl = url });
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

		[HttpPost("follow/{id}")]
		public async Task<ActionResult<object>> Follow(string id)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				await _userService.FollowUserAsync(currentUserId, id);
				return Ok(new { message = $"Followed {id}" });
			}
			catch (KeyNotFoundException)
			{
				return NotFound(new { message = "User not found" });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		[HttpDelete("unfollow/{id}")]
		public async Task<ActionResult<object>> Unfollow(string id)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				await _userService.UnfollowUserAsync(currentUserId, id);
				return Ok(new { message = $"Unfollowed {id}" });
			}
			catch (KeyNotFoundException)
			{
				return NotFound(new { message = "User not found" });
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
			catch (InvalidOperationException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
		}

		[HttpGet("search")]
		public async Task<ActionResult<List<UserSearchDTO>>> Search([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				var results = await _userService.SearchUsersAsync(query, currentUserId, page, pageSize);
				return Ok(results);
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
			catch
			{
				// Catch any unexpected exceptions and return a 500 with a generic message
				return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while searching users." });
			}
		}

		[HttpGet("{id}/followers")]
		public async Task<ActionResult<List<UserSearchDTO>>> GetFollowers(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				var results = await _userService.GetFollowersAsync(id, currentUserId, page, pageSize);
				return Ok(results);
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

		[HttpGet("test")]
		public ActionResult<object> Test()
		{
			return Ok(new { message = "Service is working", timestamp = DateTime.UtcNow });
		}

		[HttpGet("debug")]
		public ActionResult<object> Debug()
		{
			var userId = GetCurrentUserId();
			var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
			return Ok(new { 
				userId = userId,
				userIdLength = userId?.Length ?? 0,
				claims = claims,
				isAuthenticated = User.Identity?.IsAuthenticated ?? false
			});
		}

		[HttpGet("my-friends")]
		public async Task<ActionResult<List<UserSearchDTO>>> GetMyFriends([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			try
			{
				var userId = GetCurrentUserId() ?? string.Empty;
				if (string.IsNullOrEmpty(userId))
				{
					return BadRequest(new { message = "User not authenticated" });
				}
				
				// Get the current user to find their following list
				var user = await _userService.GetUserByIdAsync(userId);
				if (user == null)
				{
					return NotFound(new { message = "User not found" });
				}
				
				// Get friends from following list
				var friends = await _userService.GetFollowingAsync(userId, userId, page, pageSize);
				return Ok(friends);
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
		public async Task<ActionResult<List<UserSearchDTO>>> GetFollowing(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			try
			{
				var currentUserId = GetCurrentUserId() ?? string.Empty;
				var results = await _userService.GetFollowingAsync(id, currentUserId, page, pageSize);
				return Ok(results);
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

		[HttpGet("suggested")]
		public async Task<ActionResult<List<UserSearchDTO>>> GetSuggestedUsers([FromQuery] string? userId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
		{
			try
			{
				var currentUserId = userId ?? GetCurrentUserId() ?? string.Empty;
				if (string.IsNullOrEmpty(currentUserId))
				{
					return BadRequest(new { message = "User ID is required" });
				}
				var results = await _userService.GetSuggestedUsersAsync(currentUserId, page, pageSize);
				return Ok(results);
			}
			catch (ArgumentException ex)
			{
				return BadRequest(new { message = ex.Message });
			}
			catch (KeyNotFoundException ex)
			{
				return NotFound(new { message = ex.Message });
			}
			catch
			{
				return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while getting suggested users." });
			}
		}

		[HttpPut("birthday")]
		public async Task<ActionResult<object>> UpdateBirthday([FromBody] UpdateBirthdayDTO updateDto)
		{
			try
			{
				var userId = GetCurrentUserId() ?? string.Empty;
				await _userService.UpdateBirthdayAsync(userId, updateDto.Birthday);
				return Ok(new { message = "Birthday updated successfully" });
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

		[HttpPut("birthday/{userId}")]
		public async Task<ActionResult<object>> UpdateUserBirthday(string userId, [FromBody] UpdateBirthdayDTO updateDto)
		{
			try
			{
				await _userService.UpdateBirthdayAsync(userId, updateDto.Birthday);
				return Ok(new { message = "Birthday updated successfully" });
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

	public class UpdateBirthdayDTO
	{
		public required string Birthday { get; set; }
	}
}
