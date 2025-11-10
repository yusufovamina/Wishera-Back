using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace gift_wishlist_service.SwaggerFilters
{
    public class IncludeOnlyWishlistAndGiftFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var allowedPrefixes = new[] { "/api/Wishlists", "/api/Gift" };
            var toRemove = swaggerDoc.Paths.Keys
                .Where(p => !allowedPrefixes.Any(a => p.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var key in toRemove)
            {
                swaggerDoc.Paths.Remove(key);
            }
        }
    }
}


