using Microsoft.AspNetCore.Mvc;
using user_service.Services;
using WishlistApp.DTO;

namespace user_service.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class UsersController : ControllerBase
	{
		private readonly IUserService _userService;

		public UsersController(IUserService userService)
		{
			_userService = userService;
		}

		[HttpGet("{id}")]
		public async Task<ActionResult<UserProfileDTO>> GetById(string id, [FromQuery] string currentUserId)
		{
			var profile = await _userService.GetUserProfileAsync(id, currentUserId);
			return Ok(profile);
		}

		[HttpPut("profile")]
		public async Task<ActionResult<UserProfileDTO>> UpdateProfile([FromQuery] string userId, [FromBody] UpdateUserProfileDTO updateDto)
		{
			var profile = await _userService.UpdateUserProfileAsync(userId, updateDto);
			return Ok(profile);
		}

		[HttpPost("avatar")]
		public async Task<ActionResult<object>> UploadAvatar([FromQuery] string userId, IFormFile file)
		{
			var url = await _userService.UpdateAvatarAsync(userId, file);
			return Ok(new { avatarUrl = url });
		}

		[HttpPost("follow/{id}")]
		public async Task<ActionResult<object>> Follow(string id, [FromQuery] string currentUserId)
		{
			await _userService.FollowUserAsync(currentUserId, id);
			return Ok(new { message = $"Followed {id}" });
		}

		[HttpDelete("unfollow/{id}")]
		public async Task<ActionResult<object>> Unfollow(string id, [FromQuery] string currentUserId)
		{
			await _userService.UnfollowUserAsync(currentUserId, id);
			return Ok(new { message = $"Unfollowed {id}" });
		}

		[HttpGet("search")]
		public async Task<ActionResult<List<UserSearchDTO>>> Search([FromQuery] string q, [FromQuery] string currentUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			var results = await _userService.SearchUsersAsync(q, currentUserId, page, pageSize);
			return Ok(results);
		}

		[HttpGet("{id}/followers")]
		public async Task<ActionResult<List<UserSearchDTO>>> GetFollowers(string id, [FromQuery] string currentUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			var results = await _userService.GetFollowersAsync(id, currentUserId, page, pageSize);
			return Ok(results);
		}

		[HttpGet("{id}/following")]
		public async Task<ActionResult<List<UserSearchDTO>>> GetFollowing(string id, [FromQuery] string currentUserId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			var results = await _userService.GetFollowingAsync(id, currentUserId, page, pageSize);
			return Ok(results);
		}
	}
}
