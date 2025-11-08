using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using user_service.Services;
using user_service.Middleware;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using WisheraApp.DTO;
using WisheraApp.Models;
using Microsoft.Extensions.Caching.StackExchangeRedis;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com
var port = Environment.GetEnvironmentVariable("PORT") ?? "5001";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers().ConfigureApplicationPartManager(pm =>
{
	pm.ApplicationParts.Clear();
	pm.ApplicationParts.Add(new AssemblyPart(typeof(Program).Assembly));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
	o.DocumentFilter<user_service.SwaggerFilters.IncludeOnlyUsersFilter>();
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
	});

// Register core user logic (local services)
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<ICacheService, CacheService>();

// RabbitMQ RPC server
builder.Services.AddHostedService<UserRpcServer>();

// Redis distributed cache (optional - service will work without it)
var redisConnection = builder.Configuration.GetConnectionString("Redis")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Redis")
    ?? null;

if (!string.IsNullOrEmpty(redisConnection))
{
    try
    {
        // Fix Upstash Redis connection string format
        var connectionString = redisConnection;
        
        // Remove duplicate port if present (some connection strings have :6379:6379)
        if (connectionString.Contains(":6379:6379"))
        {
            connectionString = connectionString.Replace(":6379:6379", ":6379");
        }
        
        // For Upstash Redis, convert redis:// to rediss:// for TLS
        // Upstash requires TLS by default
        if (connectionString.StartsWith("redis://") && connectionString.Contains("upstash.io"))
        {
            connectionString = connectionString.Replace("redis://", "rediss://");
        }
        
        // Parse the connection string and configure options
        var configOptions = StackExchange.Redis.ConfigurationOptions.Parse(connectionString);
        configOptions.AbortOnConnectFail = false; // Don't fail service if Redis is unavailable
        configOptions.ConnectTimeout = 2000; // 2 seconds timeout (reduced to fail faster)
        configOptions.SyncTimeout = 2000;
        configOptions.AsyncTimeout = 2000;
        configOptions.ConnectRetry = 0; // Don't retry connections - fail fast
        configOptions.ReconnectRetryPolicy = null; // Don't auto-reconnect
        
        // For Upstash, ensure SSL is enabled
        if (connectionString.Contains("upstash.io"))
        {
            configOptions.Ssl = true;
            configOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
        }
        
        // Use the configured options
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConfigurationOptions = configOptions;
            options.InstanceName = "wishera:";
        });
        
        Console.WriteLine($"Redis cache configured with connection string: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
        Console.WriteLine("Note: Redis is optional - service will work without it if connection fails.");
    }
    catch (Exception ex)
    {
        // Log error but don't fail service startup if Redis config is invalid
        Console.WriteLine($"Warning: Failed to configure Redis cache: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        Console.WriteLine("Service will continue without Redis caching (using in-memory cache).");
        // Use in-memory cache as fallback
        builder.Services.AddDistributedMemoryCache();
    }
}
else
{
    // No Redis connection string provided, use in-memory cache as fallback
    Console.WriteLine("No Redis connection string provided. Using in-memory cache.");
    builder.Services.AddDistributedMemoryCache();
}

// Register exception middleware
builder.Services.AddSingleton<GlobalExceptionMiddleware>();
builder.Services.AddSingleton<ILogger<GlobalExceptionMiddleware>>(sp => 
    sp.GetRequiredService<ILoggerFactory>().CreateLogger<GlobalExceptionMiddleware>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// Handle OPTIONS requests for CORS preflight
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        var origin = context.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin))
        {
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS, PATCH";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, X-Requested-With";
            context.Response.Headers["Access-Control-Max-Age"] = "3600";
        }
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync(string.Empty);
        return;
    }
    await next();
});

app.UseRouting();
app.UseCors("Frontend");
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
