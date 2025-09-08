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
        private readonly IRabbitMqAuthClient _rpcClient;

        public AuthService(IRabbitMqAuthClient rpcClient)
        {
            _rpcClient = rpcClient;
        }

        public async Task<AuthResponseDTO> RegisterAsync(RegisterDTO registerDto)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(registerDto);
            var resp = await _rpcClient.SendRpcAsync("auth.register", json);
            return System.Text.Json.JsonSerializer.Deserialize<AuthResponseDTO>(resp)!;
        }

        public async Task<AuthResponseDTO> LoginAsync(LoginDTO loginDto)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(loginDto);
            var resp = await _rpcClient.SendRpcAsync("auth.login", json);
            return System.Text.Json.JsonSerializer.Deserialize<AuthResponseDTO>(resp)!;
        }

        public async Task<bool> IsEmailUniqueAsync(string email)
        {
            var resp = await _rpcClient.SendRpcAsync("auth.checkEmail", email);
            return System.Text.Json.JsonSerializer.Deserialize<bool>(resp);
        }

        public async Task<bool> IsUsernameUniqueAsync(string username)
        {
            var resp = await _rpcClient.SendRpcAsync("auth.checkUsername", username);
            return System.Text.Json.JsonSerializer.Deserialize<bool>(resp);
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new ForgotPasswordDTO { Email = email });
            await _rpcClient.SendRpcAsync("auth.forgot", payload);
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new ResetPasswordDTO { Token = token, NewPassword = newPassword });
            await _rpcClient.SendRpcAsync("auth.reset", payload);
        }
    }
} 