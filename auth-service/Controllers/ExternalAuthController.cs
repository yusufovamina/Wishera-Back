using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using auth_service.Services;
using auth_service.Models;
using MongoDB.Driver;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace auth_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalAuthController : ControllerBase
    {
        private readonly MongoDbContext _db;
        private readonly IConfiguration _configuration;

        public ExternalAuthController(MongoDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        [HttpGet("login/{provider}")]
        public IActionResult Login(string provider)
        {
            var redirectUrl = Url.ActionLink(nameof(Callback), values: new { provider });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, provider);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string provider, [FromQuery] string? redirect)
        {
            var result = await HttpContext.AuthenticateAsync("External");
            if (!result.Succeeded || result.Principal is null)
            {
                return BadRequest("External authentication failed.");
            }

            var externalId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = result.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var name = result.Principal.Identity?.Name ?? email ?? "User";

            // Find or create local user
            var users = _db.Users;
            User? user = null;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var normalized = email.Trim().ToLowerInvariant();
                user = await users.Find(u => u.EmailNormalized == normalized).FirstOrDefaultAsync();
            }
            if (user == null)
            {
                user = new User
                {
                    Username = (name ?? email ?? $"user_{Guid.NewGuid():N}").Replace(" ", "").Trim(),
                    Email = email ?? string.Empty,
                    EmailNormalized = (email ?? string.Empty).Trim().ToLowerInvariant(),
                    UsernameNormalized = (name ?? email ?? Guid.NewGuid().ToString("N")).Trim().ToLowerInvariant(),
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    IsEmailVerified = !string.IsNullOrEmpty(email)
                };
                await users.InsertOneAsync(user);
            }
            else
            {
                var update = Builders<User>.Update.Set(u => u.LastActive, DateTime.UtcNow);
                await users.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            // Issue JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured"));
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var jwt = tokenHandler.WriteToken(token);

            await HttpContext.SignOutAsync("External");

            // Redirect to frontend with token or return JSON
            var baseUrl = redirect ?? _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000/oauth-complete";
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var _))
            {
                var sep = baseUrl.Contains('?') ? '&' : '?';
                var url = $"{baseUrl}{sep}token={Uri.EscapeDataString(jwt)}";
                return Redirect(url);
            }

            return Ok(new { provider, externalId, email, name, token = jwt });
        }
    }
}


