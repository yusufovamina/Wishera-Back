using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WishlistApp.DTO;
using WishlistApp.Models;

namespace WishlistApp.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto);
        Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto);
        Task<bool> IsEmailUniqueAsync(string email);
        Task<bool> IsUsernameUniqueAsync(string username);
        Task ForgotPasswordAsync(string email);
        Task ResetPasswordAsync(string token, string newPassword);
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
            if (!await IsEmailUniqueAsync(registerDto.Email))
                throw new InvalidOperationException("Email is already registered");

            if (!await IsUsernameUniqueAsync(registerDto.Username))
                throw new InvalidOperationException("Username is already taken");

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
                CreatedAt = DateTime.UtcNow,
                LastActive = DateTime.UtcNow
            };

            await _dbContext.Users.InsertOneAsync(user);

            // Send welcome email
            try
            {
                await _emailService.SendWelcomeEmailAsync(user.Email, user.Username);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail registration
                Console.WriteLine($"Failed to send welcome email: {ex.Message}");
            }

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var user = await _dbContext.Users.Find(u => u.Email == loginDto.Email).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("Invalid email or password");

            if (!BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                throw new InvalidOperationException("Invalid email or password");

            // Update last active timestamp
            var update = Builders<User>.Update.Set(u => u.LastActive, DateTime.UtcNow);
            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);

            return await GenerateAuthResponseAsync(user);
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            return !await _dbContext.Users.Find(u => u.Email == email).AnyAsync();
        }

        public async Task<bool> IsUsernameUniqueAsync(string username)
        {
            return !await _dbContext.Users.Find(u => u.Username == username).AnyAsync();
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
                .Set(u => u.ResetToken, resetToken)
                .Set(u => u.ResetTokenExpiry, resetTokenExpiry);

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
            var user = await _dbContext.Users.Find(u => u.ResetToken == token).FirstOrDefaultAsync();
            if (user == null)
                throw new InvalidOperationException("Invalid reset token");

            if (user.ResetTokenExpiry < DateTime.UtcNow)
                throw new InvalidOperationException("Reset token has expired");

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var update = Builders<User>.Update
                .Set(u => u.PasswordHash, passwordHash)
                .Set(u => u.ResetToken, (string?)null)
                .Set(u => u.ResetTokenExpiry, (DateTime?)null);

            await _dbContext.Users.UpdateOneAsync(u => u.Id == user.Id, update);
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
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = await Task.Run(() => tokenHandler.CreateToken(tokenDescriptor));

            return new AuthResponseDTO
            {
                Token = tokenHandler.WriteToken(token),
                UserId = user.Id,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl
            };
        }
    }
} 