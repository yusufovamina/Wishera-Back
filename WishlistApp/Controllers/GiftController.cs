// Controllers/GiftController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WishlistApp.Models;
using WishlistApp.DTO;
using System.Security.Claims;
using WishlistApp.Services;
using MongoDB.Driver;

namespace WishlistApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class GiftController : ControllerBase
    {
        private readonly MongoDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public GiftController(MongoDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateGift([FromForm] string name, [FromForm] decimal price, [FromForm] string category, IFormFile imageFile)
        {
            var userId = User.FindFirst(ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var gift = new Gift
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Price = price,
                Category = category,
                WishlistId = userId // ✅ Set WishlistId to match UserId
            };

            if (imageFile != null)
            {
                var imageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                gift.ImageUrl = imageUrl;
            }

            await _context.Gifts.InsertOneAsync(gift);
            return Ok(new { id = gift.Id, message = "Gift created successfully" });
        }

        // Обновление подарка
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGift(string id, [FromBody] GiftUpdateDto giftDto)
        {
            if (giftDto == null)
                return BadRequest("Invalid gift data.");

            var filter = Builders<Gift>.Filter.Eq(g => g.Id, id);
            var existingGift = await _context.Gifts.Find(filter).FirstOrDefaultAsync();
            if (existingGift == null)
                return NotFound();

            existingGift.Name = giftDto.Name ?? existingGift.Name;
            existingGift.Price = giftDto.Price ?? existingGift.Price;

            existingGift.Category = giftDto.Category ?? existingGift.Category;

            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, existingGift);
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGift(string id)
        {
            var filter = Builders<Gift>.Filter.Eq(g => g.Id, id);
            var gift = await _context.Gifts.Find(filter).FirstOrDefaultAsync();
            if (gift == null)
                return NotFound();

            await _context.Gifts.DeleteOneAsync(filter);

            return Ok();
        }

        [HttpPost("{id}/reserve")]
        public async Task<IActionResult> ReserveGift(string id)
        {
            var userId = User.FindFirst(ClaimTypes.Name)?.Value;
            var username = User.FindFirst(ClaimTypes.GivenName)?.Value; // ✅ Fetch username

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
                return Unauthorized(new { message = "User information is missing" });

            var gift = await _context.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (gift == null) return NotFound(new { message = "Gift not found" });
            if (!string.IsNullOrEmpty(gift.ReservedByUserId))
                return BadRequest(new { message = "Gift is already reserved!" });

            // ✅ Assign the reserving user and username
            gift.ReservedByUserId = userId;
            gift.ReservedByUsername = username;

            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, gift);

            return Ok(new { message = "Gift reserved successfully", reservedBy = username });
        }

        [HttpGet("reserved")]
        public async Task<IActionResult> GetReservedGifts()
        {
            var userId = User.FindFirst(ClaimTypes.Name)?.Value;
            var reservedGifts = await _context.Gifts.Find(g => g.ReservedByUserId == userId).ToListAsync();

            return Ok(reservedGifts);
        }

        [HttpPost("{id}/cancel-reserve")]
        public async Task<IActionResult> CancelReservation(string id)
        {
            var userId = User.FindFirst(ClaimTypes.Name)?.Value;
            var gift = await _context.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();

            if (gift == null) return NotFound(new { message = "Gift not found" });
            if (gift.ReservedByUserId != userId) return Unauthorized(new { message = "You cannot cancel this reservation" });

            // Убираем резерв
            gift.ReservedByUserId = null;
            gift.ReservedByUsername = null;

            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, gift);

            return Ok(new { message = "Reservation cancelled successfully" });
        }

        [HttpGet("wishlist")]
        public async Task<IActionResult> GetUserWishlist(
            [FromQuery] string? category = null,
            [FromQuery] string? sortBy = null)
        {
            var userId = User.FindFirst(ClaimTypes.Name)?.Value;
            var filter = Builders<Gift>.Filter.Eq(g => g.WishlistId, userId);

            if (!string.IsNullOrEmpty(category))
            {
                filter &= Builders<Gift>.Filter.Regex(g => g.Category, new MongoDB.Bson.BsonRegularExpression(category, "i"));
            }

            var giftsQuery = _context.Gifts.Find(filter);

            if (!string.IsNullOrEmpty(sortBy))
            {
                giftsQuery = sortBy switch
                {
                    "price-asc" => giftsQuery.SortBy(g => g.Price),
                    "price-desc" => giftsQuery.SortByDescending(g => g.Price),
                    "name-asc" => giftsQuery.SortBy(g => g.Name),
                    "name-desc" => giftsQuery.SortByDescending(g => g.Name),
                    _ => giftsQuery
                };
            }

            var gifts = await giftsQuery.ToListAsync();
            return Ok(gifts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGiftById(string id)
        {
            var gift = await _context.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();

            if (gift == null)
            {
                return NotFound(new { message = "Gift not found" });
            }

            return Ok(gift);
        }

        [HttpGet("shared/{userId}")]
        public async Task<IActionResult> GetSharedWishlist(string userId)
        {
            var gifts = await _context.Gifts.Find(g => g.WishlistId == userId).ToListAsync();
            return Ok(gifts);
        }

        // Загрузка изображения для существующего подарка
        [HttpPost("{id}/upload-image")]
        public async Task<IActionResult> UploadImage(string id, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return BadRequest("No file uploaded.");

            var filter = Builders<Gift>.Filter.Eq(g => g.Id, id);
            var gift = await _context.Gifts.Find(filter).FirstOrDefaultAsync();
            
            if (gift == null)
                return NotFound();

            var imageUrl = await _cloudinaryService.UploadImageAsync(imageFile);
            gift.ImageUrl = imageUrl;

            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, gift);
            return Ok(new { ImageUrl = imageUrl });
        }
    }
}
