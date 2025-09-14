using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WisheraApp.DTO;
using WisheraApp.Services;
using System.Collections.Generic;

namespace WisheraApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class WishlistsController : ControllerBase
    {
        private readonly IGiftWishlistServiceClient _giftWishlistServiceClient;

        public WishlistsController(IGiftWishlistServiceClient giftWishlistServiceClient)
        {
            _giftWishlistServiceClient = giftWishlistServiceClient;
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
                var wishlist = await _giftWishlistServiceClient.CreateWishlistAsync(currentUserId, createDto);
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
                var wishlist = await _giftWishlistServiceClient.GetWishlistAsync(id, currentUserId);
                return Ok(wishlist);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
            
            Console.WriteLine($"UpdateWishlist called - ID: {id}, User: {currentUserId}");
            Console.WriteLine($"Update data - Title: {updateDto.Title}, Description: {updateDto.Description}, Category: {updateDto.Category}, IsPublic: {updateDto.IsPublic}");
            
            try
            {
                var wishlist = await _giftWishlistServiceClient.UpdateWishlistAsync(id, currentUserId, updateDto);
                Console.WriteLine($"Update successful - New title: {wishlist.Title}, Category: {wishlist.Category}, IsPublic: {wishlist.IsPublic}");
                return Ok(wishlist);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                var result = await _giftWishlistServiceClient.DeleteWishlistAsync(id, currentUserId);
                if (result)
                    return NoContent();
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                var wishlists = await _giftWishlistServiceClient.GetUserWishlistsAsync(userId, currentUserId, page, pageSize);
                return Ok(wishlists);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "User not found" });
            }
        }

        [HttpGet("categories")]
        public ActionResult<string[]> GetCategories()
        {
            return Ok(_giftWishlistServiceClient.GetCategories());
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
            var feed = await _giftWishlistServiceClient.GetFeedAsync(currentUserId, page, pageSize);
            return Ok(feed);
        }

        [HttpPost("upload-image")]
        public async Task<ActionResult<string>> UploadItemImage(IFormFile file)
        {
            try
            {
                var imageUrl = await _giftWishlistServiceClient.UploadItemImageAsync(file);
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
                var result = await _giftWishlistServiceClient.LikeWishlistAsync(id, currentUserId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                var result = await _giftWishlistServiceClient.UnlikeWishlistAsync(id, currentUserId);
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
                var comment = await _giftWishlistServiceClient.AddCommentAsync(id, currentUserId, commentDto);
                return Ok(comment);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                var comment = await _giftWishlistServiceClient.UpdateCommentAsync(id, currentUserId, commentDto);
                return Ok(comment);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Comment not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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
                var result = await _giftWishlistServiceClient.DeleteCommentAsync(id, currentUserId);
                if (result)
                    return NoContent();
                return NotFound(new { message = "Comment not found" });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
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
                var comments = await _giftWishlistServiceClient.GetCommentsAsync(id, page, pageSize);
                return Ok(comments);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Wishlist not found" });
            }
        }
    }
} 