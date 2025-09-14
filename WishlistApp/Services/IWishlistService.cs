using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using WisheraApp.DTO;

namespace WisheraApp.Services
{
    public interface IWishlistService
    {
        Task<WishlistResponseDTO> CreateWishlistAsync(string userId, CreateWishlistDTO createDto);
        Task<WishlistResponseDTO> GetWishlistAsync(string id, string currentUserId);
        Task<WishlistResponseDTO> UpdateWishlistAsync(string id, string currentUserId, UpdateWishlistDTO updateDto);
        Task<bool> DeleteWishlistAsync(string id, string currentUserId);
        Task<List<WishlistFeedDTO>> GetUserWishlistsAsync(string userId, string currentUserId, int page, int pageSize);
        Task<List<WishlistFeedDTO>> GetFeedAsync(string currentUserId, int page, int pageSize);
        Task<bool> LikeWishlistAsync(string id, string currentUserId);
        Task<bool> UnlikeWishlistAsync(string id, string currentUserId);
        Task<CommentDTO> AddCommentAsync(string wishlistId, string userId, CreateCommentDTO commentDto);
        Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, UpdateCommentDTO commentDto);
        Task<bool> DeleteCommentAsync(string commentId, string userId);
        Task<List<CommentDTO>> GetCommentsAsync(string wishlistId, int page, int pageSize);
        Task<string> UploadItemImageAsync(IFormFile file);
        Task<int> CleanupCorruptedWishlistsAsync();
    }
}


