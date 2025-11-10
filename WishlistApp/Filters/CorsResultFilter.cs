using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WisheraApp.Filters
{
    public class CorsResultFilter : IAlwaysRunResultFilter
    {
        private static readonly string[] AllowedOrigins = new[]
        {
            "http://localhost:3000",
            "http://localhost:3001",
            "http://localhost:8081",
            "http://localhost:19000",
            "http://localhost:19006",
            "http://127.0.0.1:3000",
            "http://127.0.0.1:8081",
            "http://10.0.2.2:8081",
            "https://wishera.vercel.app",
            "https://wishera.vercel.app/"
        };

        public void OnResultExecuting(ResultExecutingContext context)
        {
            // Add CORS headers before result is executed
            // This is critical - headers must be added BEFORE the response body is written
            AddCorsHeaders(context.HttpContext);
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // Ensure CORS headers are present even if they weren't added earlier
            if (!context.HttpContext.Response.HasStarted)
            {
                AddCorsHeaders(context.HttpContext);
            }
        }

        private void AddCorsHeaders(HttpContext context)
        {
            // Always try to add headers, even if CORS middleware might have added them
            // This ensures headers are present in all cases
            
            var origin = context.Request.Headers["Origin"].ToString();
            if (string.IsNullOrEmpty(origin))
            {
                // Render proxy: If no Origin header, try to get it from Referer (fallback)
                var referer = context.Request.Headers["Referer"].ToString();
                if (!string.IsNullOrEmpty(referer))
                {
                    try
                    {
                        var uri = new Uri(referer);
                        origin = $"{uri.Scheme}://{uri.Host}" + (uri.Port != 80 && uri.Port != 443 ? $":{uri.Port}" : "");
                    }
                    catch
                    {
                        // Invalid referer, skip
                    }
                }
                
                // Also check HttpContext.Items (set by our middleware)
                if (string.IsNullOrEmpty(origin) && context.Items.ContainsKey("CorsOrigin"))
                {
                    origin = context.Items["CorsOrigin"]?.ToString() ?? string.Empty;
                }
                
                if (string.IsNullOrEmpty(origin))
                {
                    return;
                }
            }

            // Normalize origin (remove trailing slash, lowercase for comparison)
            var normalizedOrigin = origin.TrimEnd('/').ToLowerInvariant();

            // Check if origin is allowed (exact match or starts with check)
            // Render proxy passes through the original Origin header, so this should work
            bool isAllowed = AllowedOrigins.Any(o => 
            {
                var normalizedAllowed = o.TrimEnd('/').ToLowerInvariant();
                return normalizedOrigin == normalizedAllowed || 
                       normalizedOrigin.StartsWith(normalizedAllowed, StringComparison.OrdinalIgnoreCase);
            });

            // Also check HttpContext.Items (set by our middleware)
            if (!isAllowed && context.Items.ContainsKey("CorsAllowed"))
            {
                isAllowed = context.Items["CorsAllowed"] is bool allowed && allowed;
            }

            if (isAllowed)
            {
                // ALWAYS set headers, even if they might already be present
                // This ensures headers are definitely there
                // Following Vercel CORS guide: https://vercel.com/guides/how-to-enable-cors
                // All required headers must be present for CORS to work properly
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
                // Note: Access-Control-Max-Age is set in the CORS policy (86400 seconds = 24 hours)
            }
        }
    }
}

