using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using user_service.Services;
using WisheraApp.DTO;

namespace user_service.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventsController(IEventService eventService)
        {
            _eventService = eventService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpPost]
        public async Task<ActionResult<EventDTO>> CreateEvent([FromBody] CreateEventDTO createEventDto)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var eventDto = await _eventService.CreateEventAsync(userId, createEventDto);
                return CreatedAtAction(nameof(GetEvent), new { id = eventDto.Id }, eventDto);
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
                var userId = GetCurrentUserId() ?? string.Empty;
                var events = await _eventService.GetInvitedEventsAsync(userId, page, pageSize);
                return Ok(events);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
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
    }
}
