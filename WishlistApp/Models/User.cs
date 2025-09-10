using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace WishlistApp.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("username")]
        [BsonRequired]
        public string Username { get; set; } = null!;

        [BsonElement("email")]
        [BsonRequired]
        public string Email { get; set; } = null!;

        [BsonElement("emailNormalized")]
        public string EmailNormalized { get; set; } = string.Empty;

        [BsonElement("usernameNormalized")]
        public string UsernameNormalized { get; set; } = string.Empty;

        [BsonElement("passwordHash")]
        [BsonRequired]
        public string PasswordHash { get; set; } = null!;

        [BsonElement("role")]
        public string Role { get; set; } = "user";
        [BsonElement("bio")]
        public string Bio { get; set; } = string.Empty;
        [BsonElement("interests")]
        public List<string> Interests { get; set; } = new List<string>();
        [BsonElement("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [BsonElement("lastActive")]
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        // Password reset fields
        [BsonElement("resetPasswordToken")]
        public string? ResetToken { get; set; }
        [BsonElement("resetPasswordTokenExpiry")]
        public DateTime? ResetTokenExpiry { get; set; }

        // Social features
        [BsonElement("followingIds")]
        public List<string> FollowingIds { get; set; } = new List<string>();
        [BsonElement("followerIds")]
        public List<string> FollowerIds { get; set; } = new List<string>();
        [BsonElement("wishlistIds")]
        public List<string> WishlistIds { get; set; } = new List<string>();

        // Privacy settings
        [BsonElement("isPrivate")]
        public bool IsPrivate { get; set; } = false;
        [BsonElement("allowedViewerIds")]
        public List<string> AllowedViewerIds { get; set; } = new List<string>();
    }
} 