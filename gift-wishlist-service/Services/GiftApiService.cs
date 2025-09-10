using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using WishlistApp.Models;
using WishlistApp.DTO;

namespace gift_wishlist_service.Services
{
    public interface IGiftApiService
    {
        Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, IFormFile? imageFile);
        Task<object> UpdateGiftAsync(string id, GiftUpdateDto giftDto);
        Task<object> DeleteGiftAsync(string id);
        Task<Gift> GetGiftByIdAsync(string id);
        Task<object> ReserveGiftAsync(string id, string userId, string username);
        Task<object> CancelReservationAsync(string id, string userId);
        Task<List<Gift>> GetReservedGiftsAsync(string userId);
        Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy);
        Task<List<Gift>> GetSharedWishlistAsync(string userId);
        Task<string> UploadGiftImageAsync(string id, IFormFile imageFile);
        Task<object> AssignGiftToWishlistAsync(string id, string wishlistId);
        Task<object> RemoveGiftFromWishlistAsync(string id);
    }

    public class GiftApiService : IGiftApiService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICloudinaryService _cloudinaryService;

        public GiftApiService(MongoDbContext dbContext, ICloudinaryService cloudinaryService)
        {
            _dbContext = dbContext;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, IFormFile? imageFile)
        {
            var gift = new Gift
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                Name = name,
                Price = price,
                Category = category,
                WishlistId = wishlistId
            };
            if (imageFile != null)
            {
                var uploadedUrl = await _cloudinaryService.UploadImageAsync(imageFile);
                gift.ImageUrl = uploadedUrl;
            }
            await _dbContext.Gifts.InsertOneAsync(gift);
            return new { id = gift.Id, message = "Gift created successfully" };
        }

        public async Task<object> UpdateGiftAsync(string id, GiftUpdateDto giftDto)
        {
            var existingGift = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (existingGift == null) throw new KeyNotFoundException("Gift not found");
            existingGift.Name = giftDto.Name ?? existingGift.Name;
            existingGift.Price = giftDto.Price ?? existingGift.Price;
            existingGift.Category = giftDto.Category ?? existingGift.Category;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, existingGift);
            return new { message = "Gift updated successfully" };
        }

        public async Task<object> DeleteGiftAsync(string id)
        {
            await _dbContext.Gifts.DeleteOneAsync(g => g.Id == id);
            return new { message = "Gift deleted successfully" };
        }

        public async Task<Gift> GetGiftByIdAsync(string id)
        {
            var gift = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (gift == null) throw new KeyNotFoundException("Gift not found");
            return gift;
        }

        public async Task<object> ReserveGiftAsync(string id, string userId, string username)
        {
            var giftToReserve = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToReserve == null) throw new KeyNotFoundException("Gift not found");
            if (!string.IsNullOrEmpty(giftToReserve.ReservedByUserId))
                throw new InvalidOperationException("Gift is already reserved!");
            giftToReserve.ReservedByUserId = userId;
            giftToReserve.ReservedByUsername = username;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToReserve);
            return new { message = "Gift reserved successfully", reservedBy = username };
        }

        public async Task<object> CancelReservationAsync(string id, string userId)
        {
            var giftToCancel = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToCancel == null) throw new KeyNotFoundException("Gift not found");
            if (giftToCancel.ReservedByUserId != userId)
                throw new UnauthorizedAccessException("You cannot cancel this reservation");
            giftToCancel.ReservedByUserId = null;
            giftToCancel.ReservedByUsername = null;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToCancel);
            return new { message = "Reservation cancelled successfully" };
        }

        public async Task<List<Gift>> GetReservedGiftsAsync(string userId)
        {
            return await _dbContext.Gifts.Find(g => g.ReservedByUserId == userId).ToListAsync();
        }

        public async Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy)
        {
            var userWishlistsList = await _dbContext.Wishlists.Find(w => w.UserId == userId).ToListAsync();
            var wishlistIds = userWishlistsList.Select(w => w.Id).ToList();
            var filter = Builders<Gift>.Filter.Or(
                Builders<Gift>.Filter.In(g => g.WishlistId, wishlistIds),
                Builders<Gift>.Filter.Eq(g => g.WishlistId, (string)null)
            );
            if (!string.IsNullOrEmpty(category))
            {
                filter &= Builders<Gift>.Filter.Regex(g => g.Category, new MongoDB.Bson.BsonRegularExpression(category, "i"));
            }
            var giftsQuery = _dbContext.Gifts.Find(filter);
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
            return await giftsQuery.ToListAsync();
        }

        public async Task<List<Gift>> GetSharedWishlistAsync(string userId)
        {
            return await _dbContext.Gifts.Find(g => g.WishlistId == userId).ToListAsync();
        }

        public async Task<string> UploadGiftImageAsync(string id, IFormFile imageFile)
        {
            var url = await _cloudinaryService.UploadImageAsync(imageFile);
            return url;
        }

        public async Task<object> AssignGiftToWishlistAsync(string id, string wishlistId)
        {
            var giftToAssign = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToAssign == null) throw new KeyNotFoundException("Gift not found");
            giftToAssign.WishlistId = wishlistId;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToAssign);
            return new { message = "Gift assigned to wishlist successfully" };
        }

        public async Task<object> RemoveGiftFromWishlistAsync(string id)
        {
            var giftToRemove = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToRemove == null) throw new KeyNotFoundException("Gift not found");
            giftToRemove.WishlistId = null;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToRemove);
            return new { message = "Gift removed from wishlist successfully" };
        }
    }
}


