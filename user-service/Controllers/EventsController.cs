using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using user_service.Services;
using WisheraApp.DTO;
using MongoDB.Driver;

namespace user_service.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;
        private readonly MongoDbContext _dbContext;

        public EventsController(IEventService eventService, MongoDbContext dbContext)
        {
            _eventService = eventService;
            _dbContext = dbContext;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet("debug-all-invitations")]
        public async Task<ActionResult> DebugAllInvitations()
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var allInvitations = await _dbContext.EventInvitations.Find(_ => true).ToListAsync();
                return Ok(new { 
                    userId = userId,
                    totalInvitations = allInvitations.Count,
                    invitations = allInvitations.Select(i => new { 
                        id = i.Id, 
                        eventId = i.EventId, 
                        inviteeId = i.InviteeId, 
                        inviterId = i.InviterId,
                        status = i.Status.ToString(),
                        invitedAt = i.InvitedAt,
                        respondedAt = i.RespondedAt
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("debug/user-id")]
        public ActionResult DebugUserId()
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var isValid = MongoDB.Bson.ObjectId.TryParse(userId, out var objectId);
                return Ok(new { 
                    userId = userId,
                    userIdLength = userId.Length,
                    isValid = isValid,
                    objectId = isValid ? objectId.ToString() : null
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<ActionResult<EventDTO>> CreateEvent([FromBody] CreateEventDTO createEventDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                if (createEventDto == null)
                {
                    return BadRequest(new { message = "Event data is required." });
                }

                var eventDto = await _eventService.CreateEventAsync(userId, createEventDto);
                return CreatedAtAction(nameof(GetEvent), new { id = eventDto.Id }, eventDto);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Event creation timeout: {ex.Message}");
                return StatusCode(504, new { message = "Request timed out. Please try again." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while creating the event. Please try again later.", details = ex.Message });
            }
        }

        [HttpGet("my-events")]
        public async Task<ActionResult<EventListDTO>> GetMyEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var events = await _eventService.GetUserEventsAsync(userId, page, pageSize);
                return Ok(events);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("invited-events")]
        public async Task<ActionResult<EventListDTO>> GetInvitedEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var events = await _eventService.GetInvitedEventsAsync(userId, page, pageSize);
                return Ok(events);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching invited events: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while fetching invited events", details = ex.Message });
            }
        }

        [HttpGet("my-invitations")]
        public async Task<ActionResult<EventInvitationListDTO>> GetMyInvitations([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var invitations = await _eventService.GetUserInvitationsAsync(userId, page, pageSize);
                return Ok(invitations);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EventDTO>> GetEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var eventDto = await _eventService.GetEventByIdAsync(id, userId);
                if (eventDto == null)
                    return NotFound(new { message = "Event not found" });
                return Ok(eventDto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<EventDTO>> UpdateEvent(string id, [FromBody] UpdateEventDTO updateEventDto)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var eventDto = await _eventService.UpdateEventAsync(id, userId, updateEventDto);
                return Ok(eventDto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/cancel")]
        public async Task<ActionResult<object>> CancelEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var result = await _eventService.CancelEventAsync(id, userId);
                return Ok(new { message = "Event cancelled successfully", success = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<object>> DeleteEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var result = await _eventService.DeleteEventAsync(id, userId);
                return Ok(new { message = "Event deleted successfully", success = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpGet("{id}/invitations")]
        public async Task<ActionResult<List<EventInvitationDTO>>> GetEventInvitations(string id)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var invitations = await _eventService.GetEventInvitationsAsync(id, userId);
                return Ok(invitations);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("invitations/{invitationId}/respond")]
        public async Task<ActionResult<EventInvitationDTO>> RespondToInvitation(string invitationId, [FromBody] RespondToInvitationDTO responseDto)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var invitation = await _eventService.RespondToInvitationAsync(invitationId, userId, responseDto);
                return Ok(invitation);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}
