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
        private readonly IUserService _userService;

        public NotificationsController(INotificationService notificationService, IUserService userService)
        {
            _notificationService = notificationService;
            _userService = userService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet]
        public async Task<ActionResult<NotificationListDTO>> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
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
        }

        [HttpPut("mark-read")]
        public async Task<ActionResult<object>> MarkAsRead([FromBody] MarkNotificationReadDTO markReadDto)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var result = await _notificationService.MarkNotificationsAsReadAsync(userId, markReadDto.NotificationIds);
                return Ok(new { success = result, message = "Notifications marked as read" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("mark-all-read")]
        public async Task<ActionResult<object>> MarkAllAsRead()
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var result = await _notificationService.MarkAllNotificationsAsReadAsync(userId);
                return Ok(new { success = result, message = "All notifications marked as read" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{notificationId}")]
        public async Task<ActionResult<object>> DeleteNotification(string notificationId)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                var result = await _notificationService.DeleteNotificationAsync(userId, notificationId);
                return Ok(new { success = result, message = result ? "Notification deleted" : "Notification not found" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("birthdays")]
        public async Task<ActionResult<List<BirthdayReminderDTO>>> GetUpcomingBirthdays([FromQuery] int daysAhead = 7)
        {
            try
            {
                var userId = GetCurrentUserId() ?? string.Empty;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }
                
                var birthdays = await _userService.GetUpcomingBirthdaysAsync(userId, daysAhead);
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
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching birthdays", details = ex.Message });
            }
        }
    }
}