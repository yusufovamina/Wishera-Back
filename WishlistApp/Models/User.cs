using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace WishlistApp.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string Username { get; set; } = null!;

        [BsonRequired]
        public string Email { get; set; } = null!;

        [BsonRequired]
        public string PasswordHash { get; set; } = null!;

        public string Role { get; set; } = "user";
        public string Bio { get; set; } = string.Empty;
        public List<string> Interests { get; set; } = new List<string>();
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastActive { get; set; } = DateTime.UtcNow;

        // Password reset fields
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Social features
        public List<string> FollowingIds { get; set; } = new List<string>();
        public List<string> FollowerIds { get; set; } = new List<string>();
        public List<string> WishlistIds { get; set; } = new List<string>();

        // Privacy settings
        public bool IsPrivate { get; set; } = false;
        public List<string> AllowedViewerIds { get; set; } = new List<string>();
    }
} 