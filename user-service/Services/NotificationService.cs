using MongoDB.Driver;
using WisheraApp.DTO;
using WisheraApp.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace user_service.Services
{
    public class NotificationService : INotificationService
    {
        private readonly MongoDbContext _dbContext;
        private readonly ICacheService _cache;

        public NotificationService(MongoDbContext dbContext, ICacheService cache)
        {
            _dbContext = dbContext;
            _cache = cache;
        }

        private bool IsValidObjectId(string id) => MongoDB.Bson.ObjectId.TryParse(id, out _);

        public async Task<List<NotificationDTO>> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 20)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"notifications:{userId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var notifications = await _dbContext.Notifications
                    .Find(n => n.UserId == userId)
                    .Sort(Builders<Notification>.Sort.Descending(n => n.CreatedAt))
                    .Skip((page - 1) * pageSize)
                    .Limit(pageSize)
                    .ToListAsync();

                return notifications.Select(MapToDTO).ToList();
            }, TimeSpan.FromMinutes(2)) ?? new List<NotificationDTO>();
        }

        public async Task<NotificationDTO> CreateNotificationAsync(CreateNotificationDTO notificationDto)
        {
            if (!IsValidObjectId(notificationDto.UserId)) throw new ArgumentException("Invalid user ID format.");

            var notification = new Notification
            {
                UserId = notificationDto.UserId,
                Type = notificationDto.Type,
                Title = notificationDto.Title,
                Message = notificationDto.Message,
                RelatedUserId = notificationDto.RelatedUserId,
                RelatedUserName = notificationDto.RelatedUserName,
                RelatedUserAvatar = notificationDto.RelatedUserAvatar,
                ExpiresAt = notificationDto.ExpiresAt,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Notifications.InsertOneAsync(notification);
            
            // Clear cache for this user
            await _cache.RemoveAsync($"notifications:{notificationDto.UserId}:*");
            await _cache.RemoveAsync($"unread_count:{notificationDto.UserId}");

            return MapToDTO(notification);
        }

        public async Task<bool> MarkNotificationAsReadAsync(string notificationId, string userId)
        {
            if (!IsValidObjectId(notificationId)) throw new ArgumentException("Invalid notification ID format.");
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var result = await _dbContext.Notifications.UpdateOneAsync(
                n => n.Id == notificationId && n.UserId == userId,
                Builders<Notification>.Update.Set(n => n.IsRead, true)
            );

            if (result.ModifiedCount > 0)
            {
                await _cache.RemoveAsync($"notifications:{userId}:*");
                await _cache.RemoveAsync($"unread_count:{userId}");
                return true;
            }

            return false;
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(string userId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var result = await _dbContext.Notifications.UpdateManyAsync(
                n => n.UserId == userId && !n.IsRead,
                Builders<Notification>.Update.Set(n => n.IsRead, true)
            );

            if (result.ModifiedCount > 0)
            {
                await _cache.RemoveAsync($"notifications:{userId}:*");
                await _cache.RemoveAsync($"unread_count:{userId}");
                return true;
            }

            return false;
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"unread_count:{userId}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var count = await _dbContext.Notifications
                    .CountDocumentsAsync(n => n.UserId == userId && !n.IsRead);
                return (int)count;
            }, TimeSpan.FromMinutes(5));
        }

        public async Task<List<BirthdayReminderDTO>> GetUpcomingBirthdaysAsync(string userId, int daysAhead = 7)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"upcoming_birthdays:{userId}:{daysAhead}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
                if (user == null) throw new KeyNotFoundException("User not found.");

                var followingIds = user.FollowingIds ?? new List<string>();
                if (!followingIds.Any()) return new List<BirthdayReminderDTO>();

                var friends = await _dbContext.Users
                    .Find(u => followingIds.Contains(u.Id) && u.Birthday.HasValue)
                    .ToListAsync();

                var today = DateTime.UtcNow.Date;
                var upcomingBirthdays = new List<BirthdayReminderDTO>();

                foreach (var friend in friends)
                {
                    if (!friend.Birthday.HasValue) continue;

                    var birthdayThisYear = new DateTime(today.Year, friend.Birthday.Value.Month, friend.Birthday.Value.Day);
                    var birthdayNextYear = new DateTime(today.Year + 1, friend.Birthday.Value.Month, friend.Birthday.Value.Day);

                    DateTime nextBirthday;
                    int daysUntilBirthday;

                    if (birthdayThisYear >= today)
                    {
                        nextBirthday = birthdayThisYear;
                        daysUntilBirthday = (birthdayThisYear - today).Days;
                    }
                    else
                    {
                        nextBirthday = birthdayNextYear;
                        daysUntilBirthday = (birthdayNextYear - today).Days;
                    }

                    if (daysUntilBirthday <= daysAhead)
                    {
                        upcomingBirthdays.Add(new BirthdayReminderDTO
                        {
                            FriendId = friend.Id,
                            FriendName = friend.Username,
                            FriendAvatar = friend.AvatarUrl,
                            Birthday = nextBirthday,
                            DaysUntilBirthday = daysUntilBirthday,
                            IsToday = daysUntilBirthday == 0
                        });
                    }
                }

                return upcomingBirthdays.OrderBy(b => b.DaysUntilBirthday).ToList();
            }, TimeSpan.FromHours(1)) ?? new List<BirthdayReminderDTO>();
        }

        public async Task CreateBirthdayRemindersAsync()
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Get all users who have friends with birthdays today or tomorrow
            var usersWithFriends = await _dbContext.Users
                .Find(u => u.FollowingIds != null && u.FollowingIds.Any())
                .ToListAsync();

            foreach (var user in usersWithFriends)
            {
                var followingIds = user.FollowingIds ?? new List<string>();
                if (!followingIds.Any()) continue;

                var friendsWithBirthdays = await _dbContext.Users
                    .Find(u => followingIds.Contains(u.Id) && u.Birthday.HasValue)
                    .ToListAsync();

                foreach (var friend in friendsWithBirthdays)
                {
                    if (!friend.Birthday.HasValue) continue;

                    var birthdayThisYear = new DateTime(today.Year, friend.Birthday.Value.Month, friend.Birthday.Value.Day);
                    var birthdayNextYear = new DateTime(today.Year + 1, friend.Birthday.Value.Month, friend.Birthday.Value.Day);

                    DateTime nextBirthday;
                    int daysUntilBirthday;

                    if (birthdayThisYear >= today)
                    {
                        nextBirthday = birthdayThisYear;
                        daysUntilBirthday = (birthdayThisYear - today).Days;
                    }
                    else
                    {
                        nextBirthday = birthdayNextYear;
                        daysUntilBirthday = (birthdayNextYear - today).Days;
                    }

                    // Create notification for birthdays within the next 7 days
                    if (daysUntilBirthday >= 0 && daysUntilBirthday <= 7)
                    {
                        var message = daysUntilBirthday == 0 
                            ? $"It's {friend.Username}'s birthday today! 🎉"
                            : $"{friend.Username}'s birthday is tomorrow! 🎂";

                        var title = daysUntilBirthday == 0 
                            ? "Birthday Today!" 
                            : "Birthday Tomorrow!";

                        // Check if notification already exists for today
                        var existingNotification = await _dbContext.Notifications
                            .Find(n => n.UserId == user.Id && 
                                      n.Type == NotificationType.BirthdayReminder && 
                                      n.RelatedUserId == friend.Id &&
                                      n.CreatedAt.Date == today)
                            .FirstOrDefaultAsync();

                        if (existingNotification == null)
                        {
                            var notificationDto = new CreateNotificationDTO
                            {
                                UserId = user.Id,
                                Type = NotificationType.BirthdayReminder,
                                Title = title,
                                Message = message,
                                RelatedUserId = friend.Id,
                                RelatedUserName = friend.Username,
                                RelatedUserAvatar = friend.AvatarUrl,
                                ExpiresAt = today.AddDays(7) // Expire after 7 days
                            };

                            await CreateNotificationAsync(notificationDto);
                        }
                    }
                }
            }
        }

        public async Task DeleteExpiredNotificationsAsync()
        {
            var result = await _dbContext.Notifications.DeleteManyAsync(
                n => n.ExpiresAt.HasValue && n.ExpiresAt.Value < DateTime.UtcNow
            );

            // Clear all notification caches since we deleted some notifications
            // In a production system, you'd want to be more selective about cache invalidation
            await _cache.RemoveAsync("notifications:*");
            await _cache.RemoveAsync("unread_count:*");
        }

        private NotificationDTO MapToDTO(Notification notification)
        {
            return new NotificationDTO
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                RelatedUserId = notification.RelatedUserId,
                RelatedUserName = notification.RelatedUserName,
                RelatedUserAvatar = notification.RelatedUserAvatar,
                ExpiresAt = notification.ExpiresAt
            };
        }
    }
}

