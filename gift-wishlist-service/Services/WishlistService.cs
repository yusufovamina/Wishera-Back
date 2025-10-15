using MongoDB.Driver;
using WisheraApp.DTO;
using WisheraApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace gift_wishlist_service.Services
{
    public static class WishlistCategories
    {
        public static readonly string[] Categories = new[]
        {
            "Electronics", "Books", "Home Decor", "Fashion", "Sports",
            "Toys", "Gaming", "Health", "Beauty", "Food",
            "Travel", "Experiences", "Jewelry", "Art", "Collectibles"
        };
    }

   

    public class WishlistService : WisheraApp.Services.IWishlistService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICloudinaryService _cloudinaryService;
		private readonly ICacheService _cache;

		public WishlistService(MongoDbContext dbContext, ICloudinaryService cloudinaryService, ICacheService cache)
        {
            _dbContext = dbContext;
            _cloudinaryService = cloudinaryService;
			_cache = cache;
        }

        public async Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto)
        {
            // Validate userId format before querying Mongo to avoid parse errors
            if (string.IsNullOrEmpty(userId) || !ObjectIdValidator.IsValidObjectId(userId))
            {
                throw new ArgumentException("Invalid user id format.");
            }
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            if (string.IsNullOrWhiteSpace(createDto.Title)) throw new ArgumentException("Title cannot be empty.");

            var wishlist = new Wishlist
            {
                UserId = userId,
                Title = createDto.Title,
                Description = createDto.Description ?? string.Empty,
                Category = createDto.Category ?? string.Empty,
                IsPublic = createDto.IsPublic,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

			await _dbContext.Wishlists.InsertOneAsync(wishlist);
			await _cache.RemoveAsync($"wishlist:feed:{userId}:1:10");

            // Update user's wishlists list
            var userUpdate = Builders<User>.Update.AddToSet(u => u.WishlistIds, wishlist.Id);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, userUpdate);

            return new WishlistResponseDTO
            {
                Id = wishlist.Id,
                UserId = wishlist.UserId,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                Title = wishlist.Title,
                Description = wishlist.Description,
                Category = wishlist.Category,
                IsPublic = wishlist.IsPublic,
                CreatedAt = wishlist.CreatedAt,
                UpdatedAt = wishlist.UpdatedAt,
                Items = new List<WishlistItemDTO>(), // Empty items for new wishlist
                LikeCount = 0,
                CommentCount = 0,
                IsLiked = false,
                IsOwner = true // Creator is always the owner
            };
        }

		public async Task<WishlistResponseDTO> GetWishlistAsync(string id, string currentUserId)
        {
			var cacheKey = $"wishlist:detail:{id}:{currentUserId}";
			return await _cache.GetOrSetAsync(cacheKey, async () =>
			{
				var wishlist = await _dbContext.Wishlists.Find(w => w.Id == id).FirstOrDefaultAsync();
            if (wishlist == null) throw new KeyNotFoundException("Wishlist not found.");

            var user = await _dbContext.Users.Find(u => u.Id == wishlist.UserId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("Wishlist owner not found.");

            var isFollowing = await _dbContext.Relationships.Find(
                r => r.FollowerId == currentUserId && r.FollowingId == user.Id
            ).AnyAsync();

            // Check access rights
            if (!wishlist.IsPublic && wishlist.UserId != currentUserId && !(user.AllowedViewerIds?.Contains(currentUserId) ?? false))
            {
                throw new UnauthorizedAccessException("You do not have permission to view this wishlist.");
            }

            // Build items from Gifts collection assigned to this wishlist
            var gifts = await _dbContext.Gifts.Find(g => g.WishlistId == wishlist.Id).ToListAsync();
            var items = gifts.Select(g => new WishlistItemDTO
            {
                Title = g.Name,
                Description = null,
                ImageUrl = g.ImageUrl,
                Category = g.Category,
                Price = g.Price,
                Url = null,
                GiftId = g.Id
            }).ToList();

            // Aggregate likes and comments
            var likeCount = (int)await _dbContext.Likes.CountDocumentsAsync(l => l.WishlistId == id);
            var commentCount = (int)await _dbContext.Comments.CountDocumentsAsync(c => c.WishlistId == id);
            var isLiked = !string.IsNullOrEmpty(currentUserId)
                && await _dbContext.Likes.Find(l => l.WishlistId == id && l.UserId == currentUserId).AnyAsync();

			return new WishlistResponseDTO
            {
                Id = wishlist.Id,
                UserId = wishlist.UserId,
                Title = wishlist.Title,
                Description = wishlist.Description,
                Category = wishlist.Category,
                IsPublic = wishlist.IsPublic,
                CreatedAt = wishlist.CreatedAt,
                UpdatedAt = wishlist.UpdatedAt,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                Items = items,
                LikeCount = likeCount,
                CommentCount = commentCount,
                IsLiked = isLiked,
                IsOwner = wishlist.UserId == currentUserId
            };
			}, TimeSpan.FromMinutes(2))!;
		}

        public async Task<WishlistResponseDTO> UpdateWishlistAsync(string id, string currentUserId, UpdateWishlistDTO updateDto)
        {
            var wishlist = await _dbContext.Wishlists.Find(w => w.Id == id).FirstOrDefaultAsync();
            if (wishlist == null) throw new KeyNotFoundException("Wishlist not found.");

            if (wishlist.UserId != currentUserId) throw new UnauthorizedAccessException("You are not authorized to update this wishlist.");

            wishlist.Title = updateDto.Title ?? wishlist.Title;
            wishlist.Description = updateDto.Description ?? wishlist.Description;
            wishlist.Category = updateDto.Category ?? wishlist.Category;
            wishlist.IsPublic = updateDto.IsPublic;
            wishlist.UpdatedAt = DateTime.UtcNow;

			await _dbContext.Wishlists.ReplaceOneAsync(w => w.Id == id, wishlist);
			await _cache.RemoveAsync($"wishlist:detail:{id}:{currentUserId}");

			return await GetWishlistAsync(id, currentUserId); // Fetch updated wishlist
        }

        public async Task<bool> DeleteWishlistAsync(string id, string currentUserId)
        {
            var wishlist = await _dbContext.Wishlists.Find(w => w.Id == id).FirstOrDefaultAsync();
            if (wishlist == null) return false;

            if (wishlist.UserId != currentUserId) throw new UnauthorizedAccessException("You are not authorized to delete this wishlist.");

            // Remove associated gifts
            await _dbContext.Gifts.DeleteManyAsync(g => g.WishlistId == id);
            // Remove associated likes
            await _dbContext.Likes.DeleteManyAsync(l => l.WishlistId == id);
            // Remove associated comments
            await _dbContext.Comments.DeleteManyAsync(c => c.WishlistId == id);
            // Remove associated feed events
            await _dbContext.Feed.DeleteManyAsync(fe => fe.WishlistId == id);

			var deleteResult = await _dbContext.Wishlists.DeleteOneAsync(w => w.Id == id);
			await _cache.RemoveAsync($"wishlist:detail:{id}:{currentUserId}");

            // Update user's wishlists list
            var update = Builders<User>.Update.Pull(u => u.WishlistIds, id);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == wishlist.UserId, update);

            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page, int pageSize)
        {
            // Validate userId format before querying Mongo to avoid parse errors
            if (string.IsNullOrEmpty(userId) || !ObjectIdValidator.IsValidObjectId(userId))
            {
                throw new ArgumentException("Invalid user id format.");
            }
            // Validate currentUserId format before querying Mongo to avoid parse errors
            if (!string.IsNullOrEmpty(currentUserId) && !ObjectIdValidator.IsValidObjectId(currentUserId))
            {
                throw new ArgumentException("Invalid current user id format.");
            }
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            var filterBuilder = Builders<Wishlist>.Filter;
            // Filter out wishlists with invalid UserIds to prevent ObjectId errors
            var filter = filterBuilder.And(
                filterBuilder.Eq(w => w.UserId, userId),
                filterBuilder.Exists(w => w.UserId),
                filterBuilder.Ne(w => w.UserId, null),
                filterBuilder.Ne(w => w.UserId, ""),
                filterBuilder.Not(filterBuilder.Regex(w => w.UserId, new MongoDB.Bson.BsonRegularExpression("^[a-zA-Z]+$"))) // Exclude usernames like 'Sakyu'
            );

            // If not the owner and not public, apply privacy filter
            // Also validate that currentUserId is a valid ObjectId before checking allowed viewers
            var isValidCurrentUserId = !string.IsNullOrEmpty(currentUserId) && ObjectIdValidator.IsValidObjectId(currentUserId);
            var isAllowedViewer = isValidCurrentUserId && (user.AllowedViewerIds?.Contains(currentUserId) ?? false);
            
            if (userId != currentUserId && !isAllowedViewer)
            {
                filter &= filterBuilder.Eq(w => w.IsPublic, true);
            }

			var wishlists = await _dbContext.Wishlists.Find(filter)
                                        .SortByDescending(w => w.CreatedAt)
                                        .Skip((page - 1) * pageSize)
                                        .Limit(pageSize)
                                        .ToListAsync();

            var wishlistDTOs = new List<WishlistFeedDTO>();
            foreach (var w in wishlists)
            {
                // Validate wishlist UserId before using it
                if (!ObjectIdValidator.IsValidObjectId(w.UserId))
                {
                    Console.WriteLine($"Skipping wishlist {w.Id} with invalid UserId: '{w.UserId}'");
                    continue;
                }
                
                wishlistDTOs.Add(new WishlistFeedDTO
                {
                    Id = w.Id,
                    UserId = w.UserId,
                    Username = user.Username,
                    AvatarUrl = user.AvatarUrl,
                    Title = w.Title,
                    Description = w.Description,
                    Category = w.Category,
                    IsPublic = w.IsPublic,
                    CreatedAt = w.CreatedAt
                });
            }
            return wishlistDTOs;
        }

		public async Task<List<WishlistFeedDTO>> GetFeedAsync(string currentUserId, int page, int pageSize)
        {
            // Validate currentUserId format before using it
            if (!string.IsNullOrEmpty(currentUserId) && !ObjectIdValidator.IsValidObjectId(currentUserId))
            {
                // If currentUserId is not a valid ObjectId (e.g., it's a username), 
                // just return public feed without user-specific data
                Console.WriteLine($"Invalid user ID format in feed: '{currentUserId}' - treating as anonymous user");
                currentUserId = string.Empty;
            }

            var user = await _dbContext.Users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
            // If user not found, treat as no relationships
            var followingIds = user?.FollowingIds ?? new List<string>();
            var followerIds = user?.FollowerIds ?? new List<string>();
            
            // Filter out invalid ObjectIds to prevent MongoDB errors
            var validFollowingIds = followingIds.Where(id => ObjectIdValidator.IsValidObjectId(id)).ToList();
            
            // Log any invalid IDs found for debugging
            var invalidIds = followingIds.Where(id => !ObjectIdValidator.IsValidObjectId(id)).ToList();
            if (invalidIds.Any())
            {
                Console.WriteLine($"Found invalid ObjectIds in following list: {string.Join(", ", invalidIds)}");
            }
            
            // Build friend list = mutual follow (both following each other)
            var validFollowerIds = followerIds.Where(id => ObjectIdValidator.IsValidObjectId(id)).ToList();
            var friends = validFollowingIds.Intersect(validFollowerIds).ToList();

            var filterBuilder = Builders<Wishlist>.Filter;
            // New policy: Feed only shows posts from friends or people you follow, plus your own
            var candidates = new List<string>();
            candidates.AddRange(validFollowingIds);
            candidates.AddRange(friends);
            if (!string.IsNullOrEmpty(currentUserId) && ObjectIdValidator.IsValidObjectId(currentUserId))
            {
                candidates.Add(currentUserId);
            }
            candidates = candidates.Distinct().ToList();

            // If no relationships, return empty feed
            if (!candidates.Any())
            {
                return new List<WishlistFeedDTO>();
            }

            // Only include wishlists from candidate users; respect privacy: public or allowed to current user
            // We cannot easily evaluate AllowedViewerIds in a single query if it's an array; include both public and private from candidates
            var filter = filterBuilder.And(
                filterBuilder.In(w => w.UserId, candidates),
                filterBuilder.Exists(w => w.UserId),
                filterBuilder.Ne(w => w.UserId, null),
                filterBuilder.Ne(w => w.UserId, ""),
                filterBuilder.Not(filterBuilder.Regex(w => w.UserId, new MongoDB.Bson.BsonRegularExpression("^[a-zA-Z]+$")))
            );

            var cacheKey = $"wishlist:feed:v2:{currentUserId}:{page}:{pageSize}";
			return await _cache.GetOrSetAsync(cacheKey, async () =>
			{
				var feedWishlists = await _dbContext.Wishlists.Find(filter)
                                             .SortByDescending(w => w.CreatedAt)
                                             .Skip((page - 1) * pageSize)
                                             .Limit(pageSize)
                                             .ToListAsync();
                                             
            Console.WriteLine($"Found {feedWishlists.Count} wishlists for feed");
            
            // Log any corrupted wishlists found for debugging
            await LogCorruptedWishlistsAsync();

            var feedDTOs = new List<WishlistFeedDTO>();
            foreach (var w in feedWishlists)
            {
                // Validate wishlist UserId before querying Users collection
                if (!ObjectIdValidator.IsValidObjectId(w.UserId))
                {
                    Console.WriteLine($"Skipping wishlist {w.Id} with invalid UserId: '{w.UserId}'");
                    continue;
                }
                
                var owner = await _dbContext.Users.Find(u => u.Id == w.UserId).FirstOrDefaultAsync();
                if (owner == null) continue; // Skip if owner not found
                var likeCount = (int)await _dbContext.Likes.CountDocumentsAsync(l => l.WishlistId == w.Id);
                var commentCount = (int)await _dbContext.Comments.CountDocumentsAsync(c => c.WishlistId == w.Id);
                var isLiked = !string.IsNullOrEmpty(currentUserId)
                    && await _dbContext.Likes.Find(l => l.WishlistId == w.Id && l.UserId == currentUserId).AnyAsync();

                feedDTOs.Add(new WishlistFeedDTO
                {
                    Id = w.Id,
                    UserId = w.UserId,
                    Title = w.Title,
                    Description = w.Description,
                    Category = w.Category,
                    IsPublic = w.IsPublic,
                    CreatedAt = w.CreatedAt,
                    Username = owner.Username,
                    AvatarUrl = owner.AvatarUrl,
                    LikeCount = likeCount,
                    CommentCount = commentCount,
                    IsLiked = isLiked
                });
            }
				return feedDTOs;
			}, TimeSpan.FromSeconds(30))!;
        }

        public async Task<bool> LikeWishlistAsync(string id, string currentUserId)
        {
            var wishlist = await _dbContext.Wishlists.Find(w => w.Id == id).FirstOrDefaultAsync();
            if (wishlist == null) throw new KeyNotFoundException("Wishlist not found.");

            var existingLike = await _dbContext.Likes.Find(l => l.WishlistId == id && l.UserId == currentUserId).FirstOrDefaultAsync();
            if (existingLike != null) throw new InvalidOperationException("Wishlist already liked.");

            var like = new Like
            {
                WishlistId = id,
                UserId = currentUserId,
                CreatedAt = DateTime.UtcNow
            };
            await _dbContext.Likes.InsertOneAsync(like);

			await _cache.RemoveAsync($"wishlist:detail:{id}:{currentUserId}");
			await _cache.RemoveAsync($"wishlist:feed:{currentUserId}:1:10");
			return true;
        }

        public async Task<bool> UnlikeWishlistAsync(string id, string currentUserId)
        {
            var wishlist = await _dbContext.Wishlists.Find(w => w.Id == id).FirstOrDefaultAsync();
            if (wishlist == null) throw new KeyNotFoundException("Wishlist not found.");

            var deleteResult = await _dbContext.Likes.DeleteOneAsync(l => l.WishlistId == id && l.UserId == currentUserId);

            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<CommentDTO> AddCommentAsync(string wishlistId, string userId, CreateCommentDTO commentDto)
        {
            var wishlist = await _dbContext.Wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync();
            if (wishlist == null) throw new KeyNotFoundException("Wishlist not found.");

            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            var comment = new Comment
            {
                WishlistId = wishlistId,
                UserId = userId,
                Text = commentDto.Text,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Comments.InsertOneAsync(comment);

            return new CommentDTO
            {
                Id = comment.Id,
                WishlistId = comment.WishlistId,
                UserId = comment.UserId,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };
        }

        public async Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, UpdateCommentDTO commentDto)
        {
            var comment = await _dbContext.Comments.Find(c => c.Id == commentId).FirstOrDefaultAsync();
            if (comment == null) throw new KeyNotFoundException("Comment not found.");

            if (comment.UserId != userId) throw new UnauthorizedAccessException("You are not authorized to update this comment.");

            comment.Text = commentDto.Text ?? comment.Text;
            comment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.Comments.ReplaceOneAsync(c => c.Id == commentId, comment);

            var user = await _dbContext.Users.Find(u => u.Id == comment.UserId).FirstOrDefaultAsync();

            return new CommentDTO
            {
                Id = comment.Id,
                WishlistId = comment.WishlistId,
                UserId = comment.UserId,
                Username = user?.Username ?? string.Empty,
                AvatarUrl = user?.AvatarUrl,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt
            };
        }

        public async Task<bool> DeleteCommentAsync(string commentId, string userId)
        {
            var comment = await _dbContext.Comments.Find(c => c.Id == commentId).FirstOrDefaultAsync();
            if (comment == null) return false;

            if (comment.UserId != userId) throw new UnauthorizedAccessException("You are not authorized to delete this comment.");

            var deleteResult = await _dbContext.Comments.DeleteOneAsync(c => c.Id == commentId);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<List<CommentDTO>> GetCommentsAsync(string wishlistId, int page, int pageSize)
        {
            var comments = await _dbContext.Comments.Find(c => c.WishlistId == wishlistId)
                                         .SortBy(c => c.CreatedAt)
                                         .Skip((page - 1) * pageSize)
                                         .Limit(pageSize)
                                         .ToListAsync();

            var commentDTOs = new List<CommentDTO>();
            foreach (var comment in comments)
            {
                var user = await _dbContext.Users.Find(u => u.Id == comment.UserId).FirstOrDefaultAsync();
                commentDTOs.Add(new CommentDTO
                {
                    Id = comment.Id,
                    WishlistId = comment.WishlistId,
                    UserId = comment.UserId,
                    Username = user?.Username ?? string.Empty,
                    AvatarUrl = user?.AvatarUrl,
                    Text = comment.Text,
                    CreatedAt = comment.CreatedAt,
                    UpdatedAt = comment.UpdatedAt
                });
            }
            return commentDTOs;
        }

        public async Task<string> UploadItemImageAsync(IFormFile file)
        {
            return await _cloudinaryService.UploadImageAsync(file);
        }

        private async Task LogCorruptedWishlistsAsync()
        {
            try
            {
                // Find wishlists with invalid UserIds (usernames instead of ObjectIds)
                // We need to fetch all wishlists and filter in C# since ObjectIdValidator can't be translated to MongoDB
                var allWishlists = await _dbContext.Wishlists.Find(_ => true).ToListAsync();
                var corruptedWishlists = allWishlists.Where(w => !ObjectIdValidator.IsValidObjectId(w.UserId)).ToList();
                
                if (corruptedWishlists.Any())
                {
                    Console.WriteLine($"Found {corruptedWishlists.Count} corrupted wishlists with invalid UserIds:");
                    foreach (var w in corruptedWishlists.Take(10)) // Log first 10
                    {
                        Console.WriteLine($"  - Wishlist {w.Id}: UserId='{w.UserId}', Title='{w.Title}'");
                    }
                    if (corruptedWishlists.Count > 10)
                    {
                        Console.WriteLine($"  ... and {corruptedWishlists.Count - 10} more");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging corrupted wishlists: {ex.Message}");
            }
        }

        public async Task<int> CleanupCorruptedWishlistsAsync()
        {
            try
            {
                // Find and delete wishlists with invalid UserIds
                // We need to fetch all wishlists and filter in C# since ObjectIdValidator can't be translated to MongoDB
                var allWishlists = await _dbContext.Wishlists.Find(_ => true).ToListAsync();
                var corruptedWishlists = allWishlists.Where(w => !ObjectIdValidator.IsValidObjectId(w.UserId)).ToList();
                
                if (!corruptedWishlists.Any())
                {
                    Console.WriteLine("No corrupted wishlists found.");
                    return 0;
                }

                Console.WriteLine($"Cleaning up {corruptedWishlists.Count} corrupted wishlists...");
                
                var deletedCount = 0;
                foreach (var wishlist in corruptedWishlists)
                {
                    try
                    {
                        // Delete the corrupted wishlist
                        var deleteResult = await _dbContext.Wishlists.DeleteOneAsync(w => w.Id == wishlist.Id);
                        if (deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0)
                        {
                            deletedCount++;
                            Console.WriteLine($"Deleted corrupted wishlist: {wishlist.Id} (UserId: '{wishlist.UserId}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting wishlist {wishlist.Id}: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Successfully cleaned up {deletedCount} corrupted wishlists.");
                return deletedCount;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                return 0;
            }
        }

        public async Task<List<WishlistFeedDTO>> GetLikedWishlistsAsync(string currentUserId, int page = 1, int pageSize = 20)
        {
            var cacheKey = $"wishlist:liked:{currentUserId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                // Get all likes for the current user
                var userLikes = await _dbContext.Likes
                    .Find(l => l.UserId == currentUserId)
                    .SortByDescending(l => l.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                if (!userLikes.Any())
                    return new List<WishlistFeedDTO>();

                // Get the wishlist IDs from the likes
                var wishlistIds = userLikes.Select(l => l.WishlistId).ToList();

                // Get the wishlists
                var wishlists = await _dbContext.Wishlists
                    .Find(w => wishlistIds.Contains(w.Id))
                    .ToListAsync();

                // Get the owners of these wishlists
                var ownerIds = wishlists.Select(w => w.UserId).Distinct().ToList();
                var owners = await _dbContext.Users
                    .Find(u => ownerIds.Contains(u.Id))
                    .ToListAsync();

                var feedDTOs = new List<WishlistFeedDTO>();

                foreach (var w in wishlists)
                {
                    var owner = owners.FirstOrDefault(o => o.Id == w.UserId);
                    if (owner == null) continue;

                    // Get like count for this wishlist
                    var likeCount = await _dbContext.Likes.CountDocumentsAsync(l => l.WishlistId == w.Id);

                    // Get comment count for this wishlist
                    var commentCount = await _dbContext.Comments.CountDocumentsAsync(c => c.WishlistId == w.Id);

                    // This wishlist is liked by the current user (since we're in the liked section)
                    var isLiked = true;

                    feedDTOs.Add(new WishlistFeedDTO
                    {
                        Id = w.Id,
                        UserId = w.UserId,
                        Title = w.Title,
                        Description = w.Description,
                        Category = w.Category,
                        IsPublic = w.IsPublic,
                        CreatedAt = w.CreatedAt,
                        Username = owner.Username,
                        AvatarUrl = owner.AvatarUrl,
                        LikeCount = (int)likeCount,
                        CommentCount = (int)commentCount,
                        IsLiked = isLiked
                    });
                }

                // Sort by the order of likes (most recent first)
                var sortedDTOs = new List<WishlistFeedDTO>();
                foreach (var like in userLikes)
                {
                    var dto = feedDTOs.FirstOrDefault(d => d.Id == like.WishlistId);
                    if (dto != null)
                    {
                        sortedDTOs.Add(dto);
                    }
                }

                return sortedDTOs;
            }, TimeSpan.FromSeconds(30))!;
        }
    }
}
