using MongoDB.Driver;
using auth_service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using auth_service.Middleware;
using auth_service.Filters;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com - Render provides PORT environment variable
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    // Use + to bind to all interfaces (both IPv4 and IPv6)
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddControllers(options =>
{
    // Add CORS result filter to ensure headers are always present
    options.Filters.Add<CorsResultFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure request timeout (30 seconds)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
});

// CORS
const string CorsPolicyName = "DevCors";
builder.Services.AddCors(options =>
{
	options.AddPolicy(CorsPolicyName, policy =>
	{
		policy.WithOrigins(
			"http://localhost:3000",      // Web frontend
			"http://localhost:8081",      // React Native Metro bundler
			"http://localhost:19000",     // Expo development
			"http://localhost:19006",     // Expo tunnel
			"http://127.0.0.1:8081",       // iOS simulator
			"http://10.0.2.2:8081",       // Android emulator
			"https://wishera.vercel.app", // Production frontend
			"https://wishera.vercel.app/" // Production frontend (with trailing slash)
		)
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowCredentials();
	});
});

// MongoDB with timeout configuration
builder.Services.AddSingleton<IMongoClient>(_ =>
{
	// Use the same key casing as other services if present
	var connectionString = builder.Configuration.GetConnectionString("MongoDB")
		?? builder.Configuration.GetConnectionString("MongoDb")
		?? "mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster";
	
	// Configure MongoDB client settings with timeouts
	var settings = MongoClientSettings.FromConnectionString(connectionString);
	settings.ConnectTimeout = TimeSpan.FromSeconds(5); // 5 seconds to establish connection
	settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5); // 5 seconds to select server
	settings.SocketTimeout = TimeSpan.FromSeconds(10); // 10 seconds for socket operations
	settings.MaxConnectionPoolSize = 100;
	settings.MinConnectionPoolSize = 10;
	
	return new MongoClient(settings);
});
builder.Services.AddSingleton(provider =>
{
	var client = provider.GetRequiredService<IMongoClient>();
	var dbName = builder.Configuration.GetValue<string>("MongoDB:Database")
		?? builder.Configuration.GetValue<string>("MongoDb:Database")
		?? "WishlistApp";
	return client.GetDatabase(dbName);
});
builder.Services.AddSingleton<MongoDbContext>();

// Auth + email services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Register global exception handling middleware
builder.Services.AddTransient<GlobalExceptionMiddleware>();

// Authentication (JWT for API + Cookie for external flow) and external providers
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        // Use named cookie scheme for external sign-in
        options.DefaultSignInScheme = "External";
    })
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
    })
    .AddCookie("External", options =>
    {
        options.Cookie.Name = ".Wishera.External";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    });
// RabbitMQ RPC server for Auth
builder.Services.AddHostedService<AuthRpcServer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

// Enable HTTPS redirection only outside Development (http profile doesn't define https url)
if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

// Apply CORS before auth and endpoints
app.UseRouting();
app.UseCors(CorsPolicyName);

// Add global exception handling middleware (must be after CORS but before controllers)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Add authentication and authorization middleware (required for protected endpoints)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();
