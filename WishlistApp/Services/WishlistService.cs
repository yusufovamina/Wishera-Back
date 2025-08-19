using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MongoDB.Driver;
using WishlistApp.DTO;
using WishlistApp.Models;

namespace WishlistApp.Services
{
    public interface IWishlistService
    {
        Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto);
        Task<WishlistResponseDTO> GetWishlistAsync(string wishlistId, string currentUserId);
        Task<WishlistResponseDTO> UpdateWishlistAsync(string wishlistId, string userId, UpdateWishlistDTO updateDto);
        Task<bool> DeleteWishlistAsync(string wishlistId, string userId);
        Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<WishlistFeedDTO>> GetFeedAsync(string userId, int page = 1, int pageSize = 20);
        Task<string> UploadItemImageAsync(IFormFile file);
        Task<bool> LikeWishlistAsync(string wishlistId, string userId);
        Task<bool> UnlikeWishlistAsync(string wishlistId, string userId);
        Task<CommentDTO> AddCommentAsync(string wishlistId, string userId, CreateCommentDTO commentDto);
        Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, UpdateCommentDTO commentDto);
        Task<bool> DeleteCommentAsync(string commentId, string userId);
        Task<List<CommentDTO>> GetCommentsAsync(string wishlistId, int page = 1, int pageSize = 20);
    }

    public class WishlistService : IWishlistService
    {
        private readonly IMongoCollection<Wishlist> _wishlists;
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Like> _likes;
        private readonly IMongoCollection<Comment> _comments;
        private readonly IMongoCollection<FeedEvent> _feed;
        private readonly Cloudinary _cloudinary;

        public WishlistService(IMongoDatabase database, Cloudinary cloudinary)
        {
            _wishlists = database.GetCollection<Wishlist>("Wishlists");
            _users = database.GetCollection<User>("Users");
            _likes = database.GetCollection<Like>("Likes");
            _comments = database.GetCollection<Comment>("Comments");
            _feed = database.GetCollection<FeedEvent>("Feed");
            _cloudinary = cloudinary;
        }

        public async Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto)
        {
            var wishlist = new Wishlist
            {
                UserId = userId,
                Title = createDto.Title,
                Description = createDto.Description ?? string.Empty,
                Category = createDto.Category ?? string.Empty,
                IsPublic = createDto.IsPublic,
                AllowedViewerIds = createDto.AllowedViewerIds ?? new List<string>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _wishlists.InsertOneAsync(wishlist);

            // Add to user's wishlists
            var userUpdate = Builders<User>.Update.AddToSet(u => u.WishlistIds, wishlist.Id);
            await _users.UpdateOneAsync(u => u.Id == userId, userUpdate);

            // Create feed event
            var feedEvent = new FeedEvent
            {
                UserId = userId,
                ActionType = "created_wishlist",
                WishlistId = wishlist.Id,
                WishlistTitle = wishlist.Title,
                CreatedAt = DateTime.UtcNow
            };
            await _feed.InsertOneAsync(feedEvent);

            return await GetWishlistAsync(wishlist.Id, userId);
        }

        public async Task<WishlistResponseDTO> GetWishlistAsync(string wishlistId, string currentUserId)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            // Check visibility
            if (!wishlist.IsPublic && wishlist.UserId != currentUserId && !wishlist.AllowedViewerIds.Contains(currentUserId))
                throw new UnauthorizedAccessException("You don't have permission to view this wishlist");

            var user = await _users.Find(u => u.Id == wishlist.UserId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var isLiked = await _likes.Find(l => l.UserId == currentUserId && l.WishlistId == wishlistId).AnyAsync();

            return new WishlistResponseDTO
            {
                Id = wishlist.Id,
                UserId = wishlist.UserId,
                Username = user.Username ?? throw new InvalidOperationException("Username cannot be null"),
                Title = wishlist.Title,
                Description = wishlist.Description,
                Category = wishlist.Category,
                IsPublic = wishlist.IsPublic,
                Items = wishlist.Items.Select(i => new WishlistItemDTO
                {
                    Title = i.Title,
                    Description = i.Description,
                    ImageUrl = i.ImageUrl,
                    Category = i.Category,
                    Price = i.Price,
                    Url = i.Url
                }).ToList(),
                CreatedAt = wishlist.CreatedAt,
                UpdatedAt = wishlist.UpdatedAt,
                LikeCount = wishlist.LikeCount,
                CommentCount = wishlist.CommentCount,
                IsLiked = isLiked,
                IsOwner = wishlist.UserId == currentUserId
            };
        }

        public async Task<WishlistResponseDTO> UpdateWishlistAsync(string wishlistId, string userId, UpdateWishlistDTO updateDto)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            if (wishlist.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update this wishlist");

            var update = Builders<Wishlist>.Update
                .Set(w => w.Title, updateDto.Title)
                .Set(w => w.Description, updateDto.Description)
                .Set(w => w.Category, updateDto.Category)
                .Set(w => w.IsPublic, updateDto.IsPublic)
                .Set(w => w.AllowedViewerIds, updateDto.AllowedViewerIds)
                .Set(w => w.UpdatedAt, DateTime.UtcNow);

            await _wishlists.UpdateOneAsync(w => w.Id == wishlistId, update);

            return await GetWishlistAsync(wishlistId, userId);
        }

        public async Task<bool> DeleteWishlistAsync(string wishlistId, string userId)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            if (wishlist.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to delete this wishlist");

            // Delete associated likes and comments
            await Task.WhenAll(
                _likes.DeleteManyAsync(l => l.WishlistId == wishlistId),
                _comments.DeleteManyAsync(c => c.WishlistId == wishlistId),
                _feed.DeleteManyAsync(f => f.WishlistId == wishlistId)
            );

            // Remove from user's wishlists
            var userUpdate = Builders<User>.Update.Pull(u => u.WishlistIds, wishlistId);
            await _users.UpdateOneAsync(u => u.Id == userId, userUpdate);

            var result = await _wishlists.DeleteOneAsync(w => w.Id == wishlistId);
            return result.DeletedCount > 0;
        }

        public async Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var filter = Builders<Wishlist>.Filter.And(
                Builders<Wishlist>.Filter.In("_id", user.WishlistIds.Select(id => MongoDB.Bson.ObjectId.Parse(id))),
                Builders<Wishlist>.Filter.Or(
                    Builders<Wishlist>.Filter.Eq("IsPublic", true),
                    Builders<Wishlist>.Filter.Eq("UserId", currentUserId),
                    Builders<Wishlist>.Filter.In("AllowedViewerIds", new[] { currentUserId })
                )
            );

            var wishlists = await _wishlists.Find(filter)
                .Sort(Builders<Wishlist>.Sort.Descending(w => w.CreatedAt))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var likedWishlistIds = await _likes.Find(l => l.UserId == currentUserId)
                .Project(l => l.WishlistId)
                .ToListAsync();

            return wishlists.Select(w => new WishlistFeedDTO
            {
                Id = w.Id,
                UserId = w.UserId,
                Username = user.Username!,
                AvatarUrl = user.AvatarUrl,
                Title = w.Title,
                Description = w.Description,
                Category = w.Category,
                CreatedAt = w.CreatedAt,
                LikeCount = w.LikeCount,
                CommentCount = w.CommentCount,
                IsLiked = likedWishlistIds.Contains(w.Id)
            }).ToList();
        }

        public async Task<List<WishlistFeedDTO>> GetFeedAsync(string userId, int page = 1, int pageSize = 20)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var followingIds = user.FollowingIds;
            followingIds.Add(userId); // Include user's own activities

            var feedEvents = await _feed.Find(f => followingIds.Contains(f.UserId))
                .Sort(Builders<FeedEvent>.Sort.Descending(f => f.CreatedAt))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var wishlistIds = feedEvents
                .Where(f => f.WishlistId != null)
                .Select(f => f.WishlistId)
                .Distinct()
                .ToList();

            var wishlists = await _wishlists.Find(w => wishlistIds.Contains(w.Id))
                .ToListAsync();

            var users = await _users.Find(u => followingIds.Contains(u.Id))
                .ToListAsync();

            var likedWishlistIds = await _likes.Find(l => l.UserId == userId)
                .Project(l => l.WishlistId)
                .ToListAsync();

            return feedEvents
                .Where(f => f.WishlistId != null)
                .Select(f =>
                {
                    var wishlist = wishlists.FirstOrDefault(w => w.Id == f.WishlistId);
                    var user = users.FirstOrDefault(u => u.Id == f.UserId);

                    return new WishlistFeedDTO
                    {
                        Id = f.WishlistId!,
                        UserId = f.UserId,
                        Username = user?.Username ?? "Unknown User",
                        AvatarUrl = user?.AvatarUrl,
                        Title = f.WishlistTitle,
                        Description = wishlist?.Description,
                        Category = wishlist?.Category,
                        CreatedAt = f.CreatedAt,
                        LikeCount = wishlist?.LikeCount ?? 0,
                        CommentCount = wishlist?.CommentCount ?? 0,
                        IsLiked = likedWishlistIds.Contains(f.WishlistId!)
                    };
                })
                .ToList();
        }

        public async Task<string> UploadItemImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Transformation = new Transformation().Width(800).Height(800).Crop("limit")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl.ToString();
        }

        public async Task<bool> LikeWishlistAsync(string wishlistId, string userId)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            if (!wishlist.IsPublic && wishlist.UserId != userId && !wishlist.AllowedViewerIds.Contains(userId))
                throw new UnauthorizedAccessException("You don't have permission to like this wishlist");

            var like = new Like
            {
                UserId = userId,
                WishlistId = wishlistId,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _likes.InsertOneAsync(like);

                // Update wishlist like count
                var update = Builders<Wishlist>.Update
                    .Inc(w => w.LikeCount, 1)
                    .AddToSet(w => w.LikeIds, like.Id);
                await _wishlists.UpdateOneAsync(w => w.Id == wishlistId, update);

                // Create feed event
                var feedEvent = new FeedEvent
                {
                    UserId = userId,
                    ActionType = "liked_wishlist",
                    WishlistId = wishlistId,
                    WishlistTitle = wishlist.Title,
                    CreatedAt = DateTime.UtcNow
                };
                await _feed.InsertOneAsync(feedEvent);

                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> UnlikeWishlistAsync(string wishlistId, string userId)
        {
            var result = await _likes.DeleteOneAsync(l =>
                l.UserId == userId && l.WishlistId == wishlistId);

            if (result.DeletedCount > 0)
            {
                // Update wishlist like count
                var update = Builders<Wishlist>.Update
                    .Inc(w => w.LikeCount, -1)
                    .Pull(w => w.LikeIds, result.DeletedCount.ToString());
                await _wishlists.UpdateOneAsync(w => w.Id == wishlistId, update);

                return true;
            }

            return false;
        }

        public async Task<CommentDTO> AddCommentAsync(string wishlistId, string userId, CreateCommentDTO commentDto)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            if (!wishlist.IsPublic && wishlist.UserId != userId && !wishlist.AllowedViewerIds.Contains(userId))
                throw new UnauthorizedAccessException("You don't have permission to comment on this wishlist");

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var comment = new Comment
            {
                UserId = userId,
                WishlistId = wishlistId,
                Text = commentDto.Text,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _comments.InsertOneAsync(comment);

            // Update wishlist comment count
            var update = Builders<Wishlist>.Update
                .Inc(w => w.CommentCount, 1)
                .AddToSet(w => w.CommentIds, comment.Id);
            await _wishlists.UpdateOneAsync(w => w.Id == wishlistId, update);

            // Create feed event
            var feedEvent = new FeedEvent
            {
                UserId = userId,
                ActionType = "commented",
                WishlistId = wishlistId,
                WishlistTitle = wishlist.Title,
                CommentId = comment.Id,
                CommentText = comment.Text,
                CreatedAt = DateTime.UtcNow
            };
            await _feed.InsertOneAsync(feedEvent);

            return new CommentDTO
            {
                Id = comment.Id,
                UserId = userId,
                Username = user.Username,
                AvatarUrl = user.AvatarUrl,
                WishlistId = wishlistId,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                IsEdited = false,
                IsOwner = true
            };
        }

        public async Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, UpdateCommentDTO commentDto)
        {
            var comment = await _comments.Find(c => c.Id == commentId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Comment not found");

            if (comment.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to update this comment");

            var update = Builders<Comment>.Update
                .Set(c => c.Text, commentDto.Text)
                .Set(c => c.UpdatedAt, DateTime.UtcNow)
                .Set(c => c.IsEdited, true);

            await _comments.UpdateOneAsync(c => c.Id == commentId, update);

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();

            return new CommentDTO
            {
                Id = comment.Id,
                UserId = userId,
                Username = user?.Username ?? "Unknown",
                AvatarUrl = user?.AvatarUrl ?? string.Empty,
                WishlistId = comment.WishlistId,
                Text = commentDto.Text,
                CreatedAt = comment.CreatedAt,
                IsEdited = true,
                IsOwner = true
            };
        }

        public async Task<bool> DeleteCommentAsync(string commentId, string userId)
        {
            var comment = await _comments.Find(c => c.Id == commentId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Comment not found");

            if (comment.UserId != userId)
                throw new UnauthorizedAccessException("You don't have permission to delete this comment");

            var result = await _comments.DeleteOneAsync(c => c.Id == commentId);

            if (result.DeletedCount > 0)
            {
                // Update wishlist comment count
                var update = Builders<Wishlist>.Update
                    .Inc(w => w.CommentCount, -1)
                    .Pull(w => w.CommentIds, commentId);
                await _wishlists.UpdateOneAsync(w => w.Id == comment.WishlistId, update);

                return true;
            }

            return false;
        }

        public async Task<List<CommentDTO>> GetCommentsAsync(string wishlistId, int page = 1, int pageSize = 20)
        {
            var wishlist = await _wishlists.Find(w => w.Id == wishlistId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Wishlist not found");

            var comments = await _comments.Find(c => c.WishlistId == wishlistId)
                .Sort(Builders<Comment>.Sort.Descending(c => c.CreatedAt))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var userIds = comments.Select(c => c.UserId).Distinct().ToList();
            var users = await _users.Find(u => userIds.Contains(u.Id))
                .ToListAsync();

            return comments.Select(c =>
            {
                var user = users.FirstOrDefault(u => u.Id == c.UserId);
                return new CommentDTO
                {
                    Id = c.Id,
                    UserId = c.UserId,
                    Username = user?.Username ?? "Unknown",
                    AvatarUrl = user?.AvatarUrl ?? string.Empty,
                    WishlistId = c.WishlistId,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    IsEdited = c.IsEdited,
                    IsOwner = false // This will be set by the controller based on the current user
                };
            }).ToList();
        }
    }
} 