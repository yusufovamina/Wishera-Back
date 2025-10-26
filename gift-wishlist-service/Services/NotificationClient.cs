using System.Text;
using System.Text.Json;

namespace gift_wishlist_service.Services
{
    public interface INotificationClient
    {
        Task CreateWishlistLikeNotificationAsync(string userId, string likerId, string wishlistId, string wishlistTitle);
        Task CreateGiftReservedNotificationAsync(string userId, string reserverId, string giftId, string giftName, string wishlistId);
        Task DeleteNotificationByTypeAndRelatedUserAsync(string userId, int notificationType, string relatedUserId, string? relatedEntityId = null);
    }

    public class NotificationClient : INotificationClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _userServiceUrl;

        public NotificationClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _userServiceUrl = configuration["Services:UserService"] ?? "http://localhost:5002";
        }

        public async Task CreateWishlistLikeNotificationAsync(string userId, string likerId, string wishlistId, string wishlistTitle)
        {
            try
            {
                var payload = new
                {
                    userId,
                    likerId,
                    wishlistId,
                    wishlistTitle
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{_userServiceUrl}/api/notifications/wishlist-like",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to create wishlist like notification: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating wishlist like notification: {ex.Message}");
            }
        }

        public async Task CreateGiftReservedNotificationAsync(string userId, string reserverId, string giftId, string giftName, string wishlistId)
        {
            try
            {
                var payload = new
                {
                    userId,
                    reserverId,
                    giftId,
                    giftName,
                    wishlistId
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(
                    $"{_userServiceUrl}/api/notifications/gift-reserved",
                    content
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to create gift reserved notification: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating gift reserved notification: {ex.Message}");
            }
        }

        public async Task DeleteNotificationByTypeAndRelatedUserAsync(string userId, int notificationType, string relatedUserId, string? relatedEntityId = null)
        {
            try
            {
                var queryParams = $"userId={userId}&notificationType={notificationType}&relatedUserId={relatedUserId}";
                if (!string.IsNullOrEmpty(relatedEntityId))
                {
                    queryParams += $"&relatedEntityId={relatedEntityId}";
                }

                var response = await _httpClient.DeleteAsync(
                    $"{_userServiceUrl}/api/notifications/by-type?{queryParams}"
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to delete notification: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting notification: {ex.Message}");
            }
        }
    }
}

