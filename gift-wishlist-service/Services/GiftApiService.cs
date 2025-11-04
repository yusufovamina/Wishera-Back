using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using WisheraApp.Models;
using WisheraApp.DTO;

namespace gift_wishlist_service.Services
{
    public interface IGiftApiService
    {
        Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, string? userId, IFormFile? imageFile);
        Task<object> UpdateGiftAsync(string id, GiftUpdateDto giftDto);
        Task<object> DeleteGiftAsync(string id);
        Task<Gift> GetGiftByIdAsync(string id);
        Task<object> ReserveGiftAsync(string id, string userId, string username);
        Task<object> CancelReservationAsync(string id, string userId);
        Task<List<Gift>> GetReservedGiftsAsync(string userId);
        Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy);
        Task<List<Gift>> GetSharedWishlistAsync(string userId);
        Task<string> UploadGiftImageAsync(string id, IFormFile imageFile);
        Task<object> AssignGiftToWishlistAsync(string id, string wishlistId, string userId);
        Task<object> RemoveGiftFromWishlistAsync(string id, string userId);
    }

    public class GiftApiService : IGiftApiService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly INotificationClient _notificationClient;

        public GiftApiService(MongoDbContext dbContext, ICloudinaryService cloudinaryService, INotificationClient notificationClient)
        {
            _dbContext = dbContext;
            _cloudinaryService = cloudinaryService;
            _notificationClient = notificationClient;
        }

        public async Task<object> CreateGiftAsync(string name, decimal price, string category, string? wishlistId, string? userId, IFormFile? imageFile)
        {
            var gift = new Gift
            {
                Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                Name = name,
                Price = price,
                Category = category,
                WishlistId = wishlistId,
                UserId = userId
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

            // Get the wishlist to find the owner and create notification
            if (!string.IsNullOrEmpty(giftToReserve.WishlistId))
            {
                var wishlist = await _dbContext.Wishlists.Find(w => w.Id == giftToReserve.WishlistId).FirstOrDefaultAsync();
                if (wishlist != null && wishlist.UserId != userId)
                {
                    try
                    {
                        await _notificationClient.CreateGiftReservedNotificationAsync(
                            wishlist.UserId,
                            userId,
                            id,
                            giftToReserve.Name,
                            giftToReserve.WishlistId
                        );
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't fail the reservation
                        Console.WriteLine($"Failed to create gift reserved notification: {ex.Message}");
                    }
                }
            }

            return new { message = "Gift reserved successfully", reservedBy = username };
        }

        public async Task<object> CancelReservationAsync(string id, string userId)
        {
            var giftToCancel = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToCancel == null) throw new KeyNotFoundException("Gift not found");
            if (giftToCancel.ReservedByUserId != userId)
                throw new UnauthorizedAccessException("You cannot cancel this reservation");
            
            // Get wishlist owner ID before clearing the reservation
            string? wishlistOwnerId = null;
            if (!string.IsNullOrEmpty(giftToCancel.WishlistId))
            {
                var wishlist = await _dbContext.Wishlists.Find(w => w.Id == giftToCancel.WishlistId).FirstOrDefaultAsync();
                if (wishlist != null)
                {
                    wishlistOwnerId = wishlist.UserId;
                }
            }
            
            giftToCancel.ReservedByUserId = null;
            giftToCancel.ReservedByUsername = null;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToCancel);
            
            // Delete the gift reserved notification
            if (!string.IsNullOrEmpty(wishlistOwnerId) && wishlistOwnerId != userId)
            {
                try
                {
                    // NotificationType.GiftReserved = 12
                    await _notificationClient.DeleteNotificationByTypeAndRelatedUserAsync(
                        wishlistOwnerId,
                        12, // GiftReserved
                        userId,
                        id
                    );
                }
                catch (Exception ex)
                {
                    // Log error but don't fail the cancel operation
                    Console.WriteLine($"Failed to delete gift reserved notification: {ex.Message}");
                }
            }
            
            return new { message = "Reservation cancelled successfully" };
        }

        public async Task<List<Gift>> GetReservedGiftsAsync(string userId)
        {
            return await _dbContext.Gifts.Find(g => g.ReservedByUserId == userId).ToListAsync();
        }

        public async Task<List<Gift>> GetUserWishlistAsync(string userId, string? category, string? sortBy)
        {
            Console.WriteLine($"=== GetUserWishlistAsync called for userId: {userId} ===");
            
            var userWishlistsList = await _dbContext.Wishlists.Find(w => w.UserId == userId).ToListAsync();
            var wishlistIds = userWishlistsList.Select(w => w.Id).ToList();
            
            Console.WriteLine($"Found {userWishlistsList.Count} wishlists for user {userId}");
            Console.WriteLine($"Wishlist IDs: [{string.Join(", ", wishlistIds)}]");
            
            // Return gifts owned by the user (either assigned to their wishlists OR unassigned)
            // Build filter: (UserId == userId AND WishlistId in user's wishlists) OR (UserId == userId AND WishlistId == null)
            var filter = Builders<Gift>.Filter.Or(
                Builders<Gift>.Filter.And(
                    Builders<Gift>.Filter.Eq(g => g.UserId, userId),
                    Builders<Gift>.Filter.In(g => g.WishlistId, wishlistIds)
                ),
                Builders<Gift>.Filter.And(
                    Builders<Gift>.Filter.Eq(g => g.UserId, userId),
                    Builders<Gift>.Filter.Eq(g => g.WishlistId, (string?)null)
                )
            );
            
            // If no wishlists exist, just filter by UserId (including null wishlistId)
            if (wishlistIds.Count == 0)
            {
                filter = Builders<Gift>.Filter.Eq(g => g.UserId, userId);
            }
            
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
            var gifts = await giftsQuery.ToListAsync();
            
            Console.WriteLine($"Found {gifts.Count} gifts for user {userId}");
            foreach (var gift in gifts)
            {
                Console.WriteLine($"  Gift: {gift.Name} (WishlistId: {gift.WishlistId}, UserId: {gift.UserId})");
            }
            
            return gifts;
        }

        public async Task<List<Gift>> GetSharedWishlistAsync(string userId)
        {
            // Find all wishlists belonging to the user
            var userWishlistsList = await _dbContext.Wishlists.Find(w => w.UserId == userId).ToListAsync();
            var wishlistIds = userWishlistsList.Select(w => w.Id).ToList();
            
            // Return gifts from those wishlists
            return await _dbContext.Gifts.Find(g => g.WishlistId != null && wishlistIds.Contains(g.WishlistId)).ToListAsync();
        }

        public async Task<string> UploadGiftImageAsync(string id, IFormFile imageFile)
        {
            var url = await _cloudinaryService.UploadImageAsync(imageFile);
            return url;
        }

        public async Task<object> AssignGiftToWishlistAsync(string id, string wishlistId, string userId)
        {
            var giftToAssign = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToAssign == null) throw new KeyNotFoundException("Gift not found");
            
            // Verify the user owns the gift (allow null UserId for backward compatibility with old gifts)
            if (!string.IsNullOrEmpty(giftToAssign.UserId) && giftToAssign.UserId != userId)
            {
                throw new UnauthorizedAccessException("You are not authorized to assign this gift.");
            }
            // If UserId is null (old gift), set it now
            if (string.IsNullOrEmpty(giftToAssign.UserId))
            {
                giftToAssign.UserId = userId;
            }
            
            // Verify the user owns the wishlist they're assigning to
            if (!string.IsNullOrEmpty(wishlistId))
            {
                var wishlist = await _dbContext.Wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync();
                if (wishlist == null)
                {
                    throw new KeyNotFoundException("Wishlist not found.");
                }
                if (wishlist.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to assign gifts to this wishlist.");
                }
            }
            
            giftToAssign.WishlistId = wishlistId;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToAssign);
            return new { message = "Gift assigned to wishlist successfully" };
        }

        public async Task<object> RemoveGiftFromWishlistAsync(string id, string userId)
        {
            var giftToRemove = await _dbContext.Gifts.Find(g => g.Id == id).FirstOrDefaultAsync();
            if (giftToRemove == null) throw new KeyNotFoundException("Gift not found");
            
            // Verify the user owns the wishlist that contains this gift
            if (!string.IsNullOrEmpty(giftToRemove.WishlistId))
            {
                var wishlist = await _dbContext.Wishlists.Find(w => w.Id == giftToRemove.WishlistId).FirstOrDefaultAsync();
                if (wishlist == null || wishlist.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to remove gifts from this wishlist.");
                }
            }
            else
            {
                // If gift has no wishlist, verify user owns the gift
                if (giftToRemove.UserId != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to modify this gift.");
                }
            }
            
            giftToRemove.WishlistId = null;
            await _dbContext.Gifts.ReplaceOneAsync(g => g.Id == id, giftToRemove);
            return new { message = "Gift removed from wishlist successfully" };
        }
    }
}


