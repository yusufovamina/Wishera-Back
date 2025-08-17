using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using System.Text;
using WishlistApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure MongoDB
var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB"));
var database = mongoClient.GetDatabase("WishlistApp");
builder.Services.AddSingleton(database);

// Register MongoDbContext
builder.Services.AddSingleton<MongoDbContext>(sp => new MongoDbContext(database));

// Configure Cloudinary
var cloudinaryAccount = new Account(
    builder.Configuration["Cloudinary:CloudName"],
    builder.Configuration["Cloudinary:ApiKey"],
    builder.Configuration["Cloudinary:ApiSecret"]
);
var cloudinary = new Cloudinary(cloudinaryAccount);
builder.Services.AddSingleton(cloudinary);

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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<IEmailService, EmailService>();

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
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
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

// Create indexes for MongoDB collections
var usersCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Users");
var wishlistsCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Wishlists");
var likesCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Likes");
var commentsCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Comments");
var feedCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Feed");
var relationshipsCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("Relationships");

// Create indexes for Users collection
var usersIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("Email"),
        new CreateIndexOptions { 
            Unique = true,
            Sparse = true  // This ensures the unique index only applies to documents where Email exists
        }
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("Username"),
        new CreateIndexOptions { 
            Unique = true,
            Sparse = true  // This ensures the unique index only applies to documents where Username exists
        }
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Text("Username")
    )
};

// Create indexes for Wishlists collection
var wishlistsIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("UserId")
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("IsPublic")
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Text("Title").Text("Description")
    )
};

// Create indexes for Likes collection
var likesIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Combine(
            Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("UserId"),
            Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("WishlistId")
        ),
        new CreateIndexOptions { Unique = true }
    )
};

// Create indexes for Comments collection
var commentsIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("WishlistId")
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("UserId")
    )
};

// Create indexes for Feed collection
var feedIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("UserId")
    ),
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Descending("CreatedAt")
    )
};

// Create indexes for Relationships collection
var relationshipsIndexes = new[]
{
    new CreateIndexModel<MongoDB.Bson.BsonDocument>(
        Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Combine(
            Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("FollowerId"),
            Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("FollowingId")
        ),
        new CreateIndexOptions { Unique = true }
    )
};

// Create all indexes
await Task.WhenAll(
    usersCollection.Indexes.CreateManyAsync(usersIndexes),
    wishlistsCollection.Indexes.CreateManyAsync(wishlistsIndexes),
    likesCollection.Indexes.CreateManyAsync(likesIndexes),
    commentsCollection.Indexes.CreateManyAsync(commentsIndexes),
    feedCollection.Indexes.CreateManyAsync(feedIndexes),
    relationshipsCollection.Indexes.CreateManyAsync(relationshipsIndexes)
);

app.Run();
