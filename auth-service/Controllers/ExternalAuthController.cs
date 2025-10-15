using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MongoDB.Driver;
using auth_service.Models;
using auth_service.Services;
using auth_service.DTO;
using Microsoft.IdentityModel.Tokens;

namespace auth_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private static readonly Dictionary<string, string> GoogleStateToCodeVerifier = new();

        public ExternalAuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("login/{provider}")]
        public IActionResult Login(string provider)
        {
            var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiOrigin = $"{Request.Scheme}://{Request.Host}";

            if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = cfg["Authentication:Google:ClientId"];
                if (string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_ACTUAL_GOOGLE_CLIENT_ID") 
                    return StatusCode(500, new { message = "Google OAuth not configured. Please set up Google OAuth credentials in appsettings.json" });

                var codeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .Replace("+", "-").Replace("/", "_").Replace("=", string.Empty);
                using var sha256 = SHA256.Create();
                var codeChallengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                var codeChallenge = Convert.ToBase64String(codeChallengeBytes).Replace("+", "-").Replace("/", "_").Replace("=", string.Empty);

                var state = $"google_{Guid.NewGuid():N}";
                lock (GoogleStateToCodeVerifier) { GoogleStateToCodeVerifier[state] = codeVerifier; }

                var backendCallback = new Uri(new Uri(apiOrigin), "/signin-google").ToString();
                var url = QueryHelpers.AddQueryString(
                    "https://accounts.google.com/o/oauth2/v2/auth",
                    new Dictionary<string, string?>
                    {
                        ["client_id"] = clientId,
                        ["redirect_uri"] = backendCallback,
                        ["response_type"] = "code",
                        ["scope"] = "openid email profile",
                        ["state"] = state,
                        ["code_challenge"] = codeChallenge,
                        ["code_challenge_method"] = "S256"
                    }
                );
                return Redirect(url);
            }
            else if (string.Equals(provider, "Twitter", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "Twitter authentication is currently disabled" });
            }

            return NotFound(new { message = "Provider not supported yet" });
        }

        [HttpGet("callback/{provider}")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, string provider)
        {
            string codeVerifier;
            lock (GoogleStateToCodeVerifier)
            {
                if (!GoogleStateToCodeVerifier.TryGetValue(state, out codeVerifier))
                {
                    return BadRequest(new { message = "Invalid state" });
                }
                GoogleStateToCodeVerifier.Remove(state);
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var frontendComplete = config["Frontend:BaseUrl"] ?? "http://localhost:3000/oauth-complete";
            var backendAuthority = $"{Request.Scheme}://{Request.Host}";
            var backendCallback = provider.Equals("Google", StringComparison.OrdinalIgnoreCase)
                ? new Uri(new Uri(backendAuthority), "/signin-google").ToString()
                : new Uri(new Uri(backendAuthority), "/signin-twitter").ToString();

            string email = string.Empty;
            string name = "user";

            if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = config["Authentication:Google:ClientId"];
                var clientSecret = config["Authentication:Google:ClientSecret"];
                using var http = new HttpClient();
                var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["client_id"] = clientId!,
                        ["client_secret"] = clientSecret!,
                        ["code"] = code,
                        ["grant_type"] = "authorization_code",
                        ["redirect_uri"] = backendCallback,
                        ["code_verifier"] = codeVerifier,
                    })
                };
                var tokenRes = await http.SendAsync(tokenReq);
                if (!tokenRes.IsSuccessStatusCode)
                {
                    return StatusCode((int)tokenRes.StatusCode, new { message = "Failed to exchange code" });
                }
                var tokenJson = await tokenRes.Content.ReadAsStringAsync();
                var tokenDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
                var idToken = tokenDoc.RootElement.GetProperty("id_token").GetString();

                // Validate id_token signature and claims using Google's OpenID configuration
                var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    "https://accounts.google.com/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever());
                var oidcConfig = await configurationManager.GetConfigurationAsync(CancellationToken.None);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = oidcConfig.SigningKeys,
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(idToken, tokenValidationParameters, out _);
                email = principal.FindFirst("email")?.Value ?? string.Empty;
                var emailVerified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
                name = principal.FindFirst("name")?.Value ?? (email.Split('@').FirstOrDefault() ?? "user");
            }
            else if (string.Equals(provider, "Twitter", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "Twitter authentication is currently disabled" });
            }

            // Upsert or get user
            var dbContext = HttpContext.RequestServices.GetRequiredService<MongoDbContext>();
            var normalizedEmail = email.Trim().ToLowerInvariant();
            var user = await dbContext.Users.Find(u => u.EmailNormalized == normalizedEmail).FirstOrDefaultAsync();
            if (user == null)
            {
                user = new auth_service.Models.User
                {
                    Username = name,
                    Email = email,
                    EmailNormalized = normalizedEmail,
                    UsernameNormalized = name.Trim().ToLowerInvariant(),
                    PasswordHash = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    IsEmailVerified = true
                };
                await dbContext.Users.InsertOneAsync(user);
            }
            else
            {
                var update = MongoDB.Driver.Builders<auth_service.Models.User>.Update.Set(u => u.LastActive, DateTime.UtcNow);
                await dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            // Issue our JWT: generate manually for social sign-ins
            AuthResponseDTO authResponse;
            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                var configJwtKey = config["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured");
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(configJwtKey);
                var descriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Email, user.Email)
                    }),
                    Expires = DateTime.UtcNow.AddDays(7),
                    Issuer = config["Jwt:Issuer"],
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };
                var token = tokenHandler.CreateToken(descriptor);
                authResponse = new AuthResponseDTO
                {
                    UserId = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    Token = tokenHandler.WriteToken(token),
                    ExpiresAt = descriptor.Expires ?? DateTime.UtcNow.AddDays(7)
                };
            }
            else
            {
                authResponse = await _authService.LoginAsync(new LoginDTO { Email = user.Email, Password = user.PasswordHash });
            }

            var redirectUrl = QueryHelpers.AddQueryString(frontendComplete, new Dictionary<string, string?>
            {
                ["token"] = authResponse.Token,
                ["userId"] = authResponse.UserId,
                ["username"] = authResponse.Username,
            });
            return Redirect(redirectUrl);
        }

        // Alias for Google callback path expected by Google OAuth
        [HttpGet("/signin-google")]
        public Task<IActionResult> GoogleSigninAlias([FromQuery] string code, [FromQuery] string state)
            => Callback(code, state, "Google");
    }
}
