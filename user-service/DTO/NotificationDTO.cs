using System.ComponentModel.DataAnnotations;
using user_service.Models;

namespace WisheraApp.DTO
{
    public class NotificationDTO
    {
        public string Id { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RelatedUserId { get; set; }
        public string? RelatedEntityId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
        public string? RelatedUserUsername { get; set; }
        public string? RelatedUserAvatarUrl { get; set; }
    }

    public class NotificationListDTO
    {
        public List<NotificationDTO> Notifications { get; set; } = new List<NotificationDTO>();
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class MarkNotificationReadDTO
    {
        public List<string> NotificationIds { get; set; } = new List<string>();
    }
}
