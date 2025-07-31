using Microsoft.AspNetCore.Http;

namespace WishlistApp.DTO
{
    public class GiftCreateDto
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; }
        public string WishlistId { get; set; }
        public IFormFile ImageFile { get; set; } // Файл изображения
    }
}
