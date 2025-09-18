using BusinessLayer.Hubs;
using BusinessLayer.Services.FileServices.Implementations;
using BusinessLayer.Services.FileServices.Interfaces;
using BusinessLayer.Services.PrivateMessageServices.Implementations;
using BusinessLayer.Services.PrivateMessageServices.Interfaces;
using BusinessLayer.Services.UserService.Implementations;
using BusinessLayer.Services.UserService.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using PresentationLayer;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Messaging-only: no persistence or auth required for delivery



// No repositories required for pure in-memory messaging

// Configure MongoDB (used for message persistence if needed)
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var mongoUrl = Environment.GetEnvironmentVariable("MONGO_URL")
        ?? Environment.GetEnvironmentVariable("MONGODB_URI")
        ?? cfg["ChatMongo:ConnectionString"]
        ?? cfg.GetConnectionString("MongoDB");
    if (string.IsNullOrWhiteSpace(mongoUrl))
    {
        throw new InvalidOperationException("MongoDB connection string is not configured. Set ChatMongo:ConnectionString or MONGO_URL.");
    }
    return new MongoClient(mongoUrl);
});

builder.Services.AddHttpClient();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddTransient<GlobalExceptionHandlingMiddleware>();


builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(c =>
{
    c.AddDefaultPolicy(options =>
        options.WithOrigins(
            "http://localhost:3000",
            "http://localhost:3001",
            "http://127.0.0.1:3000",
            "http://127.0.0.1:3001")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials());
});


builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// Ensure Mongo collection and indexes exist at startup
builder.Services.AddHostedService<MongoSetupHostedService>();

var app = builder.Build();

app.UseRouting();
app.UseCors();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.MapHub<ChatHub>("/chat");

app.Run();
