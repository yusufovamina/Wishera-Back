using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WisheraApp.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("type")]
        public NotificationType Type { get; set; }

        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("isRead")]
        public bool IsRead { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("relatedUserId")]
        public string? RelatedUserId { get; set; }

        [BsonElement("relatedUserName")]
        public string? RelatedUserName { get; set; }

        [BsonElement("relatedUserAvatar")]
        public string? RelatedUserAvatar { get; set; }

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }
    }

    public enum NotificationType
    {
        BirthdayReminder = 1,
        FriendRequest = 2,
        WishlistUpdate = 3,
        GiftReceived = 4,
        SystemMessage = 5
    }
}


