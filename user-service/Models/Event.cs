using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace user_service.Models
{
    public class Event
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("title")]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [BsonElement("eventDate")]
        [Required]
        public DateTime EventDate { get; set; }

        [BsonElement("eventTime")]
        public TimeSpan? EventTime { get; set; }

        [BsonElement("location")]
        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [BsonElement("additionalNotes")]
        [StringLength(1000)]
        public string AdditionalNotes { get; set; } = string.Empty;

        [BsonElement("creatorId")]
        [Required]
        public string CreatorId { get; set; } = string.Empty;

        [BsonElement("inviteeIds")]
        public List<string> InviteeIds { get; set; } = new List<string>();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("isCancelled")]
        public bool IsCancelled { get; set; } = false;

        [BsonElement("eventType")]
        [StringLength(50)]
        public string EventType { get; set; } = "General"; // Birthday, Party, Meeting, etc.
    }
}
