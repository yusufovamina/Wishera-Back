using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using gift_wishlist_service.Services;
using WisheraApp.Models;
using WisheraApp.DTO;

namespace gift_wishlist_service.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GiftController : ControllerBase
    {
        private readonly IGiftApiService _giftApiService;

        public GiftController(IGiftApiService giftApiService)
        {
            _giftApiService = giftApiService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        private string? GetUsername() => User.FindFirst(ClaimTypes.Name)?.Value;

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateGift([FromForm] string name, [FromForm] decimal price, [FromForm] string category, [FromForm] string? wishlistId = null, IFormFile? imageFile = null)
        {
            // This microservice's IWishlistService exposes gift operations via separate methods; create via RPC-like handler
            var result = await _giftApiService.CreateGiftAsync(name, price, category, wishlistId, imageFile);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGift(string id, [FromBody] GiftUpdateDto giftDto)
        {
            var result = await _giftApiService.UpdateGiftAsync(id, giftDto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGift(string id)
        {
            var result = await _giftApiService.DeleteGiftAsync(id);
            return Ok(result);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGiftById(string id)
        {
            var gift = await _giftApiService.GetGiftByIdAsync(id);
            return Ok(gift);
        }

        [HttpPost("{id}/reserve")]
        public async Task<IActionResult> ReserveGift(string id)
        {
            var userId = GetCurrentUserId();
            var username = GetUsername();
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "User information is missing" });
            var result = await _giftApiService.ReserveGiftAsync(id, userId, username);
            return Ok(result);
        }

        [HttpGet("reserved")]
        public async Task<IActionResult> GetReservedGifts()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });
            var reserved = await _giftApiService.GetReservedGiftsAsync(userId);
            return Ok(reserved);
        }

        [HttpPost("{id}/cancel-reserve")]
        public async Task<IActionResult> CancelReservation(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });
            var result = await _giftApiService.CancelReservationAsync(id, userId);
            return Ok(result);
        }

        [HttpGet("wishlist")]
        public async Task<IActionResult> GetUserWishlist([FromQuery] string? category = null, [FromQuery] string? sortBy = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });
            var result = await _giftApiService.GetUserWishlistAsync(userId, category, sortBy);
            return Ok(result);
        }

        [HttpGet("shared/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSharedWishlist(string userId)
        {
            var result = await _giftApiService.GetSharedWishlistAsync(userId);
            return Ok(result);
        }

        [HttpPost("{id}/upload-image")]
        public async Task<IActionResult> UploadImage(string id, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return BadRequest(new { message = "No file uploaded" });
            var url = await _giftApiService.UploadGiftImageAsync(id, imageFile);
            return Ok(new { ImageUrl = url });
        }

        [HttpPost("{id}/assign-to-wishlist")]
        public async Task<IActionResult> AssignGiftToWishlist(string id, [FromBody] AssignGiftToWishlistDto assignDto)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });
            var result = await _giftApiService.AssignGiftToWishlistAsync(id, assignDto.WishlistId);
            return Ok(result);
        }

        [HttpPost("{id}/remove-from-wishlist")]
        public async Task<IActionResult> RemoveGiftFromWishlist(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });
            var result = await _giftApiService.RemoveGiftFromWishlistAsync(id);
            return Ok(result);
        }
    }
}


