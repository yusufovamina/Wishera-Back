using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Linq;
using WisheraApp.Services;
using WisheraApp.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Keep original property names
    });

// Forwarded Headers - Required for detecting HTTPS when behind a proxy (like Render.com)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | 
                               ForwardedHeaders.XForwardedHost |
                               ForwardedHeaders.XForwardedFor;
    // Clear known networks and proxies to allow any proxy
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// API gateway does not use local Mongo or Cloudinary; those are in microservices

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Allow anonymous access for OPTIONS requests (CORS preflight)
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Skip token validation for OPTIONS requests (CORS preflight)
                if (context.Request.Method == "OPTIONS")
                {
                    context.Token = null; // Don't validate token for OPTIONS
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                // Don't send challenge response for OPTIONS requests
                if (context.Request.Method == "OPTIONS")
                {
                    context.HandleResponse();
                    return Task.CompletedTask;
                }
                
                // Add CORS headers to authentication challenge responses
                var origin = context.Request.Headers["Origin"].ToString();
                if (!string.IsNullOrEmpty(origin))
                {
                    var allowedOrigins = new[] {
                        "http://localhost:3000", "http://localhost:3001", "http://localhost:8081",
                        "http://localhost:19000", "http://localhost:19006",
                        "http://127.0.0.1:3000", "http://127.0.0.1:8081",
                        "http://10.0.2.2:8081",
                        "https://wishera.vercel.app"
                    };
                    
                    if (allowedOrigins.Any(o => origin.StartsWith(o.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
                    {
                        context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                        context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                    }
                }
                
                return Task.CompletedTask;
            }
        };
        
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
    });

// Authorization is configured with default policy (controllers use [Authorize] attribute)
builder.Services.AddAuthorization();

// Register Global Exception Middleware
builder.Services.AddScoped<GlobalExceptionMiddleware>();

// Register services
builder.Services.AddHttpClient<IAuthService, AuthService>();
builder.Services.AddHttpClient(); // Add HttpClientFactory for UserServiceClient
builder.Services.AddSingleton<IUserServiceClient, UserServiceClient>();
builder.Services.AddSingleton<IGiftWishlistServiceClient, GiftWishlistServiceClient>();
// Email is handled in auth-service

// Register Chat Integration Service
builder.Services.AddHttpClient<IChatIntegrationService, ChatIntegrationService>();

// Register HttpClient for proxy requests to user-service
builder.Services.AddHttpClient("UserService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

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

// Configure CORS - Must be configured before authentication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",      // Web frontend (local dev)
            "http://localhost:3001",      // Web frontend (alt port)
            "http://localhost:8081",      // React Native Metro bundler
            "http://localhost:19000",     // Expo development
            "http://localhost:19006",     // Expo tunnel
            "http://127.0.0.1:3000",      // iOS simulator web
            "http://127.0.0.1:8081",      // iOS simulator
            "http://10.0.2.2:8081",       // Android emulator
            "https://wishera.vercel.app", // Production frontend
            "https://wishera.vercel.app/" // Production frontend (with trailing slash)
        )
        .AllowAnyMethod()                 // Allow all HTTP methods (GET, POST, PUT, DELETE, OPTIONS, etc.)
        .AllowAnyHeader()                 // Allow all headers (Authorization, Content-Type, etc.)
        .AllowCredentials()               // Allow credentials (cookies, authorization headers)
        .SetPreflightMaxAge(TimeSpan.FromSeconds(86400)); // Cache preflight for 24 hours
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CRITICAL: Middleware order matters for CORS!
// 1. Forwarded Headers - Must be first to detect HTTPS behind proxy
app.UseForwardedHeaders();

// 2. Routing - Must be before CORS to determine the endpoint
app.UseRouting();

// 3. CORS - Must be AFTER Routing but BEFORE Authentication/Authorization
// This allows CORS middleware to handle OPTIONS preflight requests
// OPTIONS requests don't have auth tokens, so CORS processes them first
app.UseCors("AllowAll");

// 4. Authentication & Authorization - After CORS
// Note: CORS middleware handles OPTIONS requests automatically
app.UseAuthentication();
app.UseAuthorization();

// 5. Global Exception Middleware - After Authentication/Authorization to catch all exceptions
// This ensures error responses (including auth errors) have CORS headers
app.UseMiddleware<GlobalExceptionMiddleware>();

// 5. Map endpoints
app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

// No local DB index management in gateway

app.Run();
