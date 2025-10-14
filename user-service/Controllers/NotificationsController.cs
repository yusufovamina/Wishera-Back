using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using user_service.Services;

namespace user_service.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly IUserService _userService;

        public NotificationsController(IUserService userService)
        {
            _userService = userService;
        }

        private string? GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        [HttpGet("birthdays")]
        public async Task<ActionResult<List<BirthdayReminderDTO>>> GetUpcomingBirthdays([FromQuery] int daysAhead = 7)
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                var birthdays = await _userService.GetUpcomingBirthdaysAsync(currentUserId, daysAhead);
                return Ok(birthdays);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while fetching birthday notifications." });
            }
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<object>> GetUnreadNotificationCount()
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                var count = await _userService.GetUnreadNotificationCountAsync(currentUserId);
                return Ok(new { unreadCount = count });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while fetching notification count." });
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<NotificationDTO>>> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                var notifications = await _userService.GetNotificationsAsync(currentUserId, page, pageSize);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while fetching notifications." });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<ActionResult<object>> MarkAsRead(string id)
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                await _userService.MarkNotificationAsReadAsync(currentUserId, id);
                return Ok(new { message = "Notification marked as read" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Notification not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while marking notification as read." });
            }
        }

        [HttpPut("read-all")]
        public async Task<ActionResult<object>> MarkAllAsRead()
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                await _userService.MarkAllNotificationsAsReadAsync(currentUserId);
                return Ok(new { message = "All notifications marked as read" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while marking all notifications as read." });
            }
        }

        [HttpGet("debug-birthdays")]
        public async Task<ActionResult<object>> DebugBirthdays()
        {
            try
            {
                var currentUserId = GetCurrentUserId() ?? string.Empty;
                var debugInfo = await _userService.GetDebugBirthdayInfoAsync(currentUserId);
                return Ok(debugInfo);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while getting debug info." });
            }
        }
    }

    public class BirthdayReminderDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string Birthday { get; set; }
        public bool IsToday { get; set; }
        public bool IsTomorrow { get; set; }
        public int DaysUntilBirthday { get; set; }
    }

    public class NotificationDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Type { get; set; }
        public required string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? RelatedUserId { get; set; }
        public string? RelatedUsername { get; set; }
        public string? RelatedUserAvatar { get; set; }
    }
}
