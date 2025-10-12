using WisheraApp.Models;

namespace WisheraApp.DTO
{
    public class NotificationDTO
    {
        public string Id { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RelatedUserId { get; set; }
        public string? RelatedUserName { get; set; }
        public string? RelatedUserAvatar { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class CreateNotificationDTO
    {
        public string UserId { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RelatedUserId { get; set; }
        public string? RelatedUserName { get; set; }
        public string? RelatedUserAvatar { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class UpdateNotificationDTO
    {
        public bool IsRead { get; set; }
    }

    public class BirthdayReminderDTO
    {
        public string FriendId { get; set; } = string.Empty;
        public string FriendName { get; set; } = string.Empty;
        public string FriendAvatar { get; set; } = string.Empty;
        public DateTime Birthday { get; set; }
        public int DaysUntilBirthday { get; set; }
        public bool IsToday { get; set; }
    }
}


