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
using MongoDB.Bson;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using PresentationLayer;
using Swashbuckle.AspNetCore.Filters;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure port for Render.com - Render provides PORT environment variable
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    // Use + to bind to all interfaces (both IPv4 and IPv6)
    builder.WebHost.UseUrls($"http://+:{port}");
}

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
        Console.WriteLine("WARNING: MongoDB connection string is not configured. Set ChatMongo:ConnectionString or MONGO_URL.");
        // Return a dummy client to prevent startup failure - service can still run for SignalR
        return new MongoClient("mongodb://localhost:27017");
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
            "http://localhost:3000",      // Web frontend
            "http://localhost:3001",      // Web frontend alt
            "http://localhost:8081",      // React Native Metro bundler
            "http://localhost:19000",     // Expo development
            "http://localhost:19006",     // Expo tunnel
            "http://127.0.0.1:3000",      // iOS simulator web
            "http://127.0.0.1:3001",      // iOS simulator web alt
            "http://127.0.0.1:8081",      // iOS simulator
            "http://10.0.2.2:8081",       // Android emulator
            "https://wishera.vercel.app", // Production frontend
            "https://wishera.vercel.app/" // Production frontend (with trailing slash)
        )
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

// Serve static wallpapers from wwwroot
app.UseStaticFiles();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

app.MapHub<ChatHub>("/chat");

// Minimal history API backed by Mongo used by ChatHub
app.MapGet("/api/chat/history", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userA,
    [FromQuery] string userB,
    [FromQuery] int page,
    [FromQuery] int pageSize) =>
{
    if (string.IsNullOrWhiteSpace(userA) || string.IsNullOrWhiteSpace(userB))
    {
        return Results.BadRequest(new { message = "userA and userB are required" });
    }
    var a = userA;
    var b = userB;
    var conversationId = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);

    var filter = Builders<BsonDocument>.Filter.Eq("conversationId", conversationId);
    var cursor = await collection.Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Ascending("sentAt"))
        .Skip(Math.Max(0, page) * Math.Max(1, pageSize))
        .Limit(Math.Max(1, pageSize))
        .ToListAsync();

    var items = cursor.Select(d =>
    {
        var sentVal = d.GetValue("sentAt", BsonNull.Value);
        DateTimeOffset sentAtValue;
        if (sentVal is BsonDateTime bdt)
        {
            sentAtValue = bdt.ToUniversalTime();
        }
        else if (sentVal.IsString && DateTimeOffset.TryParse(sentVal.AsString, out var parsed))
        {
            sentAtValue = parsed.ToUniversalTime();
        }
        else
        {
            sentAtValue = DateTimeOffset.MinValue;
        }

        // Extract reactions as emoji -> [userIds]
        Dictionary<string, string[]> reactions = new();
        var reactionsVal = d.GetValue("reactions", BsonNull.Value);
        if (reactionsVal is BsonDocument reactionsDoc)
        {
            foreach (var emojiEl in reactionsDoc.Elements)
            {
                var emoji = emojiEl.Name;
                if (emojiEl.Value is BsonDocument usersDoc)
                {
                    var users = usersDoc.Elements
                        .Where(e => e.Value.IsBoolean && e.Value.AsBoolean)
                        .Select(e => e.Name)
                        .ToArray();
                    reactions[emoji] = users;
                }
            }
        }
        else if (reactionsVal is BsonArray reactionsArr)
        {
            // Legacy shape: [{ userId, emoji }]
            foreach (var el in reactionsArr)
            {
                if (el is BsonDocument rd)
                {
                    var emoji = rd.GetValue("emoji", BsonNull.Value).IsBsonNull ? null : rd["emoji"].AsString;
                    var userId = rd.GetValue("userId", BsonNull.Value).IsBsonNull ? null : rd["userId"].AsString;
                    if (!string.IsNullOrEmpty(emoji) && !string.IsNullOrEmpty(userId))
                    {
                        if (!reactions.ContainsKey(emoji)) reactions[emoji] = Array.Empty<string>();
                        var list = reactions[emoji].ToList();
                        if (!list.Contains(userId)) list.Add(userId);
                        reactions[emoji] = list.ToArray();
                    }
                }
            }
        }

        return new
        {
            id = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
            conversationId = d.GetValue("conversationId", BsonNull.Value).IsBsonNull ? string.Empty : d["conversationId"].AsString,
            senderUserId = d.GetValue("senderUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["senderUserId"].AsString,
            recipientUserId = d.GetValue("recipientUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["recipientUserId"].AsString,
            text = d.GetValue("text", BsonNull.Value).IsBsonNull ? string.Empty : d["text"].AsString,
            sentAt = sentAtValue,
            reactions = reactions
        };
    });

    return Results.Ok(items);
});

// Edit a message text
app.MapPost("/api/chat/message/edit", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] EditRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body?.MessageId) || string.IsNullOrWhiteSpace(body?.NewText))
    {
        return Results.BadRequest(new { message = "messageId and newText are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);
    var filter = Builders<BsonDocument>.Filter.Eq("messageId", body.MessageId);
    var update = Builders<BsonDocument>.Update.Set("text", body.NewText);
    var result = await collection.UpdateOneAsync(filter, update);
    return result.ModifiedCount > 0 ? Results.Ok(new { updated = true }) : Results.NotFound(new { updated = false });
});

// Delete a message
app.MapPost("/api/chat/message/delete", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] DeleteRequest body) =>
{
    if (string.IsNullOrWhiteSpace(body?.MessageId))
    {
        return Results.BadRequest(new { message = "messageId is required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var collectionName = configuration["ChatMongo:Collection"] ?? "messages";
    var db = mongoClient.GetDatabase(dbName);
    var collection = db.GetCollection<BsonDocument>(collectionName);
    var filter = Builders<BsonDocument>.Filter.Eq("messageId", body.MessageId);
    var result = await collection.DeleteOneAsync(filter);
    return result.DeletedCount > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound(new { deleted = false });
});

var defaultWallpapers = new List<WallpaperCatalogItem>
{
    new("abstract-aurora","Abstract Aurora","Flowing teal/purple gradients; great for dark mode.","abstract",true,true,"/wallpapers/abstract-aurora.svg"),
    new("minimal-mist","Minimal Mist","Soft off‑white with subtle grain; ideal for light mode.","minimal",false,true,"/wallpapers/minimal-mist.svg"),
    new("midnight-grid","Midnight Grid","Faint geometric grid over deep navy.","geometric",true,false,"/wallpapers/midnight-grid.svg"),
    new("desert-dunes","Desert Dunes","Warm sandy waves; cozy neutral.","nature",true,true,"/wallpapers/desert-dunes.svg"),
    new("neon-shapes","Neon Shapes","Playful geometric shapes with neon accents.","geometric",true,true,"/wallpapers/neon-shapes.svg"),
    new("forest-blur","Forest Blur","Defocused green woodland bokeh; calming.","nature",true,true,"/wallpapers/forest-blur.svg"),
    new("carbon-fiber","Carbon Fiber","Textured dark diagonal weave; industrial.","abstract",true,false,"/wallpapers/carbon-fiber.svg"),
    new("pastel-fade","Pastel Fade","Pastel rainbow gradient; cheerful.","abstract",false,true,"/wallpapers/pastel-fade.svg"),
    new("ocean-lowpoly","Ocean Low‑Poly","Polygonal sea tones; crisp depth.","geometric",true,true,"/wallpapers/ocean-lowpoly.svg"),
    new("paper-texture","Paper Texture","Light tactile paper; classic readability.","minimal",false,true,"/wallpapers/paper-texture.svg")
};

// List all wallpapers (default + custom)
app.MapGet("/api/chat/wallpapers", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string? userId) =>
{
    var allWallpapers = new List<WallpaperCatalogItem>(defaultWallpapers);
    
    // Add custom wallpapers if userId is provided
    if (!string.IsNullOrWhiteSpace(userId))
    {
        var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
        var db = mongoClient.GetDatabase(dbName);
        var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

        var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
        var customWallpapers = await wallpapers.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("uploadedAt"))
            .ToListAsync();

        var customWallpaperItems = customWallpapers.Select(doc => new WallpaperCatalogItem(
            doc["id"].AsString,
            doc["name"].AsString,
            doc["description"].AsString,
            doc["category"].AsString,
            doc["supportsDark"].AsBoolean,
            doc["supportsLight"].AsBoolean,
            doc["previewUrl"].AsString
        )).ToList();

        allWallpapers.AddRange(customWallpaperItems);
    }

    return Results.Ok(allWallpapers);
});

// Preferences storage in Mongo: collection chat_wallpaper_prefs
app.MapGet("/api/chat/preferences/wallpaper", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string me,
    [FromQuery] string peer) =>
{
    if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(peer))
    {
        return Results.BadRequest(new { message = "me and peer are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var prefs = db.GetCollection<BsonDocument>("chat_wallpaper_prefs");
    var key = string.CompareOrdinal(me, peer) < 0 ? $"{me}:{peer}" : $"{peer}:{me}";
    var doc = await prefs.Find(Builders<BsonDocument>.Filter.Eq("key", key)).FirstOrDefaultAsync();
    if (doc == null)
    {
        return Results.Ok(new { wallpaperId = (string?)null, opacity = 0.25, wallpaperUrl = (string?)null });
    }
    var wid = doc.GetValue("wallpaperId", BsonNull.Value).IsBsonNull ? null : doc["wallpaperId"].AsString;
    var opacity = doc.GetValue("opacity", 0.25).ToDouble();
    var wallpaperUrl = doc.GetValue("wallpaperUrl", BsonNull.Value).IsBsonNull ? null : doc["wallpaperUrl"].AsString;
    
    // If no URL is stored but we have a wallpaperId, try to resolve it
    if (string.IsNullOrEmpty(wallpaperUrl) && !string.IsNullOrEmpty(wid))
    {
        // Check if it's a custom wallpaper
        if (wid.StartsWith("custom_"))
        {
            var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");
            var wallpaperDoc = await wallpapers.Find(Builders<BsonDocument>.Filter.Eq("id", wid)).FirstOrDefaultAsync();
            if (wallpaperDoc != null)
            {
                wallpaperUrl = wallpaperDoc["previewUrl"].AsString;
            }
        }
        else
        {
            // It's a default wallpaper, construct the URL
            var defaultWallpaper = defaultWallpapers.FirstOrDefault(w => w.id == wid);
            if (defaultWallpaper != null)
            {
                wallpaperUrl = defaultWallpaper.previewUrl;
            }
        }
    }
    
    return Results.Ok(new { wallpaperId = wid, opacity, wallpaperUrl });
});

app.MapPost("/api/chat/preferences/wallpaper", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] SaveWallpaperPref body) =>
{
    if (string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer))
    {
        return Results.BadRequest(new { saved = false, message = "me and peer are required" });
    }
    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var prefs = db.GetCollection<BsonDocument>("chat_wallpaper_prefs");
    var key = string.CompareOrdinal(body.me, body.peer) < 0 ? $"{body.me}:{body.peer}" : $"{body.peer}:{body.me}";

    if (body.wallpaperId == null)
    {
        await prefs.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("key", key));
        return Results.Ok(new { saved = true });
    }

    // Resolve wallpaper URL if not provided
    var wallpaperUrl = body.wallpaperUrl;
    if (string.IsNullOrEmpty(wallpaperUrl))
    {
        if (body.wallpaperId.StartsWith("custom_"))
        {
            var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");
            var wallpaperDoc = await wallpapers.Find(Builders<BsonDocument>.Filter.Eq("id", body.wallpaperId)).FirstOrDefaultAsync();
            if (wallpaperDoc != null)
            {
                wallpaperUrl = wallpaperDoc["previewUrl"].AsString;
            }
        }
        else
        {
            var defaultWallpaper = defaultWallpapers.FirstOrDefault(w => w.id == body.wallpaperId);
            if (defaultWallpaper != null)
            {
                wallpaperUrl = defaultWallpaper.previewUrl;
            }
        }
    }

    var update = Builders<BsonDocument>.Update
        .Set("key", key)
        .Set("wallpaperId", body.wallpaperId)
        .Set("opacity", Math.Clamp(body.opacity ?? 0.25, 0, 1))
        .Set("wallpaperUrl", wallpaperUrl ?? "");
    await prefs.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Update.Combine(update),
        new UpdateOptions { IsUpsert = true });
    return Results.Ok(new { saved = true });
});

// Media upload endpoint → Cloudinary
app.MapPost("/api/chat/upload-media", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Form content required" });
    }
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file provided" });
    }
    var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
    var isImage = contentType.StartsWith("image/");
    var isVideo = contentType.StartsWith("video/");
    var isAudio = contentType.StartsWith("audio/");
    if (!isImage && !isVideo && !isAudio)
    {
        return Results.BadRequest(new { message = "Only images, videos, or audio files are allowed" });
    }
    try
    {
        // Prefer images via Cloudinary image API; videos and audio via Cloudinary video API
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        // Fallback: support connection URL in appsettings: Cloudinary:Url = cloudinary://<apiKey>:<apiSecret>@<cloudName>
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            return Results.Problem("Cloudinary is not configured", statusCode: 500);
        }
        var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);
        if (isImage)
        {
            var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream()),
                Transformation = new CloudinaryDotNet.Transformation().Width(1600).Crop("limit").Quality(80)
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "image" });
        }
        else if (isVideo)
        {
            var uploadParams = new CloudinaryDotNet.Actions.VideoUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream())
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "video" });
        }
        else // isAudio - Cloudinary handles audio files via VideoUploadParams
        {
            var uploadParams = new CloudinaryDotNet.Actions.VideoUploadParams
            {
                File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream())
            };
            var res = await cloudinary.UploadAsync(uploadParams);
            if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);
            return Results.Ok(new { url = res.SecureUrl.ToString(), mediaType = "audio" });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// === Custom Wallpaper Upload and Management ===
// Upload custom wallpaper to Cloudinary
app.MapPost("/api/chat/upload-wallpaper", async (
    HttpRequest request,
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Form content required" });
    }
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    var userId = form["userId"].FirstOrDefault();
    var name = form["name"].FirstOrDefault();
    var description = form["description"].FirstOrDefault();
    var category = form["category"].FirstOrDefault();
    var supportsDark = bool.TryParse(form["supportsDark"].FirstOrDefault(), out var dark) && dark;
    var supportsLight = bool.TryParse(form["supportsLight"].FirstOrDefault(), out var light) && light;

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "No file provided" });
    }
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required" });
    }

    var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
    if (!contentType.StartsWith("image/"))
    {
        return Results.BadRequest(new { message = "Only images are allowed for wallpapers" });
    }

    // Validate file size (max 10MB)
    if (file.Length > 10 * 1024 * 1024)
    {
        return Results.BadRequest(new { message = "File size must be less than 10MB" });
    }

    try
    {
        // Upload to Cloudinary
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            return Results.Problem("Cloudinary is not configured", statusCode: 500);
        }

        var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
        var cloudinary = new CloudinaryDotNet.Cloudinary(account);

        var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
        {
            File = new CloudinaryDotNet.FileDescription(file.FileName, file.OpenReadStream()),
            PublicId = $"wallpapers/custom/{userId}_{Guid.NewGuid()}",
            Transformation = new CloudinaryDotNet.Transformation().Width(1920).Height(1080).Crop("fill").Quality(85),
            Folder = "wallpapers/custom"
        };

        var res = await cloudinary.UploadAsync(uploadParams);
        if (res.Error != null) return Results.Problem(res.Error.Message, statusCode: 500);

        // Save wallpaper metadata to MongoDB
        var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
        var db = mongoClient.GetDatabase(dbName);
        var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

        var wallpaperId = $"custom_{Guid.NewGuid()}";
        var wallpaperDoc = new BsonDocument
        {
            { "id", wallpaperId },
            { "name", name ?? "Custom Wallpaper" },
            { "description", description ?? "User uploaded wallpaper" },
            { "category", category ?? "custom" },
            { "supportsDark", supportsDark },
            { "supportsLight", supportsLight },
            { "previewUrl", res.SecureUrl.ToString() },
            { "cloudinaryPublicId", res.PublicId },
            { "userId", userId },
            { "uploadedAt", DateTime.UtcNow },
            { "isCustom", true }
        };

        await wallpapers.InsertOneAsync(wallpaperDoc);

        return Results.Ok(new { 
            wallpaperId, 
            url = res.SecureUrl.ToString(),
            name = name ?? "Custom Wallpaper",
            description = description ?? "User uploaded wallpaper",
            category = category ?? "custom",
            supportsDark,
            supportsLight
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// Get custom wallpapers for a user
app.MapGet("/api/chat/custom-wallpapers", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userId) =>
{
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "userId is required" });
    }

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

    var filter = Builders<BsonDocument>.Filter.Eq("userId", userId);
    var customWallpapers = await wallpapers.Find(filter)
        .Sort(Builders<BsonDocument>.Sort.Descending("uploadedAt"))
        .ToListAsync();

    var result = customWallpapers.Select(doc => new WallpaperCatalogItem(
        doc["id"].AsString,
        doc["name"].AsString,
        doc["description"].AsString,
        doc["category"].AsString,
        doc["supportsDark"].AsBoolean,
        doc["supportsLight"].AsBoolean,
        doc["previewUrl"].AsString
    )).ToList();

    return Results.Ok(result);
});

// Delete custom wallpaper
app.MapDelete("/api/chat/custom-wallpapers/{wallpaperId}", async (
    string wallpaperId,
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string userId) =>
{
    if (string.IsNullOrWhiteSpace(wallpaperId) || string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest(new { message = "wallpaperId and userId are required" });
    }

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var wallpapers = db.GetCollection<BsonDocument>("custom_wallpapers");

    // Find the wallpaper and verify ownership
    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("id", wallpaperId),
        Builders<BsonDocument>.Filter.Eq("userId", userId)
    );

    var wallpaper = await wallpapers.Find(filter).FirstOrDefaultAsync();
    if (wallpaper == null)
    {
        return Results.NotFound(new { message = "Wallpaper not found or access denied" });
    }

    try
    {
        // Delete from Cloudinary
        var cloudName = app.Configuration["Cloudinary:CloudName"];
        var apiKey = app.Configuration["Cloudinary:ApiKey"];
        var apiSecret = app.Configuration["Cloudinary:ApiSecret"];
        
        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            var url = app.Configuration["Cloudinary:Url"] ?? string.Empty;
            if (url.StartsWith("cloudinary://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var withoutScheme = url.Substring("cloudinary://".Length);
                    var atIndex = withoutScheme.IndexOf('@');
                    var colonIndex = withoutScheme.IndexOf(':');
                    if (atIndex > 0 && colonIndex > 0 && colonIndex < atIndex)
                    {
                        apiKey = withoutScheme.Substring(0, colonIndex);
                        apiSecret = withoutScheme.Substring(colonIndex + 1, atIndex - colonIndex - 1);
                        cloudName = withoutScheme.Substring(atIndex + 1);
                    }
                }
                catch { }
            }
        }

        if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            var account = new CloudinaryDotNet.Account(cloudName, apiKey, apiSecret);
            var cloudinary = new CloudinaryDotNet.Cloudinary(account);

            var deletionParams = new CloudinaryDotNet.Actions.DeletionParams(wallpaper["cloudinaryPublicId"].AsString);
            await cloudinary.DestroyAsync(deletionParams);
        }

        // Delete from MongoDB
        await wallpapers.DeleteOneAsync(filter);

        return Results.Ok(new { deleted = true });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
});

// === Chat pins persistence (offline-safe) ===
app.MapGet("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromQuery] string me,
    [FromQuery] string peer) =>
{
    if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(peer))
    {
        return Results.BadRequest(new { message = "me and peer are required" });
    }
    var a = me;
    var b = peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var cursor = await pins.Find(Builders<BsonDocument>.Filter.Eq("key", key))
        .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
        .ToListAsync();

    var items = cursor.Select((d, idx) =>
    {
        var createdAtVal = d.GetValue("createdAt", BsonNull.Value);
        DateTimeOffset createdAtValue;
        if (createdAtVal is BsonDateTime bdt)
        {
            createdAtValue = bdt.ToUniversalTime();
        }
        else if (createdAtVal.IsString && DateTimeOffset.TryParse(createdAtVal.AsString, out var parsed))
        {
            createdAtValue = parsed.ToUniversalTime();
        }
        else
        {
            createdAtValue = DateTimeOffset.UtcNow;
        }

        return new
        {
            id = d.GetValue("_id", BsonNull.Value).IsBsonNull ? $"local-{idx}" : d["_id"].ToString(),
            conversationId = key,
            messageId = d.GetValue("messageId", BsonNull.Value).IsBsonNull ? string.Empty : d["messageId"].AsString,
            scope = d.GetValue("scope", "global").AsString,
            pinnedByUserId = d.GetValue("pinnedByUserId", BsonNull.Value).IsBsonNull ? string.Empty : d["pinnedByUserId"].AsString,
            createdAt = createdAtValue
        };
    });

    return Results.Ok(items);
});

app.MapPost("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] PinRequest body) =>
{
    if (body == null || string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer) || string.IsNullOrWhiteSpace(body.messageId))
    {
        return Results.BadRequest(new { pinned = false, message = "me, peer, and messageId are required" });
    }
    var a = body.me;
    var b = body.peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Filter.Eq("messageId", body.messageId)
    );
    var update = Builders<BsonDocument>.Update
        .SetOnInsert("key", key)
        .SetOnInsert("messageId", body.messageId)
        .Set("scope", string.IsNullOrWhiteSpace(body.scope) ? "global" : body.scope)
        .Set("pinnedByUserId", body.me)
        .SetOnInsert("createdAt", DateTimeOffset.UtcNow);
    var options = new UpdateOptions { IsUpsert = true };
    await pins.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Combine(update), options);

    return Results.Ok(new { pinned = true, id = body.messageId });
});

app.MapDelete("/api/chat/pins", async (
    [FromServices] IMongoClient mongoClient,
    [FromServices] IConfiguration configuration,
    [FromBody] PinRequest body) =>
{
    if (body == null || string.IsNullOrWhiteSpace(body.me) || string.IsNullOrWhiteSpace(body.peer) || string.IsNullOrWhiteSpace(body.messageId))
    {
        return Results.BadRequest(new { unpinned = false, message = "me, peer, and messageId are required" });
    }
    var a = body.me;
    var b = body.peer;
    var key = string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";

    var dbName = configuration["ChatMongo:Database"] ?? "wishlist_chat";
    var db = mongoClient.GetDatabase(dbName);
    var pins = db.GetCollection<BsonDocument>("chat_pins");

    var filter = Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("key", key),
        Builders<BsonDocument>.Filter.Eq("messageId", body.messageId)
    );
    await pins.DeleteOneAsync(filter);

    return Results.Ok(new { unpinned = true });
});
app.Run();

// ===== Types (must follow top-level statements) =====
public record WallpaperCatalogItem(
    string id,
    string name,
    string description,
    string category,
    bool supportsDark,
    bool supportsLight,
    string previewUrl
);
public record SaveWallpaperPref(string me, string peer, string? wallpaperId, double? opacity, string? wallpaperUrl = null);
public record EditRequest(string MessageId, string NewText);
public record DeleteRequest(string MessageId);
public record PinRequest(string me, string peer, string messageId, string scope);
