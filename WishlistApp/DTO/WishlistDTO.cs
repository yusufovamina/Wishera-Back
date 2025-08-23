using System.ComponentModel.DataAnnotations;

namespace WishlistApp.DTO
{
    // Static categories for consistency across the application
    public static class WishlistCategories
    {
        public static readonly string[] Categories = {
            "Electronics",
            "Books",
            "Clothing",
            "Home & Garden",
            "Sports & Outdoors",
            "Beauty & Personal Care",
            "Toys & Games",
            "Food & Beverages",
            "Health & Wellness",
            "Automotive",
            "Travel",
            "Music",
            "Movies & TV",
            "Art & Crafts",
            "Jewelry & Accessories",
            "Pet Supplies",
            "Office & School",
            "Baby & Kids",
            "Other"
        };
    }

    public class CreateWishlistDTO
    {
        [Required]
        [StringLength(100)]
        public required string Title { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }
        public bool IsPublic { get; set; } = true;
        public List<string> AllowedViewerIds { get; set; } = new List<string>();
    }

    public class UpdateWishlistDTO
    {
        [StringLength(100)]
        public string? Title { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(50)]
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public List<string>? AllowedViewerIds { get; set; }
    }

    public class WishlistItemDTO
    {
        [Required]
        [StringLength(100)]
        public required string Title { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public decimal? Price { get; set; }
        public string? Url { get; set; }
        public string? GiftId { get; set; } // ID of the gift if it's from the Gift collection
    }

    public class WishlistResponseDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public List<WishlistItemDTO> Items { get; set; } = new List<WishlistItemDTO>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
        public bool IsOwner { get; set; }
    }

    public class WishlistFeedDTO
    {
        public required string Id { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
    }
} 