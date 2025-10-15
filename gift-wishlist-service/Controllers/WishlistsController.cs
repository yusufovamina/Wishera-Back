using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WisheraApp.DTO;
using gift_wishlist_service.Services;

namespace gift_wishlist_service.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistsController : ControllerBase
    {
        private readonly WisheraApp.Services.IWishlistService _wishlistService;

        public WishlistsController(WisheraApp.Services.IWishlistService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpPost]
        public async Task<ActionResult<WishlistResponseDTO>> CreateWishlist([FromBody] CreateWishlistDTO createDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var created = await _wishlistService.CreateWishlistAsync(currentUserId, createDto);
            return CreatedAtAction(nameof(GetWishlist), new { id = created.Id }, created);
        }

        [HttpGet("feed")]
        public async Task<ActionResult<List<WishlistFeedDTO>>> GetFeed([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                Console.WriteLine($"GetFeed called with currentUserId: '{currentUserId}'");
                
                if (string.IsNullOrEmpty(currentUserId))
                {
                    // No authenticated user → no feed (require friends/subscriptions)
                    return Ok(new List<WishlistFeedDTO>());
                }
                var feed = await _wishlistService.GetFeedAsync(currentUserId, page, pageSize);
                return Ok(feed);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetFeed error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Fail-soft to avoid breaking the dashboard
                return Ok(new List<WishlistFeedDTO>());
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<WishlistFeedDTO>>> GetUserWishlists(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                // Validate provided userId to avoid Mongo ObjectId parse errors
                if (!gift_wishlist_service.Services.ObjectIdValidator.IsValidObjectId(userId))
                {
                    return BadRequest(new { message = "Invalid user id format" });
                }
                var wishlists = await _wishlistService.GetUserWishlistsAsync(userId, currentUserId, page, pageSize);
                return Ok(wishlists);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
        }

        [HttpPost("cleanup-corrupted")]
        public async Task<ActionResult> CleanupCorruptedWishlists()
        {
            try
            {
                var deletedCount = await _wishlistService.CleanupCorruptedWishlistsAsync();
                return Ok(new { message = $"Cleaned up {deletedCount} corrupted wishlists" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("liked")]
        public async Task<ActionResult<List<WishlistFeedDTO>>> GetLikedWishlists([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var likedWishlists = await _wishlistService.GetLikedWishlistsAsync(currentUserId, page, pageSize);
            return Ok(likedWishlists);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WishlistResponseDTO>> GetWishlist(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var wishlist = await _wishlistService.GetWishlistAsync(id, currentUserId);
            return Ok(wishlist);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WishlistResponseDTO>> UpdateWishlist(string id, [FromBody] UpdateWishlistDTO updateDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var updated = await _wishlistService.UpdateWishlistAsync(id, currentUserId, updateDto);
            return Ok(updated);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteWishlist(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var ok = await _wishlistService.DeleteWishlistAsync(id, currentUserId);
            return ok ? NoContent() : NotFound(new { message = "Wishlist not found" });
        }

        [HttpPost("{id}/like")]
        public async Task<ActionResult<bool>> LikeWishlist(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _wishlistService.LikeWishlistAsync(id, currentUserId);
            return Ok(result);
        }

        [HttpDelete("{id}/unlike")]
        public async Task<ActionResult<bool>> UnlikeWishlist(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });

            var result = await _wishlistService.UnlikeWishlistAsync(id, currentUserId);
            return Ok(result);
        }

        [HttpGet("categories")]
        [AllowAnonymous]
        public ActionResult<string[]> GetCategories()
        {
            return Ok(gift_wishlist_service.Services.WishlistCategories.Categories);
        }

        [HttpPost("upload-image")]
        public async Task<ActionResult> UploadItemImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });
            var url = await _wishlistService.UploadItemImageAsync(file);
            return Ok(new { imageUrl = url });
        }

        [HttpPost("{id}/comments")]
        public async Task<ActionResult<CommentDTO>> AddComment(string id, [FromBody] CreateCommentDTO commentDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });
            var comment = await _wishlistService.AddCommentAsync(id, currentUserId, commentDto);
            return Ok(comment);
        }

        [HttpGet("{id}/comments")]
        public async Task<ActionResult<List<CommentDTO>>> GetComments(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var comments = await _wishlistService.GetCommentsAsync(id, page, pageSize);
            return Ok(comments);
        }

        [HttpPut("comments/{id}")]
        public async Task<ActionResult<CommentDTO>> UpdateComment(string id, [FromBody] UpdateCommentDTO commentDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });
            var comment = await _wishlistService.UpdateCommentAsync(id, currentUserId, commentDto);
            return Ok(comment);
        }

        [HttpDelete("comments/{id}")]
        public async Task<ActionResult> DeleteComment(string id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized(new { message = "User not authenticated" });
            var ok = await _wishlistService.DeleteCommentAsync(id, currentUserId);
            return ok ? NoContent() : NotFound(new { message = "Comment not found" });
        }
    }
}


