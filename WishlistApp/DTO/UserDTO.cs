using System.ComponentModel.DataAnnotations;

namespace WisheraApp.DTO
{
    public class UserProfileDTO
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public required string Email { get; set; }
        public string? Bio { get; set; }
        public List<string> Interests { get; set; } = new List<string>();
        public string? AvatarUrl { get; set; }
        public string? Birthday { get; set; }
        public DateTime CreatedAt { get; set; }
        public int FollowingCount { get; set; }
        public int FollowersCount { get; set; }
        public int WishlistCount { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsFollowing { get; set; }
    }

    public class UpdateUserProfileDTO
    {
        [StringLength(50, MinimumLength = 3)]
        public string? Username { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        public List<string>? Interests { get; set; }
        public bool IsPrivate { get; set; }
        public string? Birthday { get; set; }
    }

    public class UserSearchDTO
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public int MutualFriendsCount { get; set; }
    }

    public class BirthdayReminderDTO
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public DateTime Birthday { get; set; }
        public int DaysUntilBirthday { get; set; }
        public string Message { get; set; } = string.Empty;
    }
} 