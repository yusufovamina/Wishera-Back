using MongoDB.Driver;
using WishlistApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
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

app.MapControllers();

app.Run();
