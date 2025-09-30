using MongoDB.Driver;
using WisheraApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using WisheraApp.DTO;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;

namespace user_service.Services
{
	public class UserService : IUserService
	{
		private readonly MongoDbContext _dbContext;
		private readonly ICloudinaryService _cloudinaryService;
		private readonly ICacheService _cache;

		public UserService(MongoDbContext dbContext, ICloudinaryService cloudinaryService, ICacheService cache)
		{
			_dbContext = dbContext;
			_cloudinaryService = cloudinaryService;
			_cache = cache;
		}

		private bool IsValidObjectId(string id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

		public async Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			if (!IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
			var cacheKey = $"user:profile:{userId}:{currentUserId}";
			return await _cache.GetOrSetAsync(cacheKey, async () =>
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
				Id = user.Id,
				Username = user.Username,
				Email = user.Email,
				Bio = isProfilePublic ? user.Bio : null,
				Interests = isProfilePublic ? user.Interests : new List<string>(),
				AvatarUrl = user.AvatarUrl,
				FollowersCount = followersCount,
				FollowingCount = followingCount,
				IsFollowing = isFollowing,
				IsPrivate = user.IsPrivate,
				WishlistCount = user.WishlistIds?.Count ?? 0
			};
				return profile;
			}, TimeSpan.FromMinutes(5))
			?? throw new Exception("Failed to build profile");
		}

		public async Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
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
			user.IsPrivate = updateDto.IsPrivate;

			var updateDefinition = Builders<User>.Update
				.Set(u => u.Username, user.Username)
				.Set(u => u.Bio, user.Bio)
				.Set(u => u.Interests, user.Interests)
				.Set(u => u.IsPrivate, user.IsPrivate)
				.Set(u => u.AllowedViewerIds, user.AllowedViewerIds);

			await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, updateDefinition);
			await _cache.RemoveAsync($"user:profile:{userId}:{userId}");

			return await GetUserProfileAsync(userId, userId); // Fetch updated profile
		}

		public async Task<string> UpdateAvatarAsync(string userId, IFormFile file)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
			if (user == null)
				throw new KeyNotFoundException("User not found");

			var imageUrl = await _cloudinaryService.UploadImageAsync(file);
			user.AvatarUrl = imageUrl;

			var updateDefinition = Builders<User>.Update.Set(u => u.AvatarUrl, imageUrl);
			await _dbContext.Users.UpdateOneAsync(u => u.Id == userId, updateDefinition);
			await _cache.RemoveAsync($"user:profile:{userId}:{userId}");

			return imageUrl;
		}

		public async Task<bool> FollowUserAsync(string followerId, string followingId)
		{
			if (!IsValidObjectId(followerId)) throw new ArgumentException("Invalid follower ID format.");
			if (!IsValidObjectId(followingId)) throw new ArgumentException("Invalid following ID format.");
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
			if (!IsValidObjectId(followerId)) throw new ArgumentException("Invalid follower ID format.");
			if (!IsValidObjectId(followingId)) throw new ArgumentException("Invalid following ID format.");
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
			if (!string.IsNullOrEmpty(currentUserId) && !IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
			
			// Use regex search instead of text search to avoid requiring text index
			var filter = Builders<User>.Filter.Regex(u => u.Username, new MongoDB.Bson.BsonRegularExpression(query, "i"));
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
					Id = user.Id,
					Username = user.Username,
					AvatarUrl = user.AvatarUrl,
					IsFollowing = isFollowing,
					MutualFriendsCount = 0 // Not calculated for search results
				});
			}
			return searchResults;
		}

		public async Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page, int pageSize)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			if (!string.IsNullOrEmpty(currentUserId) && !IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
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
					Id = f.Id,
					Username = f.Username,
					AvatarUrl = f.AvatarUrl,
					IsFollowing = currentUserId != null && f.FollowerIds.Contains(currentUserId),
					MutualFriendsCount = 0 // Not calculated for followers
				});
			}
			return followerDTOs;
		}

		public async Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page, int pageSize)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			if (!string.IsNullOrEmpty(currentUserId) && !IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
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
					Id = f.Id,
					Username = f.Username,
					AvatarUrl = f.AvatarUrl,
					IsFollowing = currentUserId != null && f.FollowerIds.Contains(currentUserId),
					MutualFriendsCount = 0 // Not calculated for following
				});
			}
			return followingDTOs;
		}

		public async Task<List<UserSearchDTO>> GetSuggestedUsersAsync(string currentUserId, int page, int pageSize)
		{
			if (!IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
			
			var currentUser = await _dbContext.Users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
			if (currentUser == null) throw new KeyNotFoundException("Current user not found.");

			// Get users that the current user is not already following
			var followingIds = currentUser.FollowingIds ?? new List<string>();
			var excludeIds = new List<string>(followingIds) { currentUserId }; // Exclude self and already following

			// Find users who are not in the exclude list
			var filter = Builders<User>.Filter.And(
				Builders<User>.Filter.Nin(u => u.Id, excludeIds),
				Builders<User>.Filter.Eq(u => u.IsPrivate, false) // Only suggest public users
			);

			var suggestedUsers = await _dbContext.Users.Find(filter)
				.Skip((page - 1) * pageSize)
				.Limit(pageSize)
				.ToListAsync();

			var suggestedDTOs = new List<UserSearchDTO>();
			foreach (var user in suggestedUsers)
			{
				// Calculate mutual friends count
				var mutualFriendsCount = 0;
				if (currentUser.FollowingIds != null && user.FollowerIds != null)
				{
					mutualFriendsCount = currentUser.FollowingIds.Intersect(user.FollowerIds).Count();
				}

				suggestedDTOs.Add(new UserSearchDTO
				{
					Id = user.Id,
					Username = user.Username,
					AvatarUrl = user.AvatarUrl,
					IsFollowing = false, // They're not following since we excluded them
					MutualFriendsCount = mutualFriendsCount
				});
			}

			// Sort by mutual friends count (descending) and then by username
			return suggestedDTOs.OrderByDescending(u => {
				var user = suggestedUsers.First(su => su.Id == u.Id);
				return currentUser.FollowingIds?.Intersect(user.FollowerIds ?? new List<string>()).Count() ?? 0;
			}).ThenBy(u => u.Username).ToList();
		}

		public async Task<bool> UserExistsAsync(string userId)
		{
			if (!IsValidObjectId(userId)) return false; // Invalid format, so user can't exist
			return await _dbContext.Users.Find(u => u.Id == userId).AnyAsync();
		}

		public async Task<User> GetUserByIdAsync(string userId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			return await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync()
				?? throw new KeyNotFoundException("User not found");
		}
	}
}
