using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace WisheraApp.Models
{
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

