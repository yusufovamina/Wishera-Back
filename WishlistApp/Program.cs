using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using WisheraApp.Services;

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
    });

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

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.WithOrigins(
            "http://localhost:3000",      // Web frontend
            "http://localhost:3001",      // Web frontend alt
            "http://localhost:8081",      // React Native Metro bundler
            "http://localhost:19000",     // Expo development
            "http://localhost:19006",     // Expo web default
            "http://localhost:19001",     // Expo web alt
            "http://127.0.0.1:3000",      // iOS simulator web
            "http://127.0.0.1:3001",      // iOS simulator web alt
            "http://127.0.0.1:8081",      // iOS simulator
            "http://127.0.0.1:19006",     // iOS simulator Expo web
            "http://10.0.2.2:8081",       // Android emulator
            "http://10.0.2.2:19006",      // Android emulator Expo web
            "https://wishera.vercel.app"  // Production frontend
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
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

// No local DB index management in gateway

app.Run();
