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
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
                }
                
                var count = await _notificationService.GetUnreadNotificationCountAsync(userId);
                return Ok(new { unreadCount = count });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching unread count", details = ex.Message });
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

        [HttpDelete("by-type")]
        [AllowAnonymous] // Allow cross-service calls
        public async Task<ActionResult<object>> DeleteNotificationByType(
            [FromQuery] string userId, 
            [FromQuery] int notificationType, 
            [FromQuery] string relatedUserId,
            [FromQuery] string? relatedEntityId = null)
        {
            try
            {
                var result = await _notificationService.DeleteNotificationByTypeAndRelatedUserAsync(
                    userId, 
                    (Models.NotificationType)notificationType, 
                    relatedUserId, 
                    relatedEntityId
                );
                return Ok(new { success = result, message = result ? "Notification(s) deleted" : "Notification not found" });
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
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated. Please log in." });
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
                Console.WriteLine($"Error fetching birthdays: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred while fetching birthdays", details = ex.Message });
            }
        }

        [HttpPost("wishlist-like")]
        [AllowAnonymous] // Allow cross-service calls
        public async Task<ActionResult<NotificationDTO>> CreateWishlistLikeNotification([FromBody] WishlistLikeNotificationRequest request)
        {
            try
            {
                var notification = await _notificationService.CreateWishlistLikeNotificationAsync(
                    request.UserId,
                    request.LikerId,
                    request.WishlistId,
                    request.WishlistTitle
                );
                return Ok(notification);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("gift-reserved")]
        [AllowAnonymous] // Allow cross-service calls
        public async Task<ActionResult<NotificationDTO>> CreateGiftReservedNotification([FromBody] GiftReservedNotificationRequest request)
        {
            try
            {
                var notification = await _notificationService.CreateGiftReservedNotificationAsync(
                    request.UserId,
                    request.ReserverId,
                    request.GiftId,
                    request.GiftName,
                    request.WishlistId
                );
                return Ok(notification);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("user-suggestions")]
        [AllowAnonymous] // Allow internal calls
        public async Task<ActionResult<object>> CreateUserSuggestions()
        {
            try
            {
                var notifications = await _notificationService.CreateUserSuggestionsAsync();
                return Ok(new { count = notifications.Count, message = $"Created {notifications.Count} user suggestion notifications" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating user suggestions", details = ex.Message });
            }
        }
    }

    public class WishlistLikeNotificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string LikerId { get; set; } = string.Empty;
        public string WishlistId { get; set; } = string.Empty;
        public string WishlistTitle { get; set; } = string.Empty;
    }

    public class GiftReservedNotificationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string ReserverId { get; set; } = string.Empty;
        public string GiftId { get; set; } = string.Empty;
        public string GiftName { get; set; } = string.Empty;
        public string WishlistId { get; set; } = string.Empty;
    }
}