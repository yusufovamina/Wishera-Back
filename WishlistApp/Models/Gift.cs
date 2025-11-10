using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace WisheraApp.Models
{
    [BsonIgnoreExtraElements]
    public class Gift
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public string ImageUrl { get; set; } = ""; // Ensure default value
        public required string Category { get; set; }
        public string? WishlistId { get; set; }
        public string? UserId { get; set; } // Owner/Creator of the gift
        public string? ReservedByUserId { get; set; } = null;
        public string? ReservedByUsername { get; set; } = null; // ✅ Store username
    }
} 