using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace user_service.Models
{
    public class EventInvitation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("eventId")]
        [Required]
        public string EventId { get; set; } = string.Empty;

        [BsonElement("inviteeId")]
        [Required]
        public string InviteeId { get; set; } = string.Empty;

        [BsonElement("inviterId")]
        [Required]
        public string InviterId { get; set; } = string.Empty;

        [BsonElement("status")]
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        [BsonElement("invitedAt")]
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("respondedAt")]
        public DateTime? RespondedAt { get; set; }

        [BsonElement("responseMessage")]
        [StringLength(200)]
        public string? ResponseMessage { get; set; }
    }

    public enum InvitationStatus
    {
        Pending = 0,
        Accepted = 1,
        Declined = 2,
        Maybe = 3
    }
}
