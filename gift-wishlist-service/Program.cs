using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using gift_wishlist_service.Services;
using WisheraApp.DTO;
using WisheraApp.Models;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using gift_wishlist_service.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com
var port = Environment.GetEnvironmentVariable("PORT") ?? "5003";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers().ConfigureApplicationPartManager(apm =>
{
    apm.ApplicationParts.Clear();
    apm.ApplicationParts.Add(new AssemblyPart(typeof(Program).Assembly));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.DocumentFilter<gift_wishlist_service.SwaggerFilters.IncludeOnlyWishlistAndGiftFilter>();
});

// Mongo
var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB"));
var database = mongoClient.GetDatabase("WishlistApp");
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<MongoDbContext>(sp => new MongoDbContext(database));

// Cloudinary
var cloudinaryAccount = new Account(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]
);
var cloudinary = new Cloudinary(cloudinaryAccount);
builder.Services.AddSingleton(cloudinary);

// JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured"))),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
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
                var allowedOrigins = new[] { "http://localhost:3000", "http://localhost:8081", "http://localhost:19000", "http://localhost:19006", "http://127.0.0.1:8081", "http://10.0.2.2:8081", "https://wishera.vercel.app" };
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
                var allowedOrigins = new[] { "http://localhost:3000", "http://localhost:8081", "http://localhost:19000", "http://localhost:19006", "http://127.0.0.1:8081", "http://10.0.2.2:8081", "https://wishera.vercel.app" };
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

// Register core services (match hosted service singleton lifetime)
builder.Services.AddSingleton<WisheraApp.Services.IWishlistService, WishlistService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddSingleton<IGiftApiService, GiftApiService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

// Register global exception handling middleware
builder.Services.AddTransient<GlobalExceptionMiddleware>();

// HTTP Client for cross-service communication
builder.Services.AddHttpClient<INotificationClient, NotificationClient>();

// RabbitMQ RPC server
builder.Services.AddHostedService<GiftWishlistRpcServer>();

// Redis distributed cache
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
    ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "wishera:";
});

// CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(
			"http://localhost:3000",      // Web frontend
			"http://localhost:8081",      // React Native Metro bundler
			"http://localhost:19000",     // Expo development
			"http://localhost:19006",     // Expo tunnel
			"http://127.0.0.1:8081",       // iOS simulator
			"http://10.0.2.2:8081",       // Android emulator
			"https://wishera.vercel.app"  // Production frontend
		)
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply CORS early to ensure headers are sent even on errors
app.UseRouting();
app.UseCors("Frontend");

// Add global exception handling middleware (must be after CORS but before controllers)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
