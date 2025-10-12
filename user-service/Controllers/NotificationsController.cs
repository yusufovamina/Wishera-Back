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
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<ActionResult<List<NotificationDTO>>> GetNotifications(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);
                return Ok(notifications);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while retrieving notifications." });
            }
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<object>> GetUnreadCount()
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
                return Ok(new { unreadCount = count });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while retrieving unread count." });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<ActionResult<object>> MarkAsRead(string id)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var success = await _notificationService.MarkNotificationAsReadAsync(id, userId);
                
                if (success)
                {
                    return Ok(new { message = "Notification marked as read" });
                }
                else
                {
                    return NotFound(new { message = "Notification not found" });
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while marking notification as read." });
            }
        }

        [HttpPut("read-all")]
        public async Task<ActionResult<object>> MarkAllAsRead()
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var success = await _notificationService.MarkAllNotificationsAsReadAsync(userId);
                
                if (success)
                {
                    return Ok(new { message = "All notifications marked as read" });
                }
                else
                {
                    return Ok(new { message = "No unread notifications found" });
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while marking all notifications as read." });
            }
        }

        [HttpGet("birthdays")]
        public async Task<ActionResult<List<BirthdayReminderDTO>>> GetUpcomingBirthdays(
            [FromQuery] int daysAhead = 7)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var birthdays = await _notificationService.GetUpcomingBirthdaysAsync(userId, daysAhead);
                return Ok(birthdays);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, 
                    new { message = "An error occurred while retrieving upcoming birthdays." });
            }
        }
    }
}

