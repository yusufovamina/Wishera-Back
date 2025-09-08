using MongoDB.Driver;
using WishlistApp.DTO;
using WishlistApp.Models;
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

   

    public class WishlistService : WishlistApp.Services.IWishlistService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICloudinaryService _cloudinaryService;

        public WishlistService(MongoDbContext dbContext, ICloudinaryService cloudinaryService)
        {
            _dbContext = dbContext;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            if (string.IsNullOrWhiteSpace(createDto.Title)) throw new ArgumentException("Title cannot be empty.");

            var wishlist = new Wishlist
            {
                UserId = userId,
                Title = createDto.Title,
                Description = createDto.Description,
                Category = createDto.Category,
                IsPublic = createDto.IsPublic,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.Wishlists.InsertOneAsync(wishlist);

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
                UpdatedAt = wishlist.UpdatedAt
            };
        }

        public async Task<WishlistResponseDTO> GetWishlistAsync(string id, string currentUserId)
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
                Username = user.Username, // Include username
                AvatarUrl = user.AvatarUrl // Include avatar
            };
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

            // Update user's wishlists list
            var update = Builders<User>.Update.Pull(u => u.WishlistIds, id);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == wishlist.UserId, update);

            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page, int pageSize)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            var filterBuilder = Builders<Wishlist>.Filter;
            var filter = filterBuilder.Eq(w => w.UserId, userId);

            // If not the owner and not public, apply privacy filter
            if (userId != currentUserId && !(user.AllowedViewerIds?.Contains(currentUserId) ?? false))
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
            var user = await _dbContext.Users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("Current user not found.");

            var followingIds = user.FollowingIds ?? new List<string>();
            followingIds.Add(currentUserId); // Include current user's own wishlists

            var filterBuilder = Builders<Wishlist>.Filter;
            var filter = filterBuilder.In(w => w.UserId, followingIds);
            filter |= filterBuilder.Eq(w => w.IsPublic, true);

            var feedWishlists = await _dbContext.Wishlists.Find(filter)
                                             .SortByDescending(w => w.CreatedAt)
                                             .Skip((page - 1) * pageSize)
                                             .Limit(pageSize)
                                             .ToListAsync();

            var feedDTOs = new List<WishlistFeedDTO>();
            foreach (var w in feedWishlists)
            {
                var owner = await _dbContext.Users.Find(u => u.Id == w.UserId).FirstOrDefaultAsync();
                if (owner == null) continue; // Skip if owner not found
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
                    AvatarUrl = owner.AvatarUrl
                });
            }
            return feedDTOs;
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
                Username = user?.Username, // Null-conditional operator
                AvatarUrl = user?.AvatarUrl, // Null-conditional operator
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
                    Username = user?.Username,
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
    }
}
