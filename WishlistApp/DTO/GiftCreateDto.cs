using Microsoft.AspNetCore.Http;

namespace WisheraApp.DTO
{
    public class GiftCreateDto
    {
        public required string Name { get; set; }
        public decimal Price { get; set; }
        public required string Category { get; set; }
        public required string WishlistId { get; set; }
        public required IFormFile ImageFile { get; set; } // Файл изображения
    }

    public class AssignGiftToWishlistDto
    {
        public required string WishlistId { get; set; }
    }
} 