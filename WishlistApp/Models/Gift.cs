using System;

namespace WishlistApp.Models
{
    public class Gift
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string Name { get; set; }
        public required decimal Price { get; set; }
        public string ImageUrl { get; set; } = ""; // Ensure default value
        public required string Category { get; set; }
        public required string WishlistId { get; set; }
        public string? ReservedByUserId { get; set; } = null;
        public string? ReservedByUsername { get; set; } = null; // ✅ Store username
    }
} 