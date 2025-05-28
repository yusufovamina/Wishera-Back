using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace WishlistApp.Models
{
    public class Like
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string UserId { get; set; } = null!;

        [BsonRequired]
        public string WishlistId { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Comment
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string UserId { get; set; } = null!;

        [BsonRequired]
        public string WishlistId { get; set; } = null!;

        [BsonRequired]
        public string Text { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsEdited { get; set; } = false;
    }

    public class FeedEvent
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string UserId { get; set; } = null!;

        [BsonRequired]
        public string ActionType { get; set; } = null!; // "created_wishlist", "added_item", "liked_wishlist", "commented"

        public string WishlistId { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string CommentId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // For performance optimization
        public string Username { get; set; } = string.Empty;
        public string WishlistTitle { get; set; } = string.Empty;
        public string ItemTitle { get; set; } = string.Empty;
        public string CommentText { get; set; } = string.Empty;
    }

    public class Relationship
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string FollowerId { get; set; } = null!;

        [BsonRequired]
        public string FollowingId { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
} 