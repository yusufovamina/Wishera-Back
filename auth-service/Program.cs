<<<<<<< HEAD
using MongoDB.Driver;
using WishlistApp.Services;
=======
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using WishlistApp.Services;
using auth_service.Services;
>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
<<<<<<< HEAD
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MongoDB
builder.Services.AddSingleton<IMongoClient>(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("MongoDb")
        ?? "mongodb://localhost:27017";
    return new MongoClient(connectionString);
});
builder.Services.AddSingleton(provider =>
{
    var client = provider.GetRequiredService<IMongoClient>();
    var dbName = builder.Configuration.GetValue<string>("MongoDb:Database") ?? "wishlistapp";
    return client.GetDatabase(dbName);
});
builder.Services.AddSingleton<MongoDbContext>();

// Auth + email services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

=======

// Mongo
var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB"));
var database = mongoClient.GetDatabase("WishlistApp");
builder.Services.AddSingleton(database);
builder.Services.AddSingleton<MongoDbContext>(sp => new MongoDbContext(database));

// Email, Cloudinary are not used directly here but keep parity if needed

// JWT (for token generation if needed by auth service)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

// Register core auth logic
builder.Services.AddSingleton<IAuthService, AuthService>();
builder.Services.AddSingleton<IEmailService, EmailService>();

// RabbitMQ RPC server
builder.Services.AddHostedService<AuthRpcServer>();

var app = builder.Build();

>>>>>>> 134c1c6a7281def98db2896a1d3c460cf432b684
app.MapControllers();

app.Run();
