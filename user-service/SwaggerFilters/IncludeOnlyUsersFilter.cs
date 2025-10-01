using System;
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace user_service.SwaggerFilters
{
    public class IncludeOnlyUsersFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var allowedPrefix = "/api/Users";
            var toRemove = swaggerDoc.Paths.Keys
                .Where(p => !p.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in toRemove)
            {
                swaggerDoc.Paths.Remove(key);
            }
        }
    }
}


