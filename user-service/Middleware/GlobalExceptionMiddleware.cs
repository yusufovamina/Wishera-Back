using System.Net;
using System.Text.Json;

namespace user_service.Middleware
{
    public class GlobalExceptionMiddleware : IMiddleware
    {
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
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

            // Clear any existing response and ensure CORS headers are included
            context.Response.Clear();
            
            // Ensure CORS headers are included in error response
            var origin = context.Request.Headers["Origin"].ToString();
            if (!string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin))
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
                context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
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

