using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace WisheraApp.Models
{
    public class Wishlist
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string UserId { get; set; } = null!;

        [BsonRequired]
        public string Title { get; set; } = null!;

        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = true;
        public List<string> AllowedViewerIds { get; set; } = new List<string>();
        public List<WishlistItem> Items { get; set; } = new List<WishlistItem>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Social features
        public int LikeCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public List<string> LikeIds { get; set; } = new List<string>();
        public List<string> CommentIds { get; set; } = new List<string>();
    }

    public class WishlistItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRequired]
        public string Title { get; set; } = null!;

        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsReserved { get; set; } = false;
        public string ReservedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
} 