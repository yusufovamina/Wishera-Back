using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using WisheraApp.Services;
using WisheraApp.Middleware;
using WisheraApp.Filters;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com - Render provides PORT environment variable
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    // Use + to bind to all interfaces (both IPv4 and IPv6)
    builder.WebHost.UseUrls($"http://+:{port}");
}

// Add services to the container
builder.Services.AddControllers(options =>
{
    // Add CORS result filter to ensure headers are always present
    options.Filters.Add<CorsResultFilter>();
});

// API gateway does not use local Mongo or Cloudinary; those are in microservices

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured"))),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        
        // Improve error handling for authentication failures
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
                
                // Add CORS headers to error response (validate origin)
                var origin = context.Request.Headers["Origin"].ToString();
                var allowedOrigins = new[] { "http://localhost:3000", "http://localhost:8081", "http://localhost:19000", "http://localhost:19006", "http://127.0.0.1:8081", "http://10.0.2.2:8081", "https://wishera.vercel.app", "https://wishera.vercel.app/" };
                if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
                {
                    context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                    context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                }
                
                context.NoResult();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new { message = "Authentication failed. Please log in again." });
                return context.Response.WriteAsync(result);
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"JWT Challenge: {context.Error}, {context.ErrorDescription}");
                
                // Add CORS headers to error response (validate origin)
                var origin = context.Request.Headers["Origin"].ToString();
                var allowedOrigins = new[] { "http://localhost:3000", "http://localhost:8081", "http://localhost:19000", "http://localhost:19006", "http://127.0.0.1:8081", "http://10.0.2.2:8081", "https://wishera.vercel.app", "https://wishera.vercel.app/" };
                if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
                {
                    context.Response.Headers.Append("Access-Control-Allow-Origin", origin);
                    context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
                }
                
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new { message = "Authentication required. Please log in." });
                return context.Response.WriteAsync(result);
            }
        };
    });

// Register services
builder.Services.AddHttpClient<IAuthService, AuthService>();
builder.Services.AddSingleton<IUserServiceClient, UserServiceClient>();
builder.Services.AddSingleton<IGiftWishlistServiceClient, GiftWishlistServiceClient>();
builder.Services.AddHttpClient<IEventServiceClient, EventServiceClient>();
// Email is handled in auth-service

// Register Chat Integration Service
builder.Services.AddHttpClient<IChatIntegrationService, ChatIntegrationService>();

// Register global exception handling middleware
builder.Services.AddTransient<GlobalExceptionMiddleware>();

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WishlistApp API",
        Version = "v1",
        Description = "A social network for sharing wishlists"
    });

    // Configure Swagger to use JWT Authentication
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.WithOrigins(
            "http://localhost:3000",      // Web frontend
            "http://localhost:8081",      // React Native Metro bundler
            "http://localhost:19000",     // Expo development
            "http://localhost:19006",     // Expo tunnel
            "http://127.0.0.1:8081",       // iOS simulator
            "http://10.0.2.2:8081",       // Android emulator
            "https://wishera.vercel.app", // Production frontend
            "https://wishera.vercel.app/" // Production frontend (with trailing slash)
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// In dev we run HTTP locally; disable HTTPS redirection to avoid port mismatch
// app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAll");

// Add global exception handling middleware (must be after CORS but before controllers)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Ensure CORS headers are applied to all error responses (backup for when CORS middleware misses them)
app.Use(async (context, next) =>
{
    await next();
    
    // If this is an error response and CORS headers aren't present, add them
    // Only modify if response hasn't started
    if (!context.Response.HasStarted && 
        context.Response.StatusCode >= 400 && 
        !context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
    {
        var origin = context.Request.Headers["Origin"].ToString();
        var allowedOrigins = new[] { "http://localhost:3000", "http://localhost:8081", "http://localhost:19000", "http://localhost:19006", "http://127.0.0.1:8081", "http://10.0.2.2:8081", "https://wishera.vercel.app", "https://wishera.vercel.app/" };
        if (!string.IsNullOrEmpty(origin) && allowedOrigins.Contains(origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
        }
    }
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

// No local DB index management in gateway

app.Run();
