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
using MongoDB.Bson;

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
            Console.WriteLine("=== GiftController instantiated ===");
            _context = context;
            _cloudinaryService = cloudinaryService;
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

            // If wishlistId is provided, validate that it exists and belongs to the user
            if (!string.IsNullOrEmpty(wishlistId))
            {
                var wishlist = await _context.Wishlists.Find(w => w.Id == wishlistId && w.UserId == userId).FirstOrDefaultAsync();
                if (wishlist == null)
                {
                    return BadRequest(new { message = "Wishlist not found or you don't have permission to add gifts to it" });
                }
            }

            var gift = new Gift
            {
                Id = ObjectId.GenerateNewId().ToString(),
                Name = name,
                Price = price,
                Category = category,
                WishlistId = wishlistId // This can be null now
            };
            Console.WriteLine($"Gift created with WishlistId: {gift.WishlistId ?? "null"}");

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
            var userId = GetCurrentUserId();
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
            var userId = GetCurrentUserId();
            var reservedGifts = await _context.Gifts.Find(g => g.ReservedByUserId == userId).ToListAsync();

            return Ok(reservedGifts);
        }

        [HttpPost("{id}/cancel-reserve")]
        public async Task<IActionResult> CancelReservation(string id)
        {
            var userId = GetCurrentUserId();
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
            var userId = GetCurrentUserId();
            
            // First, get all wishlists belonging to the user
            var userWishlists = await _context.Wishlists.Find(w => w.UserId == userId).ToListAsync();
            var wishlistIds = userWishlists.Select(w => w.Id).ToList();
            
            // Find all gifts that belong to any of the user's wishlists OR are unassigned (no wishlist)
            var filter = Builders<Gift>.Filter.Or(
                Builders<Gift>.Filter.In(g => g.WishlistId, wishlistIds),
                Builders<Gift>.Filter.Eq(g => g.WishlistId, (string)null)
            );

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

            // Find the gift
            var gift = await _context.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (gift == null)
            {
                Console.WriteLine($"Gift not found: {id}");
                return NotFound(new { message = "Gift not found" });
            }
            Console.WriteLine($"Gift found: {gift.Name}, WishlistId: {gift.WishlistId}");

            // Verify the gift belongs to the current user
            // If the gift has a wishlist, check if it belongs to the user
            // If the gift has no wishlist (unassigned), it belongs to the user
            if (!string.IsNullOrEmpty(gift.WishlistId))
            {
                var userWishlists = await _context.Wishlists.Find(w => w.UserId == userId).ToListAsync();
                var userWishlistIds = userWishlists.Select(w => w.Id).ToList();
                Console.WriteLine($"User wishlists: {string.Join(", ", userWishlistIds)}");
                Console.WriteLine($"Gift wishlist: {gift.WishlistId}");
                
                if (!userWishlistIds.Contains(gift.WishlistId))
                {
                    Console.WriteLine("User doesn't have permission to modify this gift");
                    return Unauthorized(new { message = "You don't have permission to modify this gift" });
                }
            }
            else
            {
                Console.WriteLine("Gift is unassigned - user can modify it");
            }

            // Verify the target wishlist exists and belongs to the current user
            var targetWishlist = await _context.Wishlists.Find(w => w.Id == assignDto.WishlistId).FirstOrDefaultAsync();
            if (targetWishlist == null)
            {
                Console.WriteLine($"Target wishlist not found: {assignDto.WishlistId}");
                return NotFound(new { message = "Target wishlist not found" });
            }
            Console.WriteLine($"Target wishlist found: {targetWishlist.Title}, Owner: {targetWishlist.UserId}");

            if (targetWishlist.UserId != userId)
            {
                Console.WriteLine("User doesn't have permission to add gifts to this wishlist");
                return Unauthorized(new { message = "You don't have permission to add gifts to this wishlist" });
            }

            // Update the gift's wishlist
            gift.WishlistId = assignDto.WishlistId;
            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, gift);
            Console.WriteLine($"Gift {gift.Name} successfully assigned to wishlist {assignDto.WishlistId}");

            return Ok(new { message = "Gift assigned to wishlist successfully" });
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

            // Find the gift
            var gift = await _context.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (gift == null)
            {
                Console.WriteLine($"Gift not found: {id}");
                return NotFound(new { message = "Gift not found" });
            }
            Console.WriteLine($"Gift found: {gift.Name}, WishlistId: {gift.WishlistId}");

            // Verify the gift belongs to the current user
            if (!string.IsNullOrEmpty(gift.WishlistId))
            {
                var userWishlists = await _context.Wishlists.Find(w => w.UserId == userId).ToListAsync();
                var userWishlistIds = userWishlists.Select(w => w.Id).ToList();
                Console.WriteLine($"User wishlists: {string.Join(", ", userWishlistIds)}");
                Console.WriteLine($"Gift wishlist: {gift.WishlistId}");
                
                if (!userWishlistIds.Contains(gift.WishlistId))
                {
                    Console.WriteLine("User doesn't have permission to modify this gift");
                    return Unauthorized(new { message = "You don't have permission to modify this gift" });
                }
            }
            else
            {
                Console.WriteLine("Gift is unassigned - user can modify it");
            }

            // Remove the gift from the wishlist by setting WishlistId to null
            gift.WishlistId = null;
            await _context.Gifts.ReplaceOneAsync(g => g.Id == id, gift);
            Console.WriteLine($"Gift {gift.Name} successfully removed from wishlist");

            return Ok(new { message = "Gift removed from wishlist successfully" });
        }
    }
}
