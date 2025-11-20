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
        private static readonly Dictionary<string, string> GoogleStateToOrigin = new();

        public ExternalAuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet("login/{provider}")]
        public IActionResult Login(string provider, [FromQuery] string? prompt = null)
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
                
                // Store the origin (Referer or Origin header) for redirect after OAuth
                var origin = Request.Headers["Referer"].FirstOrDefault() ?? 
                            Request.Headers["Origin"].FirstOrDefault();
                
                // Extract just the origin (scheme + host + port) if full URL provided
                if (!string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
                {
                    origin = $"{originUri.Scheme}://{originUri.Authority}";
                }
                
                // Fallback to common frontend URLs if no origin header
                // Check if it's likely Expo (8081) or web (3000)
                if (string.IsNullOrWhiteSpace(origin))
                {
                    // Try to detect from common ports
                    var userAgent = Request.Headers["User-Agent"].ToString();
                    if (userAgent.Contains("Expo") || userAgent.Contains("ReactNative"))
                    {
                        origin = "http://localhost:8081"; // Expo default
                    }
                    else
                    {
                        origin = cfg["Frontend:BaseUrl"] ?? "http://localhost:3000"; // Web default
                    }
                }
                
                Console.WriteLine($"[OAuth Debug] Storing origin for state {state}: {origin}");
                lock (GoogleStateToOrigin)
                {
                    GoogleStateToOrigin[state] = origin;
                }

                var backendCallback = new Uri(new Uri(apiOrigin), "/signin-google").ToString();
                
                var queryParams = new Dictionary<string, string?>
                {
                    ["client_id"] = clientId,
                    ["redirect_uri"] = backendCallback,
                    ["response_type"] = "code",
                    ["scope"] = "openid email profile",
                    ["state"] = state,
                    ["code_challenge"] = codeChallenge,
                    ["code_challenge_method"] = "S256"
                };
                
                // Add prompt parameter if provided, otherwise default to "select_account"
                if (!string.IsNullOrWhiteSpace(prompt))
                {
                    queryParams["prompt"] = Uri.EscapeDataString(prompt);
                }
                else
                {
                    queryParams["prompt"] = "select_account";
                }
                
                var url = QueryHelpers.AddQueryString(
                    "https://accounts.google.com/o/oauth2/v2/auth",
                    queryParams
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
            Console.WriteLine($"[OAuth Debug] Callback received - Provider: '{provider}', State: '{state}', Code present: {!string.IsNullOrEmpty(code)}");
            
            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("[OAuth Error] Code parameter is missing");
                return BadRequest(new { message = "Authorization code is required" });
            }
            
            if (string.IsNullOrEmpty(state))
            {
                Console.WriteLine("[OAuth Error] State parameter is missing");
                return BadRequest(new { message = "State parameter is required" });
            }
            
            string codeVerifier;
            string? origin = null;
            lock (GoogleStateToCodeVerifier)
            {
                if (!GoogleStateToCodeVerifier.TryGetValue(state, out codeVerifier))
                {
                    Console.WriteLine($"[OAuth Error] Invalid state: '{state}'. Available states: {string.Join(", ", GoogleStateToCodeVerifier.Keys)}");
                    return BadRequest(new { message = "Invalid state. The OAuth session may have expired. Please try again." });
                }
                GoogleStateToCodeVerifier.Remove(state);
            }
            
            lock (GoogleStateToOrigin)
            {
                if (GoogleStateToOrigin.TryGetValue(state, out origin))
                {
                    GoogleStateToOrigin.Remove(state);
                }
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            // Use stored origin if available, otherwise fallback to config or default
            var frontendBase = origin ?? config["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var frontendComplete = $"{frontendBase}/oauth-complete";
            
            Console.WriteLine($"[OAuth Debug] Redirecting to frontend: {frontendComplete}");
            var backendAuthority = $"{Request.Scheme}://{Request.Host}";
            var backendCallback = provider.Equals("Google", StringComparison.OrdinalIgnoreCase)
                ? new Uri(new Uri(backendAuthority), "/signin-google").ToString()
                : new Uri(new Uri(backendAuthority), "/signin-twitter").ToString();

            string email = string.Empty;
            string name = "user";
            string googleId = string.Empty;

            if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = config["Authentication:Google:ClientId"];
                var clientSecret = config["Authentication:Google:ClientSecret"];
                
                if (string.IsNullOrWhiteSpace(clientId) || clientId == "YOUR_ACTUAL_GOOGLE_CLIENT_ID")
                {
                    Console.WriteLine("[OAuth Error] Google ClientId is not configured");
                    return StatusCode(500, new { message = "Google OAuth not configured. Please set up Google OAuth credentials in appsettings.json" });
                }
                
                if (string.IsNullOrWhiteSpace(clientSecret))
                {
                    Console.WriteLine("[OAuth Error] Google ClientSecret is not configured");
                    return StatusCode(500, new { message = "Google OAuth ClientSecret is not configured" });
                }
                
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
                    var errorContent = await tokenRes.Content.ReadAsStringAsync();
                    Console.WriteLine($"[OAuth Error] Token exchange failed: Status {tokenRes.StatusCode}, Response: {errorContent}");
                    return StatusCode((int)tokenRes.StatusCode, new { message = "Failed to exchange code", details = errorContent });
                }
                var tokenJson = await tokenRes.Content.ReadAsStringAsync();
                Console.WriteLine($"[OAuth Debug] Token response received");
                var tokenDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
                
                if (!tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenElement))
                {
                    Console.WriteLine($"[OAuth Error] id_token not found in token response: {tokenJson}");
                    return StatusCode(400, new { message = "Invalid token response from Google" });
                }
                
                var idToken = idTokenElement.GetString();
                if (string.IsNullOrEmpty(idToken))
                {
                    Console.WriteLine("[OAuth Error] id_token is null or empty");
                    return StatusCode(400, new { message = "Invalid id_token from Google" });
                }

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
                ClaimsPrincipal principal;
                try
                {
                    principal = handler.ValidateToken(idToken, tokenValidationParameters, out _);
                    Console.WriteLine("[OAuth Debug] ID token validated successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OAuth Error] Token validation failed: {ex.Message}");
                    return StatusCode(400, new { message = "Failed to validate Google token", details = ex.Message });
                }
                
                // Extract claims from Google ID token
                email = principal.FindFirst(ClaimTypes.Email)?.Value 
                    ?? principal.FindFirst("email")?.Value 
                    ?? string.Empty;
                googleId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                    ?? principal.FindFirst("sub")?.Value 
                    ?? string.Empty;
                name = principal.FindFirst(ClaimTypes.Name)?.Value 
                    ?? principal.FindFirst("name")?.Value 
                    ?? "user";
                
                Console.WriteLine($"[OAuth Debug] Email: '{email}', GoogleId: '{googleId}', Name: '{name}'");
                
                // Validate that we received both email and Google ID from Google
                if (string.IsNullOrWhiteSpace(email))
                {
                    Console.WriteLine("[OAuth Error] Failed to retrieve email from Google account");
                    return StatusCode(400, new { message = "Failed to retrieve email from Google account. Please ensure your Google account has an email address and try again." });
                }
                if (string.IsNullOrWhiteSpace(googleId))
                {
                    Console.WriteLine("[OAuth Error] Failed to retrieve Google ID from account");
                    return StatusCode(400, new { message = "Failed to retrieve user ID from Google account" });
                }
                
                var emailVerified = string.Equals(principal.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase);
                if (!emailVerified)
                {
                    Console.WriteLine($"[OAuth Warning] Email not verified for: {email}");
                }
            }
            else if (string.Equals(provider, "Twitter", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "Twitter authentication is currently disabled" });
            }

            // Upsert or get user - prioritize Google ID over email for OAuth users
            var dbContext = HttpContext.RequestServices.GetRequiredService<MongoDbContext>();
            var normalizedEmail = email.Trim().ToLowerInvariant();
            
            Console.WriteLine($"[OAuth Debug] Looking up user - GoogleId: '{googleId}', Email: '{normalizedEmail}'");
            
            auth_service.Models.User? user = null;
            try
            {
                // For Google OAuth, first try to find by Google ID, then by email
                user = await dbContext.Users.Find(u => u.GoogleId == googleId).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OAuth Error] Database query failed: {ex.Message}");
                Console.WriteLine($"[OAuth Error] Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Database error occurred", details = ex.Message });
            }
            if (user == null)
            {
                Console.WriteLine($"[OAuth Debug] No user found with GoogleId, trying email lookup");
                user = await dbContext.Users.Find(u => u.EmailNormalized == normalizedEmail).FirstOrDefaultAsync();
            }
            else
            {
                Console.WriteLine($"[OAuth Debug] Found user by GoogleId: {user.Id}");
            }
            if (user == null)
            {
                Console.WriteLine($"[OAuth Debug] Creating new user with email '{email}' and GoogleId '{googleId}'");
                user = new auth_service.Models.User
                {
                    Username = name,
                    Email = email,
                    EmailNormalized = normalizedEmail,
                    UsernameNormalized = name.Trim().ToLowerInvariant(),
                    PasswordHash = string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    IsEmailVerified = true,
                    GoogleId = googleId
                };
                await dbContext.Users.InsertOneAsync(user);
                Console.WriteLine($"[OAuth Debug] Created new user with ID: {user.Id}");
            }
            else
            {
                Console.WriteLine($"[OAuth Debug] Updating existing user {user.Id} - setting GoogleId to '{googleId}'");
                // Update last active, mark email as verified, and ensure Google ID is set
                var update = MongoDB.Driver.Builders<auth_service.Models.User>.Update
                    .Set(u => u.LastActive, DateTime.UtcNow)
                    .Set(u => u.IsEmailVerified, true)
                    .Set(u => u.GoogleId, googleId);
                await dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            // Issue our JWT: always generate manually for OAuth/social sign-ins
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
            var authResponse = new AuthResponseDTO
            {
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                Token = tokenHandler.WriteToken(token),
                ExpiresAt = descriptor.Expires ?? DateTime.UtcNow.AddDays(7)
            };

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
