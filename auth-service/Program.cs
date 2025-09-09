using MongoDB.Driver;
using auth_service.Services;

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

// Enable HTTPS redirection only outside Development (http profile doesn't define https url)
if (!app.Environment.IsDevelopment())
{
	app.UseHttpsRedirection();
}

// Apply CORS before auth and endpoints
app.UseCors(CorsPolicyName);

app.MapControllers();

app.Run();
