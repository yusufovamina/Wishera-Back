using System.ComponentModel.DataAnnotations;

namespace WisheraApp.DTO
{
    public class CreateCommentDTO
    {
        [Required]
        [StringLength(1000)]
        public required string Text { get; set; }
        
        [Required]
        public required string WishlistId { get; set; }
    }

    public class UpdateCommentDTO
    {
        [Required]
        [StringLength(1000)]
        public required string Text { get; set; }
    }

    public class CommentDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string WishlistId { get; set; }
        public required string Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsEdited { get; set; }
        public bool IsOwner { get; set; }
    }
} 