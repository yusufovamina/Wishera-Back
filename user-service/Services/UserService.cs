using MongoDB.Driver;
using WishlistApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using WishlistApp.DTO;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;

namespace user_service.Services
{
    public interface IUserService
    {
        Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId);
        Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto);
        Task<string> UpdateAvatarAsync(string userId, IFormFile file);
        Task<bool> FollowUserAsync(string followerId, string followingId);
        Task<bool> UnfollowUserAsync(string followerId, string followingId);
        Task<List<UserSearchDTO>> SearchUsersAsync(string query, string currentUserId, int page, int pageSize);
        Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page, int pageSize);
        Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page, int pageSize);
        Task<bool> UserExistsAsync(string userId);
        Task<User> GetUserByIdAsync(string userId);
    }

    public class UserService : IUserService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICloudinaryService _cloudinaryService;

        public UserService(MongoDbContext dbContext, ICloudinaryService cloudinaryService)
        {
            _dbContext = dbContext;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Check if current user is following the target user
            var isFollowing = await _dbContext.Relationships.Find(
                r => r.FollowerId == currentUserId && r.FollowingId == userId
            ).AnyAsync();

            // Populate follower and following counts directly from the user object
            var followersCount = user.FollowerIds?.Count ?? 0;
            var followingCount = user.FollowingIds?.Count ?? 0;

            // Determine if the profile should be public
            bool isProfilePublic = !user.IsPrivate || user.Id == currentUserId || user.AllowedViewerIds.Contains(currentUserId);

            var profile = new UserProfileDTO
            {
                UserId = user.Id,
                Username = user.Username,
                Email = isProfilePublic ? user.Email : null, // Only show email if public
                Bio = isProfilePublic ? user.Bio : null,
                Interests = isProfilePublic ? user.Interests : new List<string>(),
                AvatarUrl = user.AvatarUrl,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                IsFollowing = isFollowing,
                IsPrivate = user.IsPrivate,
                WishlistsCount = user.WishlistIds?.Count ?? 0
            };

            return profile;
        }

        public async Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (!string.IsNullOrEmpty(updateDto.Username) && updateDto.Username != user.Username)
            {
                if (await _dbContext.Users.Find(u => u.Username == updateDto.Username && u.Id != userId).AnyAsync())
                {
                    throw new ArgumentException("Username is already taken.");
                }
                user.Username = updateDto.Username;
            }

            user.Bio = updateDto.Bio ?? user.Bio;
            user.Interests = updateDto.Interests ?? user.Interests;
            user.IsPrivate = updateDto.IsPrivate ?? user.IsPrivate;
            user.AllowedViewerIds = updateDto.AllowedViewerIds ?? user.AllowedViewerIds;

            var updateDefinition = Builders<User>.Update
                .Set(u => u.Username, user.Username)
                .Set(u => u.Bio, user.Bio)
                .Set(u => u.Interests, user.Interests)
                .Set(u => u.IsPrivate, user.IsPrivate)
                .Set(u => u.AllowedViewerIds, user.AllowedViewerIds);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, updateDefinition);

            return await GetUserProfileAsync(userId, userId); // Fetch updated profile
        }

        public async Task<string> UpdateAvatarAsync(string userId, IFormFile file)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
                throw new KeyNotFoundException("User not found");

            var imageUrl = await _cloudinaryService.UploadImageAsync(file);
            user.AvatarUrl = imageUrl;

            var updateDefinition = Builders<User>.Update.Set(u => u.AvatarUrl, imageUrl);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, updateDefinition);

            return imageUrl;
        }

        public async Task<bool> FollowUserAsync(string followerId, string followingId)
        {
            if (followerId == followingId)
                throw new ArgumentException("Cannot follow yourself.");

            var follower = await _dbContext.Users.Find(u => u.Id == followerId).FirstOrDefaultAsync();
            var following = await _dbContext.Users.Find(u => u.Id == followingId).FirstOrDefaultAsync();

            if (follower == null || following == null)
                throw new KeyNotFoundException("User not found.");

            // Check if already following
            if (follower.FollowingIds.Contains(followingId))
                throw new InvalidOperationException("Already following this user.");

            // Add relationship
            var relationship = new Relationship
            {
                FollowerId = followerId,
                FollowingId = followingId,
                CreatedAt = DateTime.UtcNow
            };
            await _dbContext.Relationships.InsertOneAsync(relationship);

            // Update user's FollowingIds
            var updateFollower = Builders<User>.Update.AddToSet(u => u.FollowingIds, followingId);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == followerId, updateFollower);

            // Update target user's FollowerIds
            var updateFollowing = Builders<User>.Update.AddToSet(u => u.FollowerIds, followerId);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == followingId, updateFollowing);

            return true;
        }

        public async Task<bool> UnfollowUserAsync(string followerId, string followingId)
        {
            var follower = await _dbContext.Users.Find(u => u.Id == followerId).FirstOrDefaultAsync();
            var following = await _dbContext.Users.Find(u => u.Id == followingId).FirstOrDefaultAsync();

            if (follower == null || following == null)
                throw new KeyNotFoundException("User not found.");

            // Check if actually following
            if (!follower.FollowingIds.Contains(followingId))
                throw new InvalidOperationException("Not following this user.");

            // Remove relationship
            await _dbContext.Relationships.DeleteOneAsync(r => r.FollowerId == followerId && r.FollowingId == followingId);

            // Update user's FollowingIds
            var updateFollower = Builders<User>.Update.Pull(u => u.FollowingIds, followingId);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == followerId, updateFollower);

            // Update target user's FollowerIds
            var updateFollowing = Builders<User>.Update.Pull(u => u.FollowerIds, followerId);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == followingId, updateFollowing);

            return true;
        }

        public async Task<List<UserSearchDTO>> SearchUsersAsync(string query, string currentUserId, int page, int pageSize)
        {
            var filter = Builders<User>.Filter.Text(query);
            var users = await _dbContext.Users.Find(filter)
                                        .Skip((page - 1) * pageSize)
                                        .Limit(pageSize)
                                        .ToListAsync();

            var searchResults = new List<UserSearchDTO>();
            foreach (var user in users)
            {
                // Only show email if public or if current user is following (or is the user themselves)
                bool isProfilePublic = !user.IsPrivate || user.Id == currentUserId || user.AllowedViewerIds.Contains(currentUserId);
                var isFollowing = user.FollowerIds.Contains(currentUserId); // Check if current user is following this user

                searchResults.Add(new UserSearchDTO
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Email = isProfilePublic ? user.Email : null,
                    Bio = isProfilePublic ? user.Bio : null,
                    AvatarUrl = user.AvatarUrl,
                    IsFollowing = isFollowing
                });
            }
            return searchResults;
        }

        public async Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page, int pageSize)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            var followerIds = user.FollowerIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var followers = await _dbContext.Users.Find(u => followerIds.Contains(u.Id)).ToListAsync();

            var followerDTOs = new List<UserSearchDTO>();
            foreach (var f in followers)
            {
                bool isProfilePublic = !f.IsPrivate || f.Id == currentUserId || f.AllowedViewerIds.Contains(currentUserId);
                followerDTOs.Add(new UserSearchDTO
                {
                    UserId = f.Id,
                    Username = f.Username,
                    Email = isProfilePublic ? f.Email : null,
                    Bio = isProfilePublic ? f.Bio : null,
                    AvatarUrl = f.AvatarUrl,
                    IsFollowing = currentUserId != null && f.FollowerIds.Contains(currentUserId) // Is *current user* following this follower
                });
            }
            return followerDTOs;
        }

        public async Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page, int pageSize)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null) throw new KeyNotFoundException("User not found.");

            var followingIds = user.FollowingIds.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var following = await _dbContext.Users.Find(u => followingIds.Contains(u.Id)).ToListAsync();

            var followingDTOs = new List<UserSearchDTO>();
            foreach (var f in following)
            {
                bool isProfilePublic = !f.IsPrivate || f.Id == currentUserId || f.AllowedViewerIds.Contains(currentUserId);
                followingDTOs.Add(new UserSearchDTO
                {
                    UserId = f.Id,
                    Username = f.Username,
                    Email = isProfilePublic ? f.Email : null,
                    Bio = isProfilePublic ? f.Bio : null,
                    AvatarUrl = f.AvatarUrl,
                    IsFollowing = currentUserId != null && f.FollowerIds.Contains(currentUserId) // Is *current user* following this person
                });
            }
            return followingDTOs;
        }

        public async Task<bool> UserExistsAsync(string userId)
        {
            return await _dbContext.Users.Find(u => u.Id == userId).AnyAsync();
        }

        public async Task<User> GetUserByIdAsync(string userId)
        {
            return await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("User not found");
        }
    }
}
