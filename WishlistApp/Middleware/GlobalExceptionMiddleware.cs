using System;
using System.Net;
using System.Text.Json;
using System.Linq;

namespace WisheraApp.Middleware
{
    public class GlobalExceptionMiddleware : IMiddleware
    {
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
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

        public GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // If response has started, we can't modify headers
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("Response has already started, cannot add CORS headers");
                return;
            }

            var statusCode = HttpStatusCode.InternalServerError;
            var message = "An error occurred while processing your request.";

            // Determine status code based on exception type
            if (exception is ArgumentException)
            {
                statusCode = HttpStatusCode.BadRequest;
                message = exception.Message;
            }
            else if (exception is KeyNotFoundException)
            {
                statusCode = HttpStatusCode.NotFound;
                message = exception.Message;
            }
            else if (exception is UnauthorizedAccessException)
            {
                statusCode = HttpStatusCode.Unauthorized;
                message = "Unauthorized access.";
            }
            else if (exception is TimeoutException || exception is InvalidOperationException)
            {
                statusCode = HttpStatusCode.BadGateway;
                message = exception.Message.Contains("RabbitMQ") || exception.Message.Contains("not available")
                    ? "Service temporarily unavailable. Please try again later."
                    : exception.Message;
            }

            // Clear any existing response and ensure CORS headers are included
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
            }
            
            // Ensure CORS headers are included in error response
            // Render proxy: Try to get origin from Origin header first, then Referer as fallback
            var origin = context.Request.Headers["Origin"].ToString();
            if (string.IsNullOrEmpty(origin))
            {
                // Render proxy: If no Origin header, try to extract from Referer header
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
                        // Invalid referer, try simple string match as fallback
                        if (referer.Contains("wishera.vercel.app"))
                        {
                            origin = "https://wishera.vercel.app";
                        }
                    }
                }
            }
            
            // Check if origin is allowed (normalized comparison for Render proxy compatibility)
            bool isAllowed = false;
            if (!string.IsNullOrEmpty(origin))
            {
                var normalizedOrigin = origin.TrimEnd('/').ToLowerInvariant();
                isAllowed = AllowedOrigins.Any(o => 
                {
                    var normalizedAllowed = o.TrimEnd('/').ToLowerInvariant();
                    return normalizedOrigin == normalizedAllowed || 
                           normalizedOrigin.StartsWith(normalizedAllowed, StringComparison.OrdinalIgnoreCase);
                });
            }
            
            if (isAllowed && !context.Response.HasStarted)
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
                context.Response.Headers["Access-Control-Expose-Headers"] = "*";
            }

            var response = new
            {
                message = message,
                error = exception.Message,
                statusCode = (int)statusCode
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}

