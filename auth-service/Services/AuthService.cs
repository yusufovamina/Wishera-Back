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
        Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto, bool isMobile = false);
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<bool> IsEmailUniqueAsync(string email);
        Task<bool> IsUsernameUniqueAsync(string username);
        Task ForgotPasswordAsync(string email, bool isMobile = false);
        Task<string> VerifyResetCodeAsync(string email, string code);
        Task ResetPasswordAsync(string token, string newPassword);
        Task VerifyEmailAsync(string token);
        Task ResendVerificationEmailAsync(string email);
        Task ResendVerificationCodeAsync(string email);
        Task VerifyEmailCodeAsync(string email, string code);
        Task DeleteAccountAsync(string userId);
        Task SendLoginConfirmationCodeAsync(string email);
        Task<AuthResponseDTO> VerifyLoginCodeAsync(string email, string code);
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

        public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto, bool isMobile = false)
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

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                EmailNormalized = emailNormalized,
                UsernameNormalized = usernameNormalized,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow,
                IsEmailVerified = false
            };

            if (isMobile)
            {
                // Generate a 6-digit verification code for mobile
                var random = new Random();
                var verificationCode = random.Next(100000, 999999).ToString();
                var codeExpiry = DateTime.UtcNow.AddMinutes(15);

                user.EmailVerificationCode = verificationCode;
                user.EmailVerificationCodeExpiry = codeExpiry;

                await _dbContext.Users.InsertOneAsync(user);

                // Send verification code email
                try
                {
                    await _emailService.SendEmailVerificationCodeAsync(user.Email, verificationCode, user.Username);
                    Console.WriteLine($"Email verification code sent to {user.Email}");
                }
                catch (Exception ex)
                {
                    // Log the error but don't fail registration
                    Console.WriteLine($"Failed to send verification code: {ex.Message}");
                }
            }
            else
            {
                // Generate email verification token for web
                var verificationToken = Guid.NewGuid().ToString("N");
                var verificationTokenExpiry = DateTime.UtcNow.AddDays(7);

                user.EmailVerificationToken = verificationToken;
                user.EmailVerificationTokenExpiry = verificationTokenExpiry;

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
                throw new InvalidOperationException("Please verify your email address before logging in. Check your inbox for the verification code.");

            // For first-time sign-in, require code confirmation
            if (!user.HasCompletedFirstLogin)
            {
                // Generate a 6-digit confirmation code
                var random = new Random();
                var confirmationCode = random.Next(100000, 999999).ToString();
                var codeExpiry = DateTime.UtcNow.AddMinutes(15);

                var update = Builders<User>.Update
                    .Set(u => u.LoginConfirmationCode, confirmationCode)
                    .Set(u => u.LoginConfirmationCodeExpiry, codeExpiry);

                await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

                Console.WriteLine($"Generated login confirmation code for user {user.Username} ({user.Email}): {confirmationCode}");

                try
                {
                    // Send login confirmation code email
                    await _emailService.SendLoginConfirmationCodeAsync(user.Email, confirmationCode, user.Username);
                    Console.WriteLine($"Login confirmation code email sent successfully to {user.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send login confirmation code email: {ex.Message}");
                    throw;
                }

                // Throw exception to indicate code is required
                throw new InvalidOperationException("LOGIN_CODE_REQUIRED");
            }

            // Update last active timestamp
            var updateLastActive = Builders<User>.Update.Set(u => u.LastActive, DateTime.UtcNow);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, updateLastActive);

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

        public async Task ForgotPasswordAsync(string email, bool isMobile = false)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("If an account with this email exists, a password reset code will be sent");

            if (isMobile)
            {
                // Generate a 6-digit code for mobile
                var random = new Random();
                var resetCode = random.Next(100000, 999999).ToString();
                var resetCodeExpiry = DateTime.UtcNow.AddMinutes(15);

                var update = Builders<User>.Update
                    .Set(u => u.ResetPasswordCode, resetCode)
                    .Set(u => u.ResetPasswordCodeExpiry, resetCodeExpiry)
                    .Set(u => u.ResetPasswordToken, (string?)null)
                    .Set(u => u.ResetPasswordTokenExpiry, (DateTime?)null);

                await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

                Console.WriteLine($"Generated reset code for user {user.Username} ({user.Email}): {resetCode}");

                try
                {
                    // Send password reset code email
                    await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode, user.Username);
                    Console.WriteLine($"Password reset code email sent successfully to {user.Email}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send password reset code email: {ex.Message}");
                    throw;
                }
            }
            else
            {
                // Generate a secure reset token for web
                var resetToken = Guid.NewGuid().ToString("N");
                var resetTokenExpiry = DateTime.UtcNow.AddHours(24);

                var update = Builders<User>.Update
                    .Set(u => u.ResetPasswordToken, resetToken)
                    .Set(u => u.ResetPasswordTokenExpiry, resetTokenExpiry)
                    .Set(u => u.ResetPasswordCode, (string?)null)
                    .Set(u => u.ResetPasswordCodeExpiry, (DateTime?)null);

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
        }

        public async Task<string> VerifyResetCodeAsync(string email, string code)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid email or code");

            if (string.IsNullOrEmpty(user.ResetPasswordCode) || user.ResetPasswordCode != code)
                throw new InvalidOperationException("Invalid reset code");

            if (user.ResetPasswordCodeExpiry == null || user.ResetPasswordCodeExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Reset code has expired");

            // Generate a token for password reset (similar to web flow)
            var resetToken = Guid.NewGuid().ToString("N");
            var resetTokenExpiry = DateTime.UtcNow.AddHours(24);

            // Clear the code and set the token
            var update = Builders<User>.Update
                .Set(u => u.ResetPasswordCode, (string?)null)
                .Set(u => u.ResetPasswordCodeExpiry, (DateTime?)null)
                .Set(u => u.ResetPasswordToken, resetToken)
                .Set(u => u.ResetPasswordTokenExpiry, resetTokenExpiry);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return resetToken;
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
                .Set(u => u.EmailVerificationTokenExpiry, verificationTokenExpiry)
                .Set(u => u.EmailVerificationCode, (string?)null)
                .Set(u => u.EmailVerificationCodeExpiry, (DateTime?)null);

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

        public async Task ResendVerificationCodeAsync(string email)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            
            if (user == null)
                throw new InvalidOperationException("No account found with this email address");

            if (user.IsEmailVerified)
                throw new InvalidOperationException("This email address is already verified");

            // Generate a 6-digit verification code
            var random = new Random();
            var verificationCode = random.Next(100000, 999999).ToString();
            var codeExpiry = DateTime.UtcNow.AddMinutes(15);

            var update = Builders<User>.Update
                .Set(u => u.EmailVerificationCode, verificationCode)
                .Set(u => u.EmailVerificationCodeExpiry, codeExpiry)
                .Set(u => u.EmailVerificationToken, (string?)null)
                .Set(u => u.EmailVerificationTokenExpiry, (DateTime?)null);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            // Send verification code email
            try
            {
                await _emailService.SendEmailVerificationCodeAsync(user.Email, verificationCode, user.Username);
                Console.WriteLine($"Verification code resent to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resend verification code: {ex.Message}");
                throw;
            }
        }

        public async Task VerifyEmailCodeAsync(string email, string code)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid email or code");

            if (string.IsNullOrEmpty(user.EmailVerificationCode) || user.EmailVerificationCode != code)
                throw new InvalidOperationException("Invalid verification code");

            if (user.EmailVerificationCodeExpiry == null || user.EmailVerificationCodeExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Verification code has expired");

            var update = Builders<User>.Update
                .Set(u => u.IsEmailVerified, true)
                .Set(u => u.EmailVerificationCode, (string?)null)
                .Set(u => u.EmailVerificationCodeExpiry, (DateTime?)null)
                .Set(u => u.EmailVerificationToken, (string?)null)
                .Set(u => u.EmailVerificationTokenExpiry, (DateTime?)null);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
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

        public async Task SendLoginConfirmationCodeAsync(string email)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid email");

            // Generate a 6-digit confirmation code
            var random = new Random();
            var confirmationCode = random.Next(100000, 999999).ToString();
            var codeExpiry = DateTime.UtcNow.AddMinutes(15);

            var update = Builders<User>.Update
                .Set(u => u.LoginConfirmationCode, confirmationCode)
                .Set(u => u.LoginConfirmationCodeExpiry, codeExpiry);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            Console.WriteLine($"Generated login confirmation code for user {user.Username} ({user.Email}): {confirmationCode}");

            try
            {
                // Send login confirmation code email
                await _emailService.SendLoginConfirmationCodeAsync(user.Email, confirmationCode, user.Username);
                Console.WriteLine($"Login confirmation code email sent successfully to {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send login confirmation code email: {ex.Message}");
                throw;
            }
        }

        public async Task<AuthResponseDTO> VerifyLoginCodeAsync(string email, string code)
        {
            var emailNormalized = email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users.Find(u => u.EmailNormalized == emailNormalized).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid email or code");

            if (string.IsNullOrEmpty(user.LoginConfirmationCode) || user.LoginConfirmationCode != code)
                throw new InvalidOperationException("Invalid confirmation code");

            if (user.LoginConfirmationCodeExpiry == null || user.LoginConfirmationCodeExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Confirmation code has expired");

            // Clear the code and mark first login as completed
            var update = Builders<User>.Update
                .Set(u => u.LoginConfirmationCode, (string?)null)
                .Set(u => u.LoginConfirmationCodeExpiry, (DateTime?)null)
                .Set(u => u.HasCompletedFirstLogin, true)
                .Set(u => u.LastActive, DateTime.UtcNow);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return await GenerateAuthResponseAsync(user);
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