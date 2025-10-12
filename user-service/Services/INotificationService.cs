using WisheraApp.DTO;
using WisheraApp.Models;

namespace user_service.Services
{
    public interface INotificationService
    {
        Task<List<NotificationDTO>> GetUserNotificationsAsync(string userId, int page = 1, int pageSize = 20);
        Task<NotificationDTO> CreateNotificationAsync(CreateNotificationDTO notificationDto);
        Task<bool> MarkNotificationAsReadAsync(string notificationId, string userId);
        Task<bool> MarkAllNotificationsAsReadAsync(string userId);
        Task<int> GetUnreadNotificationCountAsync(string userId);
        Task<List<BirthdayReminderDTO>> GetUpcomingBirthdaysAsync(string userId, int daysAhead = 7);
        Task CreateBirthdayRemindersAsync();
        Task DeleteExpiredNotificationsAsync();
    }
}


