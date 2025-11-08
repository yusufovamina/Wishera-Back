using MongoDB.Driver;
using WisheraApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using WisheraApp.DTO;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using user_service.Controllers;

namespace user_service.Services
{
	public class UserService : IUserService
	{
		private readonly MongoDbContext _dbContext;
		private readonly ICloudinaryService _cloudinaryService;
		private readonly ICacheService _cache;
		private readonly INotificationService _notificationService;

		public UserService(MongoDbContext dbContext, ICloudinaryService cloudinaryService, ICacheService cache, INotificationService notificationService)
		{
			_dbContext = dbContext;
			_cloudinaryService = cloudinaryService;
			_cache = cache;
			_notificationService = notificationService;
		}

		private bool IsValidObjectId(string id) 
		{
			if (string.IsNullOrEmpty(id)) return false;
			// Allow 24-character hex strings (MongoDB ObjectId format)
			return id.Length == 24 && System.Text.RegularExpressions.Regex.IsMatch(id, @"^[0-9a-fA-F]{24}$");
		}

		public async Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			if (!IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid current user ID format.");
			
			// Build profile directly without cache if cache is slow/unavailable
			// Cache is optional - don't let it block the request
			try
			{
			var cacheKey = $"user:profile:{userId}:{currentUserId}";
				return await _cache.GetOrSetAsync(cacheKey, async () => await BuildUserProfileAsync(userId, currentUserId), TimeSpan.FromMinutes(5))
					?? await BuildUserProfileAsync(userId, currentUserId);
			}
			catch (Exception cacheEx)
			{
				// If cache fails, build profile directly
				Console.WriteLine($"Cache operation failed, building profile directly: {cacheEx.Message}");
				return await BuildUserProfileAsync(userId, currentUserId);
			}
		}

		private async Task<UserProfileDTO> BuildUserProfileAsync(string userId, string currentUserId)
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
			bool isProfilePublic = !user.IsPrivate || user.Id == currentUserId || (user.AllowedViewerIds != null && user.AllowedViewerIds.Contains(currentUserId));

			var profile = new UserProfileDTO
			{
				Id = user.Id,
				Username = user.Username,
				Email = user.Email,
				Bio = isProfilePublic ? user.Bio : null,
				Interests = isProfilePublic ? (user.Interests ?? new List<string>()) : new List<string>(),
				AvatarUrl = user.AvatarUrl ?? string.Empty,
				Birthday = isProfilePublic ? user.Birthday : null,
				CreatedAt = user.CreatedAt,
				FollowersCount = followersCount,
				FollowingCount = followingCount,
				IsFollowing = isFollowing,
				IsPrivate = user.IsPrivate,
				WishlistCount = user.WishlistIds?.Count ?? 0
			};
				return profile;
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
			
			// Handle birthday update
			if (!string.IsNullOrEmpty(updateDto.Birthday))
			{
				// Validate birthday format
				if (!DateTime.TryParse(updateDto.Birthday, out _))
					throw new ArgumentException("Invalid birthday format. Please use YYYY-MM-DD format.");
				user.Birthday = updateDto.Birthday;
			}

			var updateDefinition = Builders<User>.Update
				.Set(u => u.Username, user.Username)
				.Set(u => u.Bio, user.Bio)
				.Set(u => u.Interests, user.Interests)
				.Set(u => u.IsPrivate, user.IsPrivate)
				.Set(u => u.Birthday, user.Birthday)
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

			// Create friend request notification
			try
			{
				await _notificationService.CreateFriendRequestNotificationAsync(followingId, followerId);
			}
			catch (Exception ex)
			{
				// Log error but don't fail the follow operation
				Console.WriteLine($"Failed to create friend request notification: {ex.Message}");
			}

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

			// Delete friend-related notifications (both FriendRequest and FriendAccepted)
			try
			{
				// Delete FriendRequest notification (when someone followed you)
				await _notificationService.DeleteNotificationByTypeAndRelatedUserAsync(
					followingId, 
					Models.NotificationType.FriendRequest, 
					followerId
				);
				
				// Also delete FriendAccepted notification (if the followed user had followed back)
				await _notificationService.DeleteNotificationByTypeAndRelatedUserAsync(
					followerId, 
					Models.NotificationType.FriendAccepted, 
					followingId
				);
			}
			catch (Exception ex)
			{
				// Log error but don't fail the unfollow operation
				Console.WriteLine($"Failed to delete friend notifications: {ex.Message}");
			}

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

            var followingIds = currentUser.FollowingIds ?? new List<string>();
            var followerIds = currentUser.FollowerIds ?? new List<string>();
            var excludeIds = new HashSet<string>(followingIds) { currentUserId };

            // Candidate pool: public users not followed by current user
            var baseFilter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.IsPrivate, false),
                Builders<User>.Filter.Nin(u => u.Id, excludeIds.ToList())
            );

            // Pull a pool to score
            var candidatePool = await _dbContext.Users.Find(baseFilter)
                .Limit(Math.Max(pageSize * 10, 100))
                .ToListAsync();

            int Score(User u)
            {
                var uFollowerIds = u.FollowerIds ?? new List<string>();
                var uInterests = u.Interests ?? new List<string>();
                var myFollowing = followingIds ?? new List<string>();
                var myInterests = currentUser.Interests ?? new List<string>();

                var mutual = myFollowing.Intersect(uFollowerIds).Count();
                var sharedInterests = myInterests.Intersect(uInterests, StringComparer.OrdinalIgnoreCase).Count();
                var recencyDays = (DateTime.UtcNow - u.LastActive).TotalDays;
                var recencyScore = recencyDays <= 1 ? 3 : recencyDays <= 7 ? 2 : recencyDays <= 30 ? 1 : 0;
                return (mutual * 3) + (sharedInterests * 2) + recencyScore;
            }

            var ranked = candidatePool
                .Select(u => new { User = u, Score = Score(u), Mutual = (followingIds.Intersect(u.FollowerIds ?? new List<string>())).Count() })
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.User.Username)
                .ToList();

            var paged = ranked.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return paged.Select(x => new UserSearchDTO
            {
                Id = x.User.Id,
                Username = x.User.Username,
                AvatarUrl = x.User.AvatarUrl,
                IsFollowing = false,
                MutualFriendsCount = x.Mutual
            }).ToList();
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

		// Notification methods
		public async Task<List<BirthdayReminderDTO>> GetUpcomingBirthdaysAsync(string currentUserId, int daysAhead)
		{
			if (!IsValidObjectId(currentUserId)) throw new ArgumentException("Invalid user ID format.");

			// Get current user's following list
			var currentUser = await _dbContext.Users.Find(u => u.Id == currentUserId).FirstOrDefaultAsync();
			if (currentUser?.FollowingIds == null || !currentUser.FollowingIds.Any())
			{
				Console.WriteLine($"User {currentUserId} has no following list or it's empty");
				return new List<BirthdayReminderDTO>();
			}

			Console.WriteLine($"User {currentUserId} is following {currentUser.FollowingIds.Count} users");

			// Get following users with birthdays
			var followingUsers = await _dbContext.Users
				.Find(u => currentUser.FollowingIds.Contains(u.Id) && !string.IsNullOrEmpty(u.Birthday))
				.ToListAsync();

			Console.WriteLine($"Found {followingUsers.Count} following users with birthdays");

			// Use UTC to avoid timezone issues
			var today = DateTime.UtcNow.Date;
			var targetDate = today.AddDays(daysAhead);
			var birthdays = new List<BirthdayReminderDTO>();
			
			// DEBUG: Output current server time
			Console.WriteLine($"=== BIRTHDAY CHECK DEBUG ===");
			Console.WriteLine($"Server UTC Now: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
			Console.WriteLine($"Server UTC Date (today): {today:yyyy-MM-dd}");
			Console.WriteLine($"Target Date (today + {daysAhead}): {targetDate:yyyy-MM-dd}");
			Console.WriteLine($"===========================");

			foreach (var user in followingUsers)
			{
				Console.WriteLine($"\n--- Checking user: {user.Username} ---");
				Console.WriteLine($"Stored birthday value: '{user.Birthday}'");
				
				if (DateTime.TryParse(user.Birthday, out var birthday))
				{
					// Extract just the month and day from the stored birthday
					var birthdayMonth = birthday.Month;
					var birthdayDay = birthday.Day;
					
					Console.WriteLine($"Parsed birthday: Year={birthday.Year}, Month={birthdayMonth}, Day={birthdayDay}");
					
					// Calculate this year's birthday
					var thisYearBirthday = new DateTime(today.Year, birthdayMonth, birthdayDay);
					
					Console.WriteLine($"This year's birthday would be: {thisYearBirthday:yyyy-MM-dd}");
					Console.WriteLine($"Server today is: {today:yyyy-MM-dd}");
					
					// Calculate days until birthday
					var daysUntil = (thisYearBirthday - today).Days;
					
					Console.WriteLine($"Days until birthday calculation: ({thisYearBirthday:yyyy-MM-dd} - {today:yyyy-MM-dd}).Days = {daysUntil}");
					
					// If birthday already passed this year (negative days), skip it
					if (daysUntil < 0)
					{
						Console.WriteLine($"❌ SKIPPING {user.Username} - birthday already passed this year (was {-daysUntil} days ago)");
						continue;
					}
					
					Console.WriteLine($"✓ User {user.Username}: daysUntil={daysUntil}, isToday={daysUntil == 0}, isTomorrow={daysUntil == 1}");

					// Check if birthday is within the specified days (only future birthdays)
					if (thisYearBirthday <= targetDate)
					{
						Console.WriteLine($"✓✓ ADDING birthday for {user.Username} - {daysUntil} days until birthday");
						
						birthdays.Add(new BirthdayReminderDTO
						{
							Id = user.Id,
							UserId = user.Id,
							Username = user.Username,
							AvatarUrl = user.AvatarUrl ?? "",
							Birthday = DateTime.TryParse(user.Birthday, out var parsedBirthday) ? parsedBirthday : DateTime.MinValue,
							IsToday = daysUntil == 0,
							IsTomorrow = daysUntil == 1,
							DaysUntilBirthday = daysUntil,
							Message = daysUntil == 0 
								? $"It's {user.Username}'s birthday today! 🎉"
								: daysUntil == 1
								? $"{user.Username}'s birthday is tomorrow! 🎂"
								: $"{user.Username}'s birthday is in {daysUntil} days"
						});
					}
				}
				else
				{
					Console.WriteLine($"Failed to parse birthday for user {user.Username}: {user.Birthday}");
				}
			}

			Console.WriteLine($"Returning {birthdays.Count} birthdays");
			return birthdays.OrderBy(b => b.DaysUntilBirthday).ToList();
		}

		public async Task<int> GetUnreadNotificationCountAsync(string userId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			
			// For now, return 0 as we don't have a notifications collection yet
			// This can be implemented when we add a proper notifications system
			return 0;
		}

		public async Task<List<NotificationDTO>> GetNotificationsAsync(string userId, int page, int pageSize)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			
			// For now, return empty list as we don't have a notifications collection yet
			// This can be implemented when we add a proper notifications system
			return new List<NotificationDTO>();
		}

		public async Task MarkNotificationAsReadAsync(string userId, string notificationId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			
			// For now, do nothing as we don't have a notifications collection yet
			// This can be implemented when we add a proper notifications system
			await Task.CompletedTask;
		}

		public async Task MarkAllNotificationsAsReadAsync(string userId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			
			// For now, do nothing as we don't have a notifications collection yet
			// This can be implemented when we add a proper notifications system
			await Task.CompletedTask;
		}

		public async Task UpdateBirthdayAsync(string userId, string birthday)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
			
			// Validate birthday format
			if (!DateTime.TryParse(birthday, out _))
				throw new ArgumentException("Invalid birthday format. Please use YYYY-MM-DD format.");

			var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
			if (user == null)
				throw new KeyNotFoundException("User not found");

			user.Birthday = birthday;
			await _dbContext.Users.ReplaceOneAsync(u => u.Id == userId, user);
		}

		public async Task<object> GetDebugBirthdayInfoAsync(string userId)
		{
			if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

			var currentUser = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
			if (currentUser == null)
				return new { error = "User not found" };

			// Get all users with birthdays
			var allUsersWithBirthdays = await _dbContext.Users
				.Find(u => !string.IsNullOrEmpty(u.Birthday))
				.ToListAsync();

			// Get following users
			var followingUsers = currentUser.FollowingIds ?? new List<string>();
			var followingUsersWithBirthdays = await _dbContext.Users
				.Find(u => followingUsers.Contains(u.Id) && !string.IsNullOrEmpty(u.Birthday))
				.ToListAsync();

			var today = DateTime.Today;
			var tomorrow = today.AddDays(1);

			return new
			{
				currentUserId = userId,
				currentUsername = currentUser.Username,
				followingCount = followingUsers.Count,
				followingIds = followingUsers,
				allUsersWithBirthdays = allUsersWithBirthdays.Select(u => new
				{
					id = u.Id,
					username = u.Username,
					birthday = u.Birthday,
					isFollowing = followingUsers.Contains(u.Id)
				}).ToList(),
				followingUsersWithBirthdays = followingUsersWithBirthdays.Select(u => new
				{
					id = u.Id,
					username = u.Username,
					birthday = u.Birthday,
					daysUntilBirthday = GetDaysUntilBirthday(u.Birthday, today)
				}).ToList(),
				today = today.ToString("yyyy-MM-dd"),
				tomorrow = tomorrow.ToString("yyyy-MM-dd")
			};
		}

		private int GetDaysUntilBirthday(string birthdayStr, DateTime today)
		{
			if (DateTime.TryParse(birthdayStr, out var birthday))
			{
				var thisYearBirthday = new DateTime(today.Year, birthday.Month, birthday.Day);
				if (thisYearBirthday < today)
					thisYearBirthday = thisYearBirthday.AddYears(1);
				return (thisYearBirthday - today).Days;
			}
			return -1;
		}
	}
}
