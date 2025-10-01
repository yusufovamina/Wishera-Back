using MongoDB.Driver;
using auth_service.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
const string CorsPolicyName = "DevCors";
builder.Services.AddCors(options =>
{
	options.AddPolicy(CorsPolicyName, policy =>
	{
		policy.WithOrigins("http://localhost:3000")
			.AllowAnyHeader()
			.AllowAnyMethod()
			.AllowCredentials();
	});
});

// MongoDB
builder.Services.AddSingleton<IMongoClient>(_ =>
{
	// Use the same key casing as other services if present
	var connectionString = builder.Configuration.GetConnectionString("MongoDB")
		?? builder.Configuration.GetConnectionString("MongoDb")
		?? "mongodb+srv://yusufovamina:Fh9nz7EKJuPZHViL@cluster.9qjuc.mongodb.net/?retryWrites=true&w=majority&appName=Cluster";
	return new MongoClient(connectionString);
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
    })
    .AddCookie("External", options =>
    {
        options.Cookie.Name = ".Wishera.External";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    })
    .AddGoogle(googleOptions =>
    {
        googleOptions.SignInScheme = "External";
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        googleOptions.CallbackPath = "/signin-google";
        googleOptions.Scope.Add("profile");
        googleOptions.Scope.Add("email");
        googleOptions.SaveTokens = true;
    })
    .AddTwitter(twitterOptions =>
    {
        // OAuth 1.0a keys
        twitterOptions.SignInScheme = "External";
        twitterOptions.ConsumerKey = builder.Configuration["Authentication:Twitter:ConsumerKey"]!;
        twitterOptions.ConsumerSecret = builder.Configuration["Authentication:Twitter:ConsumerSecret"]!;
        twitterOptions.CallbackPath = "/signin-twitter";
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
app.UseCors(CorsPolicyName);
app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Lax,
    Secure = CookieSecurePolicy.None
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
