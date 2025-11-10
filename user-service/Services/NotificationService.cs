using MongoDB.Driver;
using user_service.Models;
using WisheraApp.DTO;
using WisheraApp.Models;

namespace user_service.Services
{
    public interface INotificationService
    {
        Task<NotificationDTO> CreateEventInvitationNotificationAsync(string userId, string inviterId, string eventId, string eventTitle);
        Task<NotificationDTO> CreateEventResponseNotificationAsync(string userId, string responderId, string eventId, string eventTitle, InvitationStatus status, string? responseMessage);
        Task<NotificationDTO> CreateEventCancellationNotificationAsync(string userId, string cancellerId, string eventId, string eventTitle);
        Task<NotificationDTO> CreateEventReminderNotificationAsync(string userId, string eventId, string eventTitle, DateTime eventDate);
        Task<NotificationDTO> CreateBirthdayReminderNotificationAsync(string userId, string friendId, string friendUsername, DateTime birthday);
        Task<NotificationDTO> CreateWishlistLikeNotificationAsync(string userId, string likerId, string wishlistId, string wishlistTitle);
        Task<NotificationDTO> CreateFriendRequestNotificationAsync(string userId, string followerId);
        Task<NotificationDTO> CreateGiftReservedNotificationAsync(string userId, string reserverId, string giftId, string giftName, string wishlistId);
        Task<NotificationDTO> CreateUserSuggestionNotificationAsync(string userId, string suggestedUserId);
        Task<NotificationListDTO> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 20);
        Task<int> GetUnreadNotificationCountAsync(string userId);
        Task<bool> MarkNotificationsAsReadAsync(string userId, List<string> notificationIds);
        Task<bool> MarkAllNotificationsAsReadAsync(string userId);
        Task<bool> DeleteNotificationAsync(string userId, string notificationId);
        Task<bool> DeleteNotificationByTypeAndRelatedUserAsync(string userId, NotificationType type, string relatedUserId, string? relatedEntityId = null);
        Task<bool> DeleteExpiredNotificationsAsync();
        Task<List<NotificationDTO>> CreateBirthdayRemindersAsync();
        Task<List<NotificationDTO>> CreateUserSuggestionsAsync();
    }

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

        public async Task<NotificationDTO> CreateEventInvitationNotificationAsync(string userId, string inviterId, string eventId, string eventTitle)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(inviterId)) throw new ArgumentException("Invalid inviter ID format.");
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");

            var inviter = await _dbContext.Users.Find(u => u.Id == inviterId).FirstOrDefaultAsync();
            if (inviter == null) throw new KeyNotFoundException("Inviter not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.EventInvitation,
                Title = "Event Invitation",
                Message = $"{inviter.Username} invited you to '{eventTitle}'",
                RelatedUserId = inviterId,
                RelatedEntityId = eventId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30), // Events expire after 30 days
                Metadata = new Dictionary<string, object>
                {
                    { "eventTitle", eventTitle },
                    { "inviterUsername", inviter.Username }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateEventResponseNotificationAsync(string userId, string responderId, string eventId, string eventTitle, InvitationStatus status, string? responseMessage)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(responderId)) throw new ArgumentException("Invalid responder ID format.");
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");

            var responder = await _dbContext.Users.Find(u => u.Id == responderId).FirstOrDefaultAsync();
            if (responder == null) throw new KeyNotFoundException("Responder not found.");

            var statusText = status switch
            {
                InvitationStatus.Accepted => "accepted",
                InvitationStatus.Declined => "declined",
                InvitationStatus.Maybe => "responded 'maybe' to",
                _ => "responded to"
            };

            var message = $"{responder.Username} {statusText} your invitation to '{eventTitle}'";
            if (!string.IsNullOrEmpty(responseMessage))
            {
                message += $": \"{responseMessage}\"";
            }

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.EventResponse,
                Title = "Event Response",
                Message = message,
                RelatedUserId = responderId,
                RelatedEntityId = eventId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Responses expire after 7 days
                Metadata = new Dictionary<string, object>
                {
                    { "eventTitle", eventTitle },
                    { "responderUsername", responder.Username },
                    { "responseStatus", status.ToString() },
                    { "responseMessage", responseMessage ?? "" }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateEventCancellationNotificationAsync(string userId, string cancellerId, string eventId, string eventTitle)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(cancellerId)) throw new ArgumentException("Invalid canceller ID format.");
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");

            var canceller = await _dbContext.Users.Find(u => u.Id == cancellerId).FirstOrDefaultAsync();
            if (canceller == null) throw new KeyNotFoundException("Canceller not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.EventCancellation,
                Title = "Event Cancelled",
                Message = $"{canceller.Username} cancelled the event '{eventTitle}'",
                RelatedUserId = cancellerId,
                RelatedEntityId = eventId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Cancellations expire after 7 days
                Metadata = new Dictionary<string, object>
                {
                    { "eventTitle", eventTitle },
                    { "cancellerUsername", canceller.Username }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateEventReminderNotificationAsync(string userId, string eventId, string eventTitle, DateTime eventDate)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(eventId)) throw new ArgumentException("Invalid event ID format.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.EventReminder,
                Title = "Event Reminder",
                Message = $"Don't forget! '{eventTitle}' is coming up soon",
                RelatedEntityId = eventId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = eventDate.AddDays(1), // Reminders expire 1 day after the event
                Metadata = new Dictionary<string, object>
                {
                    { "eventTitle", eventTitle },
                    { "eventDate", eventDate.ToString("yyyy-MM-dd") }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateBirthdayReminderNotificationAsync(string userId, string friendId, string friendUsername, DateTime birthday)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(friendId)) throw new ArgumentException("Invalid friend ID format.");

            var daysUntilBirthday = (birthday.Date - DateTime.UtcNow.Date).Days;
            var message = daysUntilBirthday == 0 
                ? $"It's {friendUsername}'s birthday today! 🎉"
                : $"{friendUsername}'s birthday is tomorrow! 🎂";

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.BirthdayReminder,
                Title = "Birthday Reminder",
                Message = message,
                RelatedUserId = friendId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(1), // Birthday reminders expire after 1 day
                Metadata = new Dictionary<string, object>
                {
                    { "friendUsername", friendUsername },
                    { "birthday", birthday.ToString("yyyy-MM-dd") },
                    { "daysUntilBirthday", daysUntilBirthday }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationListDTO> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 20)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:notifications:{userId}:{page}:{pageSize}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var skip = (page - 1) * pageSize;
                var notifications = await _dbContext.Notifications
                    .Find(n => n.UserId == userId && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow))
                    .Sort(Builders<Notification>.Sort.Descending(n => n.CreatedAt))
                    .Skip(skip)
                    .Limit(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.Notifications
                    .CountDocumentsAsync(n => n.UserId == userId && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow));

                var unreadCount = await _dbContext.Notifications
                    .CountDocumentsAsync(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow));

                var notificationDtos = new List<NotificationDTO>();
                foreach (var notification in notifications)
                {
                    notificationDtos.Add(await MapToNotificationDTO(notification));
                }

                return new NotificationListDTO
                {
                    Notifications = notificationDtos,
                    TotalCount = (int)totalCount,
                    UnreadCount = (int)unreadCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }, TimeSpan.FromMinutes(10));
        }

        public async Task<int> GetUnreadNotificationCountAsync(string userId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var cacheKey = $"user:unread-count:{userId}";
            return await _cache.GetOrSetAsync(cacheKey, async () =>
            {
                var count = await _dbContext.Notifications
                    .CountDocumentsAsync(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow));
                return (int)count;
            }, TimeSpan.FromMinutes(10));
        }

        public async Task<bool> MarkNotificationsAsReadAsync(string userId, List<string> notificationIds)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            foreach (var notificationId in notificationIds)
            {
                if (!IsValidObjectId(notificationId)) throw new ArgumentException($"Invalid notification ID format: {notificationId}");
            }

            var updateDefinition = Builders<Notification>.Update.Set(n => n.IsRead, true);
            var result = await _dbContext.Notifications.UpdateManyAsync(
                n => n.UserId == userId && notificationIds.Contains(n.Id),
                updateDefinition
            );

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(string userId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");

            var updateDefinition = Builders<Notification>.Update.Set(n => n.IsRead, true);
            var result = await _dbContext.Notifications.UpdateManyAsync(
                n => n.UserId == userId && !n.IsRead,
                updateDefinition
            );

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteNotificationAsync(string userId, string notificationId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(notificationId)) throw new ArgumentException("Invalid notification ID format.");

            var result = await _dbContext.Notifications.DeleteOneAsync(
                n => n.Id == notificationId && n.UserId == userId
            );

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return result.DeletedCount > 0;
        }

        public async Task<bool> DeleteNotificationByTypeAndRelatedUserAsync(string userId, NotificationType type, string relatedUserId, string? relatedEntityId = null)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(relatedUserId)) throw new ArgumentException("Invalid related user ID format.");

            var filterBuilder = Builders<Notification>.Filter;
            var filters = new List<FilterDefinition<Notification>>
            {
                filterBuilder.Eq(n => n.UserId, userId),
                filterBuilder.Eq(n => n.Type, type),
                filterBuilder.Eq(n => n.RelatedUserId, relatedUserId)
            };

            // Add optional relatedEntityId filter
            if (!string.IsNullOrEmpty(relatedEntityId))
            {
                if (!IsValidObjectId(relatedEntityId)) throw new ArgumentException("Invalid related entity ID format.");
                filters.Add(filterBuilder.Eq(n => n.RelatedEntityId, relatedEntityId));
            }

            var combinedFilter = filterBuilder.And(filters);
            var result = await _dbContext.Notifications.DeleteManyAsync(combinedFilter);

            if (result.DeletedCount > 0)
            {
                // Clear cache
                await ClearNotificationCacheAsync(userId);
            }

            return result.DeletedCount > 0;
        }

        public async Task<bool> DeleteExpiredNotificationsAsync()
        {
            var result = await _dbContext.Notifications.DeleteManyAsync(
                n => n.ExpiresAt != null && n.ExpiresAt < DateTime.UtcNow
            );

            return result.DeletedCount > 0;
        }

        public async Task<List<NotificationDTO>> CreateBirthdayRemindersAsync()
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Get users who have friends with birthdays today or tomorrow
            var users = await _dbContext.Users.Find(u => u.FollowingIds != null && u.FollowingIds.Any()).ToListAsync();
            var createdNotifications = new List<NotificationDTO>();

            foreach (var user in users)
            {
                var friends = await _dbContext.Users.Find(f => user.FollowingIds.Contains(f.Id) && !string.IsNullOrEmpty(f.Birthday)).ToListAsync();
                
                foreach (var friend in friends)
                {
                    if (string.IsNullOrEmpty(friend.Birthday)) continue;

                    // Try to parse the birthday with different formats
                    DateTime birthday;
                    if (!DateTime.TryParse(friend.Birthday, out birthday))
                    {
                        // Try parsing with specific format
                        if (!DateTime.TryParseExact(friend.Birthday, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out birthday))
                        {
                            // Skip if we can't parse the birthday
                            continue;
                        }
                    }
                    var birthdayThisYear = new DateTime(today.Year, birthday.Month, birthday.Day);
                    var daysUntilBirthday = (birthdayThisYear - today).Days;

                    // Create notification for birthdays within the next 7 days
                    if (daysUntilBirthday >= 0 && daysUntilBirthday <= 7)
                    {
                        try
                        {
                            var notification = await CreateBirthdayReminderNotificationAsync(
                                user.Id, 
                                friend.Id, 
                                friend.Username, 
                                birthdayThisYear
                            );
                            createdNotifications.Add(notification);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue processing other notifications
                            Console.WriteLine($"Error creating birthday reminder for user {user.Id}, friend {friend.Id}: {ex.Message}");
                        }
                    }
                }
            }

            return createdNotifications;
        }

        public async Task<NotificationDTO> CreateWishlistLikeNotificationAsync(string userId, string likerId, string wishlistId, string wishlistTitle)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(likerId)) throw new ArgumentException("Invalid liker ID format.");
            if (!IsValidObjectId(wishlistId)) throw new ArgumentException("Invalid wishlist ID format.");

            // Don't create notification if user likes their own wishlist
            if (userId == likerId) throw new ArgumentException("Cannot create notification for self-like.");

            var liker = await _dbContext.Users.Find(u => u.Id == likerId).FirstOrDefaultAsync();
            if (liker == null) throw new KeyNotFoundException("Liker not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.LikeReceived,
                Title = "Wishlist Liked",
                Message = $"{liker.Username} liked your wishlist '{wishlistTitle}'",
                RelatedUserId = likerId,
                RelatedEntityId = wishlistId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Metadata = new Dictionary<string, object>
                {
                    { "wishlistTitle", wishlistTitle },
                    { "likerUsername", liker.Username }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateFriendRequestNotificationAsync(string userId, string followerId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(followerId)) throw new ArgumentException("Invalid follower ID format.");

            var follower = await _dbContext.Users.Find(u => u.Id == followerId).FirstOrDefaultAsync();
            if (follower == null) throw new KeyNotFoundException("Follower not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.FriendRequest,
                Title = "New Follower",
                Message = $"{follower.Username} started following you",
                RelatedUserId = followerId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Metadata = new Dictionary<string, object>
                {
                    { "followerUsername", follower.Username }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateGiftReservedNotificationAsync(string userId, string reserverId, string giftId, string giftName, string wishlistId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(reserverId)) throw new ArgumentException("Invalid reserver ID format.");
            if (!IsValidObjectId(giftId)) throw new ArgumentException("Invalid gift ID format.");

            // Don't create notification if user reserves their own gift
            if (userId == reserverId) throw new ArgumentException("Cannot create notification for self-reservation.");

            var reserver = await _dbContext.Users.Find(u => u.Id == reserverId).FirstOrDefaultAsync();
            if (reserver == null) throw new KeyNotFoundException("Reserver not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.GiftReserved,
                Title = "Gift Reserved",
                Message = $"{reserver.Username} reserved '{giftName}' from your wishlist",
                RelatedUserId = reserverId,
                RelatedEntityId = giftId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Metadata = new Dictionary<string, object>
                {
                    { "giftName", giftName },
                    { "reserverUsername", reserver.Username },
                    { "wishlistId", wishlistId }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<NotificationDTO> CreateUserSuggestionNotificationAsync(string userId, string suggestedUserId)
        {
            if (!IsValidObjectId(userId)) throw new ArgumentException("Invalid user ID format.");
            if (!IsValidObjectId(suggestedUserId)) throw new ArgumentException("Invalid suggested user ID format.");

            var suggestedUser = await _dbContext.Users.Find(u => u.Id == suggestedUserId).FirstOrDefaultAsync();
            if (suggestedUser == null) throw new KeyNotFoundException("Suggested user not found.");

            var notification = new Notification
            {
                UserId = userId,
                Type = NotificationType.UserSuggestion,
                Title = "You May Know",
                Message = $"You may know {suggestedUser.Username}",
                RelatedUserId = suggestedUserId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // User suggestions expire after 7 days
                Metadata = new Dictionary<string, object>
                {
                    { "suggestedUsername", suggestedUser.Username }
                }
            };

            await _dbContext.Notifications.InsertOneAsync(notification);

            // Clear cache
            await ClearNotificationCacheAsync(userId);

            return await MapToNotificationDTO(notification);
        }

        public async Task<List<NotificationDTO>> CreateUserSuggestionsAsync()
        {
            // Get all users
            var users = await _dbContext.Users.Find(_ => true).ToListAsync();
            var createdNotifications = new List<NotificationDTO>();

            foreach (var user in users)
            {
                try
                {
                    var followingIds = user.FollowingIds ?? new List<string>();
                    var excludeIds = new HashSet<string>(followingIds) { user.Id };

                    // Find users not currently followed
                    var baseFilter = Builders<User>.Filter.And(
                        Builders<User>.Filter.Eq(u => u.IsPrivate, false),
                        Builders<User>.Filter.Nin(u => u.Id, excludeIds.ToList())
                    );

                    // Get candidate users (limit to 20 for scoring)
                    var candidatePool = await _dbContext.Users.Find(baseFilter)
                        .Limit(20)
                        .ToListAsync();

                    if (!candidatePool.Any()) continue;

                    // Score based on mutual friends and shared interests
                    int Score(User u)
                    {
                        var uFollowerIds = u.FollowerIds ?? new List<string>();
                        var uInterests = u.Interests ?? new List<string>();
                        var myFollowing = followingIds ?? new List<string>();
                        var myInterests = user.Interests ?? new List<string>();

                        var mutual = myFollowing.Intersect(uFollowerIds).Count();
                        var sharedInterests = myInterests.Intersect(uInterests, StringComparer.OrdinalIgnoreCase).Count();
                        return (mutual * 3) + (sharedInterests * 2);
                    }

                    var topSuggestion = candidatePool
                        .OrderByDescending(Score)
                        .FirstOrDefault();

                    if (topSuggestion != null && Score(topSuggestion) > 0)
                    {
                        // Only create suggestion if there's no recent suggestion for this user
                        var recentSuggestion = await _dbContext.Notifications
                            .Find(n => n.UserId == user.Id 
                                && n.Type == NotificationType.UserSuggestion 
                                && n.CreatedAt > DateTime.UtcNow.AddDays(-3))
                            .AnyAsync();

                        if (!recentSuggestion)
                        {
                            var notification = await CreateUserSuggestionNotificationAsync(user.Id, topSuggestion.Id);
                            createdNotifications.Add(notification);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating user suggestion for user {user.Id}: {ex.Message}");
                }
            }

            return createdNotifications;
        }

        private async Task ClearNotificationCacheAsync(string userId)
        {
            // Clear unread count cache
            await _cache.RemoveAsync($"user:unread-count:{userId}");
            
            // Clear paginated notification caches (common page sizes)
            var commonPageSizes = new[] { 10, 20, 50 };
            var commonPages = new[] { 1, 2, 3 };
            
            foreach (var pageSize in commonPageSizes)
            {
                foreach (var page in commonPages)
                {
                    await _cache.RemoveAsync($"user:notifications:{userId}:{page}:{pageSize}");
                }
            }
        }

        private async Task<NotificationDTO> MapToNotificationDTO(Notification notification)
        {
            var dto = new NotificationDTO
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                RelatedUserId = notification.RelatedUserId,
                RelatedEntityId = notification.RelatedEntityId,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt,
                ExpiresAt = notification.ExpiresAt,
                Metadata = notification.Metadata
            };

            // Get related user info if available
            if (!string.IsNullOrEmpty(notification.RelatedUserId))
            {
                var relatedUser = await _dbContext.Users.Find(u => u.Id == notification.RelatedUserId).FirstOrDefaultAsync();
                if (relatedUser != null)
                {
                    dto.RelatedUserUsername = relatedUser.Username;
                    dto.RelatedUserAvatarUrl = relatedUser.AvatarUrl ?? "";
                }
            }

            return dto;
        }
    }
}
