using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace auth_service.Filters
{
    public class CorsResultFilter : IAlwaysRunResultFilter
    {
        private static readonly string[] AllowedOrigins = new[]
        {
            "http://localhost:3000",
            "http://localhost:8081",
            "http://localhost:19000",
            "http://localhost:19006",
            "http://127.0.0.1:8081",
            "http://10.0.2.2:8081",
            "https://wishera.vercel.app"
        };

        public void OnResultExecuting(ResultExecutingContext context)
        {
            // Add CORS headers before result is executed
            var origin = context.HttpContext.Request.Headers["Origin"].ToString();
            if (!string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin))
            {
                if (!context.HttpContext.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                {
                    context.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = origin;
                    context.HttpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                }
            }
        }

        public void OnResultExecuted(ResultExecutedContext context)
        {
            // Ensure CORS headers are present even if they weren't added earlier
            if (!context.HttpContext.Response.HasStarted)
            {
                var origin = context.HttpContext.Request.Headers["Origin"].ToString();
                if (!string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin))
                {
                    if (!context.HttpContext.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
                    {
                        context.HttpContext.Response.Headers["Access-Control-Allow-Origin"] = origin;
                        context.HttpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    }
                }
            }
        }
    }
}

