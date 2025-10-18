using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace user_service.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("userId")]
        [Required]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("type")]
        [Required]
        public NotificationType Type { get; set; }

        [BsonElement("title")]
        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [BsonElement("message")]
        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [BsonElement("relatedUserId")]
        public string? RelatedUserId { get; set; }

        [BsonElement("relatedEntityId")]
        public string? RelatedEntityId { get; set; }

        [BsonElement("isRead")]
        public bool IsRead { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public enum NotificationType
    {
        EventInvitation = 1,
        EventResponse = 2,
        EventCancellation = 3,
        EventReminder = 4,
        BirthdayReminder = 5,
        FriendRequest = 6,
        FriendAccepted = 7,
        GiftReceived = 8,
        WishlistShared = 9,
        CommentAdded = 10,
        LikeReceived = 11
    }
}
