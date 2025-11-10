using MongoDB.Driver;
using WisheraApp.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudinaryDotNet;
using WisheraApp.DTO;
using Microsoft.Extensions.Configuration;
using BCrypt.Net;
using user_service.Controllers;


namespace user_service.Services
{
    public interface IUserService
    {
        Task<UserProfileDTO> GetUserProfileAsync(string userId, string currentUserId);
        Task<UserProfileDTO> UpdateUserProfileAsync(string userId, UpdateUserProfileDTO updateDto);
        Task<string> UpdateAvatarAsync(string userId, IFormFile file);
        Task<bool> FollowUserAsync(string followerId, string followingId);
        Task<bool> UnfollowUserAsync(string followerId, string followingId);
        Task<List<UserSearchDTO>> SearchUsersAsync(string query, string currentUserId, int page, int pageSize);
        Task<List<UserSearchDTO>> GetFollowersAsync(string userId, string currentUserId, int page, int pageSize);
        Task<List<UserSearchDTO>> GetFollowingAsync(string userId, string currentUserId, int page, int pageSize);
        Task<List<UserSearchDTO>> GetSuggestedUsersAsync(string currentUserId, int page, int pageSize);
        Task<bool> UserExistsAsync(string userId);
        Task<User> GetUserByIdAsync(string userId);
        
        // Notification methods
        Task<List<BirthdayReminderDTO>> GetUpcomingBirthdaysAsync(string currentUserId, int daysAhead);
        Task<int> GetUnreadNotificationCountAsync(string userId);
        Task<List<NotificationDTO>> GetNotificationsAsync(string userId, int page, int pageSize);
        Task MarkNotificationAsReadAsync(string userId, string notificationId);
        Task MarkAllNotificationsAsReadAsync(string userId);
        Task UpdateBirthdayAsync(string userId, string birthday);
        Task<object> GetDebugBirthdayInfoAsync(string userId);
    }
}
