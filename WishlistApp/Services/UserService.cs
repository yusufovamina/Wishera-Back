using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MongoDB.Driver;
using WishlistApp.DTO;
using WishlistApp.Models;

namespace WishlistApp.Services
{
    public interface IUserService
    {
        Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId);
        Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto);
        Task<string> UpdateAvatarAsync(string userId, IFormFile file);
        Task<bool> FollowUserAsync(string followerId, string followingId);
        Task<bool> UnfollowUserAsync(string followerId, string followingId);
        Task<List<UserSearchDTO>> SearchUsersAsync(string searchTerm, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
        Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page = 1, int pageSize = 20);
    }

    public class UserService : IUserService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<Relationship> _relationships;
        private readonly Cloudinary _cloudinary;

        public UserService(IMongoDatabase database, Cloudinary cloudinary)
        {
            _users = database.GetCollection<User>("Users");
            _relationships = database.GetCollection<Relationship>("Relationships");
            _cloudinary = cloudinary;
        }

        public async Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var isFollowing = await _relationships.Find(r => 
                r.FollowerId == currentUserId && r.FollowingId == userId).AnyAsync();

            return new UserProfileDTO
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Bio = user.Bio,
                Interests = user.Interests,
                AvatarUrl = user.AvatarUrl,
                CreatedAt = user.CreatedAt,
                FollowingCount = user.FollowingIds.Count,
                FollowersCount = user.FollowerIds.Count,
                WishlistCount = user.WishlistIds.Count,
                IsPrivate = user.IsPrivate,
                IsFollowing = isFollowing
            };
        }

        public async Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto)
        {
            var update = Builders<User>.Update
                .Set(u => u.Username, updateDto.Username)
                .Set(u => u.Bio, updateDto.Bio)
                .Set(u => u.Interests, updateDto.Interests)
                .Set(u => u.IsPrivate, updateDto.IsPrivate);

            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            if (result.MatchedCount == 0)
                throw new KeyNotFoundException("User not found");

            return await GetUserProfileAsync(userId, userId);
        }

        public async Task<string> UpdateAvatarAsync(string userId, IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("No file uploaded");

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Transformation = new Transformation().Width(200).Height(200).Crop("fill")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            var avatarUrl = uploadResult.SecureUrl.ToString();

            var update = Builders<User>.Update.Set(u => u.AvatarUrl, avatarUrl);
            await _users.UpdateOneAsync(u => u.Id == userId, update);

            return avatarUrl;
        }

        public async Task<bool> FollowUserAsync(string followerId, string followingId)
        {
            if (followerId == followingId)
                throw new InvalidOperationException("Cannot follow yourself");

            var following = await _users.Find(u => u.Id == followingId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User to follow not found");

            var relationship = new Relationship
            {
                FollowerId = followerId,
                FollowingId = followingId,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _relationships.InsertOneAsync(relationship);

                // Update following/followers lists
                var followerUpdate = Builders<User>.Update
                    .AddToSet(u => u.FollowingIds, followingId);
                var followingUpdate = Builders<User>.Update
                    .AddToSet(u => u.FollowerIds, followerId);

                await Task.WhenAll(
                    _users.UpdateOneAsync(u => u.Id == followerId, followerUpdate),
                    _users.UpdateOneAsync(u => u.Id == followingId, followingUpdate)
                );

                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        public async Task<bool> UnfollowUserAsync(string followerId, string followingId)
        {
            var result = await _relationships.DeleteOneAsync(r =>
                r.FollowerId == followerId && r.FollowingId == followingId);

            if (result.DeletedCount > 0)
            {
                // Update following/followers lists
                var followerUpdate = Builders<User>.Update
                    .Pull(u => u.FollowingIds, followingId);
                var followingUpdate = Builders<User>.Update
                    .Pull(u => u.FollowerIds, followerId);

                await Task.WhenAll(
                    _users.UpdateOneAsync(u => u.Id == followerId, followerUpdate),
                    _users.UpdateOneAsync(u => u.Id == followingId, followingUpdate)
                );

                return true;
            }

            return false;
        }

        public async Task<List<UserSearchDTO>> SearchUsersAsync(string searchTerm, string currentUserId, int page = 1, int pageSize = 20)
        {
            var filter = Builders<User>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));
            var users = await _users.Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var followingIds = await _relationships.Find(r => r.FollowerId == currentUserId)
                .Project(r => r.FollowingId)
                .ToListAsync();

            return users.Select(u => new UserSearchDTO
            {
                Id = u.Id,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                IsFollowing = followingIds.Contains(u.Id)
            }).ToList();
        }

        public async Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var followers = await _users.Find(u => user.FollowerIds.Contains(u.Id))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var followingIds = await _relationships.Find(r => r.FollowerId == currentUserId)
                .Project(r => r.FollowingId)
                .ToListAsync();

            return followers.Select(u => new UserSearchDTO
            {
                Id = u.Id,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                IsFollowing = followingIds.Contains(u.Id)
            }).ToList();
        }

        public async Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page = 1, int pageSize = 20)
        {
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");

            var following = await _users.Find(u => user.FollowingIds.Contains(u.Id))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var followingIds = await _relationships.Find(r => r.FollowerId == currentUserId)
                .Project(r => r.FollowingId)
                .ToListAsync();

            return following.Select(u => new UserSearchDTO
            {
                Id = u.Id,
                Username = u.Username,
                AvatarUrl = u.AvatarUrl,
                IsFollowing = followingIds.Contains(u.Id)
            }).ToList();
        }
    }
} 