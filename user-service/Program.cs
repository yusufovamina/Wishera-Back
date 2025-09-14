using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;
using user_service.Services;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using WisheraApp.DTO;
using WisheraApp.Models;

var builder = WebApplication.CreateBuilder(args);

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
		policy.WithOrigins("http://localhost:3000")
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

// RabbitMQ RPC server
builder.Services.AddHostedService<UserRpcServer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
