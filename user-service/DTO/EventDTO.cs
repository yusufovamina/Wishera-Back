using System.ComponentModel.DataAnnotations;
using user_service.Models;

namespace WisheraApp.DTO
{
    public class CreateEventDTO
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime EventDate { get; set; }

        public TimeSpan? EventTime { get; set; }

        [StringLength(200)]
        public string Location { get; set; } = string.Empty;

        [StringLength(1000)]
        public string AdditionalNotes { get; set; } = string.Empty;

        [Required]
        public List<string> InviteeIds { get; set; } = new List<string>();

        [StringLength(50)]
        public string EventType { get; set; } = "General";
    }

    public class UpdateEventDTO
    {
        [StringLength(100, MinimumLength = 1)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime? EventDate { get; set; }

        public TimeSpan? EventTime { get; set; }

        [StringLength(200)]
        public string? Location { get; set; }

        [StringLength(1000)]
        public string? AdditionalNotes { get; set; }

        public List<string>? InviteeIds { get; set; }

        [StringLength(50)]
        public string? EventType { get; set; }
    }

    public class EventDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public TimeSpan? EventTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public string AdditionalNotes { get; set; } = string.Empty;
        public string CreatorId { get; set; } = string.Empty;
        public string CreatorUsername { get; set; } = string.Empty;
        public string CreatorAvatarUrl { get; set; } = string.Empty;
        public List<string> InviteeIds { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsCancelled { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int AcceptedCount { get; set; }
        public int DeclinedCount { get; set; }
        public int PendingCount { get; set; }
        public InvitationStatus? UserResponse { get; set; }
    }

    public class EventInvitationDTO
    {
        public string Id { get; set; } = string.Empty;
        public string EventId { get; set; } = string.Empty;
        public string InviteeId { get; set; } = string.Empty;
        public string InviterId { get; set; } = string.Empty;
        public InvitationStatus Status { get; set; }
        public DateTime InvitedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
        public string? ResponseMessage { get; set; }
        public EventDTO? Event { get; set; }
        public string InviterUsername { get; set; } = string.Empty;
        public string InviterAvatarUrl { get; set; } = string.Empty;
    }

    public class RespondToInvitationDTO
    {
        [Required]
        public InvitationStatus Status { get; set; }

        [StringLength(200)]
        public string? ResponseMessage { get; set; }
    }

    public class EventListDTO
    {
        public List<EventDTO> Events { get; set; } = new List<EventDTO>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class EventInvitationListDTO
    {
        public List<EventInvitationDTO> Invitations { get; set; } = new List<EventInvitationDTO>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
