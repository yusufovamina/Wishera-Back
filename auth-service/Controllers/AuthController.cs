using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using MongoDB.Driver;
using auth_service.Models;
using auth_service.Services;
using auth_service.DTO;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace auth_service.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDTO>> Register(RegisterDTO registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDTO>> Login(LoginDTO loginDto)
        {
            try
            {
                if (loginDto == null || string.IsNullOrWhiteSpace(loginDto.Email) || string.IsNullOrWhiteSpace(loginDto.Password))
                {
                    return BadRequest(new { message = "Email and password are required" });
                }

                var response = await _authService.LoginAsync(loginDto);
                return Ok(response);
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"Login timeout: {ex.Message}");
                return StatusCode(504, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "An error occurred during login. Please try again later." });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordDTO forgotPasswordDto)
        {
            try
            {
                // Detect if this is a mobile client
                // Only trust explicit X-Client-Type header
                // Don't use User-Agent as it can be misleading (web browsers can have "Mobile" in UA)
                var isMobile = Request.Headers["X-Client-Type"].ToString().Equals("mobile", StringComparison.OrdinalIgnoreCase);
                
                await _authService.ForgotPasswordAsync(forgotPasswordDto.Email, isMobile);
                return Ok(new { message = "Password reset code sent to your email" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-reset-code")]
        public async Task<ActionResult> VerifyResetCode(VerifyResetCodeDTO verifyCodeDto)
        {
            try
            {
                var token = await _authService.VerifyResetCodeAsync(verifyCodeDto.Email, verifyCodeDto.Code);
                return Ok(new { token, message = "Code verified successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword(ResetPasswordDTO resetPasswordDto)
        {
            try
            {
                await _authService.ResetPasswordAsync(resetPasswordDto.Token, resetPasswordDto.NewPassword);
                return Ok(new { message = "Password reset successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("verify-email")]
        public async Task<ActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                await _authService.VerifyEmailAsync(token);
                return Ok(new { message = "Email verified successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("resend-verification")]
        public async Task<ActionResult> ResendVerification([FromBody] ForgotPasswordDTO dto)
        {
            try
            {
                await _authService.ResendVerificationEmailAsync(dto.Email);
                return Ok(new { message = "Verification email sent. Please check your inbox." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpDelete("delete-account")]
        public async Task<ActionResult> DeleteAccount()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _authService.DeleteAccountAsync(userId);
                return Ok(new { message = "Account deleted successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("check-email")]
        public async Task<ActionResult<bool>> CheckEmailAvailability([FromQuery] string email)
        {
            var isAvailable = await _authService.IsEmailUniqueAsync(email);
            return Ok(isAvailable);
        }

        [HttpGet("check-username")]
        public async Task<ActionResult<bool>> CheckUsernameAvailability([FromQuery] string username)
        {
            var isAvailable = await _authService.IsUsernameUniqueAsync(username);
            return Ok(isAvailable);
        }

        // --- OAuth: Start Google login ---
        [HttpHead("external/{provider}")]
        public IActionResult ExternalHead(string provider)
        {
            // Allow HEAD probe from web route to find a valid endpoint
            return Ok();
        }

        [HttpGet("external/{provider}")]
        public IActionResult ExternalLoginStart(string provider, [FromQuery] string? clientType = null)
        {
            var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiOrigin = $"{Request.Scheme}://{Request.Host}";
            
            // Detect if this is a mobile client
            // Only trust explicit clientType parameter or X-Client-Type header
            // Don't use User-Agent as it can be misleading (web browsers can have "Mobile" in UA)
            var isMobile = string.Equals(clientType, "mobile", StringComparison.OrdinalIgnoreCase) ||
                          Request.Headers["X-Client-Type"].ToString().Equals("mobile", StringComparison.OrdinalIgnoreCase);
            
            // Store client type in state for callback
            var clientTypeState = isMobile ? "mobile" : "web";

            if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
            {
                var clientId = cfg["Authentication:Google:ClientId"];
                if (string.IsNullOrWhiteSpace(clientId)) return StatusCode(500, new { message = "Google OAuth not configured" });

                var codeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .Replace("+", "-").Replace("/", "_").Replace("=", string.Empty);
                using var sha256 = SHA256.Create();
                var codeChallengeBytes = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
                var codeChallenge = Convert.ToBase64String(codeChallengeBytes).Replace("+", "-").Replace("/", "_").Replace("=", string.Empty);

                var state = $"google_{clientTypeState}_{Guid.NewGuid():N}";
                OAuthStateManager.StoreCodeVerifier(state, codeVerifier);

                // Get redirect URI - use explicit config if available, otherwise construct from request
                var explicitRedirectUri = cfg["Authentication:Google:RedirectUri"];
                var backendCallback = !string.IsNullOrWhiteSpace(explicitRedirectUri)
                    ? explicitRedirectUri
                    : new Uri(new Uri(apiOrigin), "/signin-google").ToString();
                
                Console.WriteLine($"[OAuth Login] Using redirect URI: {backendCallback}");
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
                        ["code_challenge_method"] = "S256",
                        ["prompt"] = "select_account"
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

        // --- OAuth: Google callback ---
        [HttpGet("callback/{provider}")]
        public async Task<IActionResult> ExternalCallback([FromQuery] string code, [FromQuery] string state, string provider)
        {
            // Use shared OAuthStateManager to retrieve code verifier (works across controllers)
            var codeVerifier = OAuthStateManager.TryGetAndRemoveCodeVerifier(state);
            if (string.IsNullOrEmpty(codeVerifier))
            {
                return BadRequest(new { message = "Invalid state" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            
            // Detect if this is a mobile client from the state parameter
            var isMobile = state.StartsWith("google_mobile_", StringComparison.OrdinalIgnoreCase);
            
            // Use appropriate redirect URL based on client type
            string frontendComplete;
            if (isMobile)
            {
                // Mobile uses deep link
                var mobileBaseUrl = config["Frontend:MobileBaseUrl"] ?? "wishera://";
                frontendComplete = $"{mobileBaseUrl}oauth-complete";
            }
            else
            {
                // Web uses HTTP URL
                // Try to get the origin from Referer or Origin header for dynamic detection
                var referer = Request.Headers["Referer"].ToString();
                var origin = Request.Headers["Origin"].ToString();
                string frontendBase;
                
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
                
                frontendComplete = $"{frontendBase}/oauth-complete";
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

    }
}
