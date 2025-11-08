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
        public IActionResult Login(string provider, [FromQuery] string? prompt = null, [FromQuery] string? clientType = null)
        {
            var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiOrigin = $"{Request.Scheme}://{Request.Host}";
            
            // Detect if this is a mobile client
            // Only trust explicit clientType parameter or X-Client-Type header
            // Don't use User-Agent as it can be misleading (web browsers can have "Mobile" in UA)
            var clientTypeParam = clientType ?? "";
            var clientTypeHeader = Request.Headers["X-Client-Type"].ToString();
            var isMobile = string.Equals(clientTypeParam, "mobile", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(clientTypeHeader, "mobile", StringComparison.OrdinalIgnoreCase);
            
            Console.WriteLine($"[OAuth Login] Provider: {provider}, clientType param: '{clientTypeParam}', clientType header: '{clientTypeHeader}', isMobile: {isMobile}");
            
            // Store client type in state for callback
            var clientTypeState = isMobile ? "mobile" : "web";
            
            // Get the origin from the request for web clients (to redirect back correctly)
            string? frontendOrigin = null;
            if (!isMobile)
            {
                var origin = Request.Headers["Origin"].ToString();
                var referer = Request.Headers["Referer"].ToString();
                
                if (!string.IsNullOrEmpty(origin))
                {
                    frontendOrigin = origin;
                }
                else if (!string.IsNullOrEmpty(referer))
                {
                    try
                    {
                        var refererUri = new Uri(referer);
                        frontendOrigin = $"{refererUri.Scheme}://{refererUri.Authority}";
                    }
                    catch { }
                }
                
                Console.WriteLine($"[OAuth Login] Frontend origin detected: {frontendOrigin ?? "none"}");
            }

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

                // Store state with client type
                // Store base state only (Google will preserve this), and origin separately
                var stateGuid = Guid.NewGuid().ToString("N");
                var state = isMobile 
                    ? $"google_mobile_{stateGuid}"
                    : $"google_web_{stateGuid}";
                
                Console.WriteLine($"[OAuth Login] Created state: {state} (clientTypeState: {clientTypeState})");
                if (frontendOrigin != null)
                {
                    Console.WriteLine($"[OAuth Login] Storing frontend origin: {frontendOrigin}");
                }
                
                // Store code verifier with base state, and frontend origin separately
                lock (GoogleStateToCodeVerifier) 
                { 
                    GoogleStateToCodeVerifier[state] = codeVerifier;
                    if (frontendOrigin != null)
                    {
                        // Store origin separately keyed by GUID
                        GoogleStateToCodeVerifier[$"origin_{stateGuid}"] = frontendOrigin;
                    }
                }

                // Get redirect URI - use explicit config if available, otherwise construct from request
                var explicitRedirectUri = cfg["Authentication:Google:RedirectUri"];
                var backendCallback = !string.IsNullOrWhiteSpace(explicitRedirectUri)
                    ? explicitRedirectUri
                    : new Uri(new Uri(apiOrigin), "/signin-google").ToString();
                
                Console.WriteLine($"[OAuth Login] Using redirect URI: {backendCallback}");
                
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
            string codeVerifier;
            string? storedOrigin = null;
            
            // State from Google should be just the base state (e.g., google_web_xxx)
            // But handle case where Google might have added something
            var stateBase = state.Split('|')[0]; // Get base state in case Google modified it
            
            lock (GoogleStateToCodeVerifier)
            {
                if (!GoogleStateToCodeVerifier.TryGetValue(stateBase, out codeVerifier))
                {
                    return BadRequest(new { message = "Invalid state" });
                }
                
                GoogleStateToCodeVerifier.Remove(stateBase);
                
                // Extract state GUID to look up stored origin
                // State format: google_web_xxx or google_mobile_xxx
                var stateGuidParts = stateBase.Split('_');
                if (stateGuidParts.Length >= 3)
                {
                    var stateGuid = stateGuidParts[2];
                    // Try to retrieve stored origin (only for web clients)
                    if (!stateBase.StartsWith("google_mobile_"))
                    {
                        if (GoogleStateToCodeVerifier.TryGetValue($"origin_{stateGuid}", out var origin) && origin != null)
                        {
                            storedOrigin = origin;
                            GoogleStateToCodeVerifier.Remove($"origin_{stateGuid}");
                        }
                    }
                }
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            
            // Detect if this is a mobile client from the state parameter
            var isMobile = stateBase.StartsWith("google_mobile_", StringComparison.OrdinalIgnoreCase);
            
            Console.WriteLine($"[OAuth Callback] State: {state}, stateBase: {stateBase}, isMobile: {isMobile}, storedOrigin: {storedOrigin ?? "none"}");
            
            // Use appropriate redirect URL based on client type
            string frontendComplete;
            if (isMobile)
            {
                // Mobile uses deep link
                var mobileBaseUrl = config["Frontend:MobileBaseUrl"] ?? "wishera://";
                frontendComplete = $"{mobileBaseUrl}oauth-complete";
                Console.WriteLine($"[OAuth Callback] Using mobile deep link: {frontendComplete}");
            }
            else
            {
                // Web uses HTTP URL
                // First try stored origin, then headers, then fallback
                string frontendBase;
                
                if (!string.IsNullOrEmpty(storedOrigin))
                {
                    // Use the origin we stored during login
                    frontendBase = storedOrigin;
                    Console.WriteLine($"[OAuth Callback] Using stored origin: {frontendBase}");
                }
                else
                {
                    // Try to get the origin from Referer or Origin header (may not be available after Google redirect)
                    var referer = Request.Headers["Referer"].ToString();
                    var origin = Request.Headers["Origin"].ToString();
                    
                    Console.WriteLine($"[OAuth Callback] Web client detected. Origin: {origin}, Referer: {referer}");
                    
                    if (!string.IsNullOrEmpty(origin))
                    {
                        // Use the origin from the request (e.g., http://localhost:19006)
                        frontendBase = origin;
                    }
                    else if (!string.IsNullOrEmpty(referer))
                    {
                        // Parse origin from referer URL
                        try
                        {
                            var refererUri = new Uri(referer);
                            frontendBase = $"{refererUri.Scheme}://{refererUri.Authority}";
                        }
                        catch
                        {
                            frontendBase = config["Frontend:BaseUrl"] ?? "http://localhost:3000";
                        }
                    }
                    else
                    {
                        // Fallback to configured URL
                        frontendBase = config["Frontend:BaseUrl"] ?? "http://localhost:3000";
                    }
                }
                
                frontendComplete = $"{frontendBase}/oauth-complete";
                Console.WriteLine($"[OAuth Callback] Using web URL: {frontendComplete}");
            }
            
            // Get redirect URI - use explicit config if available, otherwise construct from request
            var explicitRedirectUri = config["Authentication:Google:RedirectUri"];
            var backendAuthority = $"{Request.Scheme}://{Request.Host}";
            var backendCallback = provider.Equals("Google", StringComparison.OrdinalIgnoreCase)
                ? (!string.IsNullOrWhiteSpace(explicitRedirectUri)
                    ? explicitRedirectUri
                    : new Uri(new Uri(backendAuthority), "/signin-google").ToString())
                : new Uri(new Uri(backendAuthority), "/signin-twitter").ToString();
            
            Console.WriteLine($"[OAuth Callback] Using redirect URI: {backendCallback}");

            string email = string.Empty;
            string name = "user";
            string googleId = string.Empty;

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
            
            // For Google OAuth, first try to find by Google ID, then by email
            var user = await dbContext.Users.Find(u => u.GoogleId == googleId).FirstOrDefaultAsync();
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
