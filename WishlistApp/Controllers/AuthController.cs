using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using MongoDB.Driver;
using WisheraApp.Models;
using WisheraApp.Services;
using WisheraApp.DTO;

namespace WisheraApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IChatIntegrationService _chatIntegrationService;

        public AuthController(IAuthService authService, IChatIntegrationService chatIntegrationService)
        {
            _authService = authService;
            _chatIntegrationService = chatIntegrationService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDTO>> Register(RegisterDTO registerDto)
        {
            try
            {
                var response = await _authService.RegisterAsync(registerDto);
                // Register user with chat service on successful registration
                await _chatIntegrationService.UpsertUserAsync(response.UserId);
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
                var response = await _authService.LoginAsync(loginDto);
                // Try to get chat token on successful login, but don't fail if chat service is unavailable
                try
                {
                    var chatToken = await _chatIntegrationService.GetUserTokenAsync(response.UserId);
                    response.ChatToken = chatToken; // Assuming AuthResponseDTO has a ChatToken property
                }
                catch (Exception)
                {
                    // Chat service unavailable, continue without chat token
                    response.ChatToken = null;
                }
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordDTO forgotPasswordDto)
        {
            try
            {
                await _authService.ForgotPasswordAsync(forgotPasswordDto.Email);
                return Ok(new { message = "Password reset link sent to your email" });
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
    }
}
