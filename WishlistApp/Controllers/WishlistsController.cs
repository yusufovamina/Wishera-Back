using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WishlistApp.DTO;
using WishlistApp.Services;
using System.Collections.Generic;

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

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpPost]
        public async Task<ActionResult<WishlistResponseDTO>> CreateWishlist(CreateWishlistDTO createDto)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var wishlist = await _wishlistService.CreateWishlistAsync(currentUserId, createDto);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var wishlist = await _wishlistService.GetWishlistAsync(id, currentUserId);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var wishlist = await _wishlistService.UpdateWishlistAsync(id, currentUserId, updateDto);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _wishlistService.DeleteWishlistAsync(id, currentUserId);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var wishlists = await _wishlistService.GetUserWishlistsAsync(userId, currentUserId, page, pageSize);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            var feed = await _wishlistService.GetFeedAsync(currentUserId, page, pageSize);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _wishlistService.LikeWishlistAsync(id, currentUserId);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _wishlistService.UnlikeWishlistAsync(id, currentUserId);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var comment = await _wishlistService.AddCommentAsync(id, currentUserId, commentDto);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var comment = await _wishlistService.UpdateCommentAsync(id, currentUserId, commentDto);
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
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }
            try
            {
                var result = await _wishlistService.DeleteCommentAsync(id, currentUserId);
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