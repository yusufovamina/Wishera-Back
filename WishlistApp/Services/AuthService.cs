using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WisheraApp.DTO;
using WisheraApp.Models;

namespace WisheraApp.Services
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
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public AuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto)
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5219/api/auth/register", registerDto);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthResponseDTO>() ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5219/api/auth/login", loginDto);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AuthResponseDTO>() ?? throw new InvalidOperationException("Failed to deserialize response");
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5219/api/auth/check-email?email={Uri.EscapeDataString(email)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<bool>();
        }

        public async Task<bool> IsUsernameUniqueAsync(string username)
        {
            var response = await _httpClient.GetAsync($"http://localhost:5219/api/auth/check-username?username={Uri.EscapeDataString(username)}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<bool>();
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var payload = new ForgotPasswordDTO { Email = email };
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5219/api/auth/forgot-password", payload);
            response.EnsureSuccessStatusCode();
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            var payload = new ResetPasswordDTO { Token = token, NewPassword = newPassword };
            var response = await _httpClient.PostAsJsonAsync("http://localhost:5219/api/auth/reset-password", payload);
            response.EnsureSuccessStatusCode();
        }
    }
} 