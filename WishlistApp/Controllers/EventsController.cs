using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WisheraApp.DTO;
using WisheraApp.Services;

namespace WisheraApp.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventServiceClient _eventServiceClient;

        public EventsController(IEventServiceClient eventServiceClient)
        {
            _eventServiceClient = eventServiceClient;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        private string GetAuthToken()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                throw new UnauthorizedAccessException("Authorization token is missing");
            }
            return authHeader.Substring(7); // Remove "Bearer " prefix
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

                var token = GetAuthToken();
                var eventDto = await _eventServiceClient.CreateEventAsync(userId, createEventDto, token);
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
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP error creating event: {ex.Message}");
                return StatusCode(502, new { message = "Failed to communicate with event service. Please try again later." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating event: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while creating the event. Please try again later.", details = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<EventDTO>> GetEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var eventDto = await _eventServiceClient.GetEventByIdAsync(id, userId, token);
                if (eventDto == null)
                    return NotFound(new { message = "Event not found" });
                return Ok(eventDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while fetching the event." });
            }
        }

        [HttpGet("my-events")]
        public async Task<ActionResult<EventListDTO>> GetMyEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var events = await _eventServiceClient.GetUserEventsAsync(userId, userId, page, pageSize, token);
                return Ok(events);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting my events: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while fetching events." });
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

                var token = GetAuthToken();
                var events = await _eventServiceClient.GetInvitedEventsAsync(userId, page, pageSize, token);
                return Ok(events);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting invited events: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while fetching invited events." });
            }
        }

        [HttpGet("my-invitations")]
        public async Task<ActionResult<EventListDTO>> GetMyInvitations([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var invitations = await _eventServiceClient.GetMyInvitationsAsync(userId, page, pageSize, token);
                return Ok(invitations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting invitations: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while fetching invitations." });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<EventDTO>> UpdateEvent(string id, [FromBody] UpdateEventDTO updateEventDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var eventDto = await _eventServiceClient.UpdateEventAsync(id, userId, updateEventDto, token);
                return Ok(eventDto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating event: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while updating the event." });
            }
        }

        [HttpPut("{id}/cancel")]
        public async Task<ActionResult<object>> CancelEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var result = await _eventServiceClient.CancelEventAsync(id, userId, token);
                return Ok(new { message = "Event cancelled successfully", success = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cancelling event: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while cancelling the event." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<object>> DeleteEvent(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var result = await _eventServiceClient.DeleteEventAsync(id, userId, token);
                return Ok(new { message = "Event deleted successfully", success = result });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting event: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while deleting the event." });
            }
        }

        [HttpGet("{id}/invitations")]
        public async Task<ActionResult<List<EventInvitationDTO>>> GetEventInvitations(string id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var invitations = await _eventServiceClient.GetEventInvitationsAsync(id, userId, token);
                return Ok(invitations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting event invitations: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while fetching invitations." });
            }
        }

        [HttpPost("invitations/{invitationId}/respond")]
        public async Task<ActionResult<EventInvitationDTO>> RespondToInvitation(string invitationId, [FromBody] RespondToInvitationDTO responseDto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }

                var token = GetAuthToken();
                var invitation = await _eventServiceClient.RespondToInvitationAsync(invitationId, userId, responseDto, token);
                return Ok(invitation);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error responding to invitation: {ex.Message}");
                return StatusCode(500, new { message = "An error occurred while responding to the invitation." });
            }
        }
    }
}

