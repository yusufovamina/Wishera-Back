// Controllers/GiftController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WishlistApp.Models;
using WishlistApp.DTO;
using System.Security.Claims;
using WishlistApp.Services;

namespace WishlistApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GiftController : ControllerBase
    {
        private readonly IGiftWishlistServiceClient _giftWishlistServiceClient;

        public GiftController(IGiftWishlistServiceClient giftWishlistServiceClient)
        {
            Console.WriteLine("=== GiftController instantiated ===");
            _giftWishlistServiceClient = giftWishlistServiceClient;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateGift([FromForm] string name, [FromForm] decimal price, [FromForm] string category, [FromForm] string? wishlistId = null, IFormFile? imageFile = null)
        {
            Console.WriteLine("=== CreateGift endpoint called ===");
            var userId = GetCurrentUserId();
            Console.WriteLine($"CreateGift called - Name: {name}, Price: {price}, Category: {category}, WishlistId: {wishlistId ?? "null"}, UserId: {userId}");
            
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            try
            {
                var result = await _giftWishlistServiceClient.CreateGiftAsync(name, price, category, wishlistId, imageFile);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Обновление подарка
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGift(string id, [FromBody] GiftUpdateDto giftDto)
        {
            if (giftDto == null)
                return BadRequest("Invalid gift data.");

            try
            {
                var result = await _giftWishlistServiceClient.UpdateGiftAsync(id, giftDto);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGift(string id)
        {
            try
            {
                var result = await _giftWishlistServiceClient.DeleteGiftAsync(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/reserve")]
        public async Task<IActionResult> ReserveGift(string id)
        {
            var userId = GetCurrentUserId();
            var username = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "User information is missing" });

            try
            {
                var result = await _giftWishlistServiceClient.ReserveGiftAsync(id, userId, username);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Gift not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("reserved")]
        public async Task<IActionResult> GetReservedGifts()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var reservedGifts = await _giftWishlistServiceClient.GetReservedGiftsAsync(userId);
                return Ok(reservedGifts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/cancel-reserve")]
        public async Task<IActionResult> CancelReservation(string id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var result = await _giftWishlistServiceClient.CancelReservationAsync(id, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Gift not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpGet("wishlist")]
        public async Task<IActionResult> GetUserWishlist(
            [FromQuery] string? category = null,
            [FromQuery] string? sortBy = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            try
            {
                var gifts = await _giftWishlistServiceClient.GetUserWishlistAsync(userId, category, sortBy);
                return Ok(gifts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGiftById(string id)
        {
            try
            {
                var gift = await _giftWishlistServiceClient.GetGiftByIdAsync(id);
                return Ok(gift);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Gift not found" });
            }
        }

        [HttpGet("shared/{userId}")]
        public async Task<IActionResult> GetSharedWishlist(string userId)
        {
            try
            {
                var gifts = await _giftWishlistServiceClient.GetSharedWishlistAsync(userId);
                return Ok(gifts);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Загрузка изображения для существующего подарка
        [HttpPost("{id}/upload-image")]
        public async Task<IActionResult> UploadImage(string id, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var imageUrl = await _giftWishlistServiceClient.UploadGiftImageAsync(id, imageFile);
                return Ok(new { ImageUrl = imageUrl });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/assign-to-wishlist")]
        public async Task<IActionResult> AssignGiftToWishlist(string id, [FromBody] AssignGiftToWishlistDto assignDto)
        {
            var userId = GetCurrentUserId();
            Console.WriteLine($"AssignGiftToWishlist called - GiftId: {id}, WishlistId: {assignDto.WishlistId}, UserId: {userId}");
            
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized(new { message = "User not authenticated" });
            }

            try
            {
                var result = await _giftWishlistServiceClient.AssignGiftToWishlistAsync(id, assignDto.WishlistId, userId);
                Console.WriteLine($"Gift {id} successfully assigned to wishlist {assignDto.WishlistId}");
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                Console.WriteLine($"Gift or wishlist not found: {ex.Message}");
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"User doesn't have permission: {ex.Message}");
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/remove-from-wishlist")]
        public async Task<IActionResult> RemoveGiftFromWishlist(string id)
        {
            var userId = GetCurrentUserId();
            Console.WriteLine($"RemoveGiftFromWishlist called - GiftId: {id}, UserId: {userId}");
            
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized(new { message = "User not authenticated" });
            }

            try
            {
                var result = await _giftWishlistServiceClient.RemoveGiftFromWishlistAsync(id, userId);
                Console.WriteLine($"Gift {id} successfully removed from wishlist");
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                Console.WriteLine($"Gift not found: {ex.Message}");
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"User doesn't have permission: {ex.Message}");
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
