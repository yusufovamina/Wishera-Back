using MongoDB.Driver;
using auth_service.Services;
using auth_service.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com
var port = Environment.GetEnvironmentVariable("PORT") ?? "5219";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

// CORS
const string CorsPolicyName = "DevCors";
builder.Services.AddCors(options =>
{
	options.AddPolicy(CorsPolicyName, policy =>
	{
		policy.WithOrigins(
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
		.AllowAnyHeader()
		.AllowAnyMethod()
		.AllowCredentials();
	});
});

// MongoDB - Create client but don't fail if connection is temporarily unavailable
builder.Services.AddSingleton<IMongoClient>(_ =>
{
	try
	{
		// Use the same key casing as other services if present
		var connectionString = builder.Configuration.GetConnectionString("MongoDB")
			?? builder.Configuration.GetConnectionString("MongoDb")
			?? "mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster";
		Console.WriteLine($"[MongoDB] Initializing MongoDB client with connection string: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
		return new MongoClient(connectionString);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[MongoDB] Error creating MongoDB client: {ex.Message}");
		throw; // MongoDB is required, so fail if we can't create the client
	}
});
builder.Services.AddSingleton(provider =>
{
	try
	{
		var client = provider.GetRequiredService<IMongoClient>();
		var dbName = builder.Configuration.GetValue<string>("MongoDB:Database")
			?? builder.Configuration.GetValue<string>("MongoDb:Database")
			?? "WishlistApp";
		Console.WriteLine($"[MongoDB] Using database: {dbName}");
		return client.GetDatabase(dbName);
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[MongoDB] Error getting database: {ex.Message}");
		throw;
	}
});
builder.Services.AddSingleton<MongoDbContext>();

// Auth + email services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

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
        var jwtKey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            var errorMessage = "JWT key is not configured. Please set the Jwt__Key environment variable or configure it in appsettings.json";
            Console.WriteLine($"[JWT] ERROR: {errorMessage}");
            throw new InvalidOperationException(errorMessage);
        }
        
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "WisheraApp";
        Console.WriteLine($"[JWT] JWT authentication configured with issuer: {jwtIssuer}");
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
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

// Register exception middleware
builder.Services.AddSingleton<GlobalExceptionMiddleware>();

var app = builder.Build();

// Use forwarded headers middleware BEFORE other middleware
// This allows the app to correctly detect HTTPS when behind a proxy (like Render.com)
app.UseForwardedHeaders();

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

app.UseRouting();

// Apply CORS before auth and endpoints
app.UseCors(CorsPolicyName);

// Use exception middleware to handle errors gracefully (after CORS, before auth)
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

try
{
    Console.WriteLine($"Auth Service starting on port {port}");
    Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error starting auth service: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    throw;
}
