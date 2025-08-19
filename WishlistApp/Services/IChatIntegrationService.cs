namespace WishlistApp.Services
{
    public interface IChatIntegrationService
    {
        Task<string> GetUserTokenAsync(string userId);
        Task UpsertUserAsync(string userId);
        Task CreateChannelAsync(string channelType, string channelId, string createdByUserId, IEnumerable<string> memberIds);
        Task SendMessageAsync(string channelType, string channelId, string senderUserId, string text);
    }
}
