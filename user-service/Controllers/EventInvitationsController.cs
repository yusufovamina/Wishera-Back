using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using user_service.Services;
using WisheraApp.DTO;
using user_service.Models;

namespace user_service.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class EventInvitationsController : ControllerBase
    {
        private readonly IEventService _eventService;

        public EventInvitationsController(IEventService eventService)
        {
            _eventService = eventService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
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

        [HttpPut("{invitationId}/respond")]
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

        [HttpGet("pending")]
        public async Task<ActionResult<EventInvitationListDTO>> GetPendingInvitations([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var invitations = await _eventService.GetUserInvitationsAsync(userId, page, pageSize);
                
                // Filter only pending invitations
                var pendingInvitations = invitations.Invitations
                    .Where(i => i.Status == InvitationStatus.Pending)
                    .ToList();

                return Ok(new EventInvitationListDTO
                {
                    Invitations = pendingInvitations,
                    TotalCount = pendingInvitations.Count,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)pendingInvitations.Count / pageSize)
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
