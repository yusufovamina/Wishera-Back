using System.ComponentModel.DataAnnotations;

namespace WishlistApp.DTO
{
    public class RegisterDTO
    {
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public required string Username { get; set; }

        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; set; }
    }

    public class LoginDTO
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }

        [Required]
        public required string Password { get; set; }
    }

    public class ForgotPasswordDTO
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
    }

    public class ResetPasswordDTO
    {
        [Required]
        public required string Token { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string NewPassword { get; set; }
    }

    public class AuthResponseDTO
    {
        public required string Token { get; set; }
        public required string UserId { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? AvatarUrl { get; set; }
        public string? ChatToken { get; set; }
    }
} 