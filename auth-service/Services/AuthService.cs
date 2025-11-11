using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using auth_service.DTO;
using auth_service.Models;
using auth_service.Services;

namespace auth_service.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto);
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<bool> IsEmailUniqueAsync(string email);
        Task<bool> IsUsernameUniqueAsync(string username);
        Task ForgotPasswordAsync(string email);
        Task ResetPasswordAsync(string token, string newPassword);
        Task VerifyEmailAsync(string token);
        Task ResendVerificationEmailAsync(string email);
        Task DeleteAccountAsync(string userId);
    }

    public class AuthService : IAuthService
    {
        private readonly MongoDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthService(MongoDbContext dbContext, IConfiguration configuration, IEmailService emailService)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto)
        {
            var emailNormalized = registerDto.Email.Trim().ToLowerInvariant();
            var usernameNormalized = registerDto.Username.Trim().ToLowerInvariant();

            // Validate password security
            ValidatePasswordStrength(registerDto.Password);

            // ensure unique by normalized fields
            var emailExists = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).AnyAsync();
            if (emailExists)
                throw new InvalidOperationException("Email is already registered");

            var usernameExists = await _dbContext.Users.Find(u => u.UsernameNormalized == usernameNormalized).AnyAsync();
            if (usernameExists)
                throw new InvalidOperationException("Username is already taken");

            // Generate email verification token
            var verificationToken = Guid.NewGuid().ToString("N");
            var verificationTokenExpiry = DateTime.UtcNow.AddDays(7);

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                EmailNormalized = emailNormalized,
                UsernameNormalized = usernameNormalized,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow,
                IsEmailVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = verificationTokenExpiry
            };

            await _dbContext.Users.InsertOneAsync(user);

            // Send verification email
            try
            {
                await _emailService.SendEmailVerificationAsync(user.Email, verificationToken, user.Username);
                Console.WriteLine($"Email verification sent to {user.Email}");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail registration
                Console.WriteLine($"Failed to send verification email: {ex.Message}");
            }

            return await GenerateAuthResponseAsync(user);
        }

        private void ValidatePasswordStrength(string password)
        {
            if (password.Length < 8)
                throw new InvalidOperationException("Password must be at least 8 characters long");
            
            if (!password.Any(char.IsUpper))
                throw new InvalidOperationException("Password must contain at least one uppercase letter");
            
            if (!password.Any(char.IsLower))
                throw new InvalidOperationException("Password must contain at least one lowercase letter");
            
            if (!password.Any(char.IsDigit))
                throw new InvalidOperationException("Password must contain at least one number");
            
            // Check for special characters (any non-alphanumeric character)
            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                throw new InvalidOperationException("Password must contain at least one special character");
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var emailNormalized = loginDto.Email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Invalid email or password");

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                throw new InvalidOperationException("Invalid email or password");

            // Check if email is verified
            if (!user.IsEmailVerified)
                throw new InvalidOperationException("Please verify your email address before logging in. Check your inbox for the verification link.");

            // Update last active timestamp
            var update = Builders<User>.Update.Set(u => u.LastActive, DateTime.UtcNow);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return !await _dbContext.Users.Find(u => u.EmailNormalized == normalized).AnyAsync();
        }

        public async Task<bool> IsUsernameUniqueAsync(string username)
        {
            var normalized = username.Trim().ToLowerInvariant();
            return !await _dbContext.Users.Find(u => u.UsernameNormalized == normalized).AnyAsync();
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var user = await _dbContext.Users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("If an account with this email exists, a password reset link will be sent");

            // Generate a secure reset token
            var resetToken = Guid.NewGuid().ToString("N");
            var resetTokenExpiry = DateTime.UtcNow.AddHours(24);

            var update = Builders<User>.Update
                .Set(u => u.ResetPasswordToken, resetToken)
                .Set(u => u.ResetPasswordTokenExpiry, resetTokenExpiry);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            Console.WriteLine($"Generated reset token for user {user.Username} ({user.Email})");

            try
            {
                // Send password reset email
                await _emailService.SendPasswordResetEmailAsync(user.Email, resetToken, user.Username);
                Console.WriteLine($"Password reset email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send password reset email: {ex.Message}");
                throw;
            }
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _dbContext.Users.Find(u => u.ResetPasswordToken == token).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid reset token");

            if (user.ResetPasswordTokenExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Reset token has expired");

            // Validate new password strength
            ValidatePasswordStrength(newPassword);

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var update = Builders<User>.Update
                .Set(u => u.PasswordHash, passwordHash)
                .Set(u => u.ResetPasswordToken, (string?)null)
                .Set(u => u.ResetPasswordTokenExpiry, (DateTime?)null);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
        }

        public async Task VerifyEmailAsync(string token)
        {
            var user = await _dbContext.Users.Find(u => u.EmailVerificationToken == token).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid verification token");

            if (user.EmailVerificationTokenExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Verification token has expired");

            var update = Builders<User>.Update
                .Set(u => u.IsEmailVerified, true)
                .Set(u => u.EmailVerificationToken, (string?)null)
                .Set(u => u.EmailVerificationTokenExpiry, (DateTime?)null);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
        }

        public async Task ResendVerificationEmailAsync(string email)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            
            if (user == null)
                throw new InvalidOperationException("No account found with this email address");

            if (user.IsEmailVerified)
                throw new InvalidOperationException("This email address is already verified");

            // Generate new verification token
            var verificationToken = Guid.NewGuid().ToString("N");
            var verificationTokenExpiry = DateTime.UtcNow.AddDays(7);

            var update = Builders<User>.Update
                .Set(u => u.EmailVerificationToken, verificationToken)
                .Set(u => u.EmailVerificationTokenExpiry, verificationTokenExpiry);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            // Send verification email
            try
            {
                await _emailService.SendEmailVerificationAsync(user.Email, verificationToken, user.Username);
                Console.WriteLine($"Verification email resent to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resend verification email: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAccountAsync(string userId)
        {
            var user = await _dbContext.Users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            
            if (user == null)
                throw new InvalidOperationException("User not found");

            // Delete the user account
            await _dbContext.Users.DeleteOneAsync(u => u.Id == userId);

            Console.WriteLine($"Account deleted for user: {user.Username} ({user.Email})");
        }

        private async Task<AuthResponseDTO> GenerateAuthResponseAsync(User user)
        {
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
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = await Task.Run(() => tokenHandler.CreateToken(tokenDescriptor));

            return new AuthResponseDTO
            {
                UserId = user.Id,
                Token = tokenHandler.WriteToken(token),
                Username = user.Username,
                Email = user.Email,
                ExpiresAt = tokenDescriptor.Expires ?? DateTime.UtcNow.AddDays(7)
            };
        }
    }
} 