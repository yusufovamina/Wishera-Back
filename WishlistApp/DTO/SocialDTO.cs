using System.ComponentModel.DataAnnotations;

namespace WishlistApp.DTO
{
    public class CommentDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsOwner { get; set; }
    }

    public class CreateCommentDTO
    {
        [Required]
        [StringLength(1000)]
        public required string Text { get; set; }
    }

    public class UpdateCommentDTO
    {
        [Required]
        [StringLength(1000)]
        public required string Text { get; set; }
    }

    public class FeedEventDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string ActionType { get; set; }
        public string? WishlistId { get; set; }
        public string? WishlistTitle { get; set; }
        public string? ItemId { get; set; }
        public string? ItemTitle { get; set; }
        public string? CommentId { get; set; }
        public string? CommentText { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PaginatedResponseDTO<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }
} 