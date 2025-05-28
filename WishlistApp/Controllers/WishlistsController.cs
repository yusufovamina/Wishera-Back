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
    public class WishlistsController : ControllerBase
    {
        private readonly IWishlistService _wishlistService;

        public WishlistsController(IWishlistService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpPost]
        public async Task<ActionResult<WishlistResponseDTO>> CreateWishlist(CreateWishlistDTO createDto)
        {
            try
            {
                var wishlist = await _wishlistService.CreateWishlistAsync(GetCurrentUserId(), createDto);
                return CreatedAtAction(nameof(GetWishlist), new { id = wishlist.Id }, wishlist);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<WishlistResponseDTO>> GetWishlist(string id)
        {
            try
            {
                var wishlist = await _wishlistService.GetWishlistAsync(id, GetCurrentUserId());
                return Ok(wishlist);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<WishlistResponseDTO>> UpdateWishlist(string id, UpdateWishlistDTO updateDto)
        {
            try
            {
                var wishlist = await _wishlistService.UpdateWishlistAsync(id, GetCurrentUserId(), updateDto);
                return Ok(wishlist);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteWishlist(string id)
        {
            try
            {
                var result = await _wishlistService.DeleteWishlistAsync(id, GetCurrentUserId());
                if (result)
                    return NoContent();
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<WishlistFeedDTO>>> GetUserWishlists(
            string userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var wishlists = await _wishlistService.GetUserWishlistsAsync(userId, GetCurrentUserId(), page, pageSize);
                return Ok(wishlists);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
        }

        [HttpGet("feed")]
        public async Task<ActionResult<List<WishlistFeedDTO>>> GetFeed(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var feed = await _wishlistService.GetFeedAsync(GetCurrentUserId(), page, pageSize);
            return Ok(feed);
        }

        [HttpPost("upload-image")]
        public async Task<ActionResult<string>> UploadItemImage(IFormFile file)
        {
            try
            {
                var imageUrl = await _wishlistService.UploadItemImageAsync(file);
                return Ok(new { imageUrl });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/like")]
        public async Task<ActionResult<bool>> LikeWishlist(string id)
        {
            try
            {
                var result = await _wishlistService.LikeWishlistAsync(id, GetCurrentUserId());
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpDelete("{id}/unlike")]
        public async Task<ActionResult<bool>> UnlikeWishlist(string id)
        {
            try
            {
                var result = await _wishlistService.UnlikeWishlistAsync(id, GetCurrentUserId());
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
        }

        [HttpPost("{id}/comments")]
        public async Task<ActionResult<CommentDTO>> AddComment(string id, CreateCommentDTO commentDto)
        {
            try
            {
                var comment = await _wishlistService.AddCommentAsync(id, GetCurrentUserId(), commentDto);
                return Ok(comment);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpPut("comments/{id}")]
        public async Task<ActionResult<CommentDTO>> UpdateComment(string id, UpdateCommentDTO commentDto)
        {
            try
            {
                var comment = await _wishlistService.UpdateCommentAsync(id, GetCurrentUserId(), commentDto);
                return Ok(comment);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Comment not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpDelete("comments/{id}")]
        public async Task<ActionResult> DeleteComment(string id)
        {
            try
            {
                var result = await _wishlistService.DeleteCommentAsync(id, GetCurrentUserId());
                if (result)
                    return NoContent();
                return NotFound(new { message = "Comment not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        [HttpGet("{id}/comments")]
        public async Task<ActionResult<List<CommentDTO>>> GetComments(
            string id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var comments = await _wishlistService.GetCommentsAsync(id, page, pageSize);
                return Ok(comments);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
        }
    }
} 