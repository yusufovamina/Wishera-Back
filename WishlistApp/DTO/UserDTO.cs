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
        public DateTime CreatedAt { get; set; }
        public int FollowingCount { get; set; }
        public int FollowersCount { get; set; }
        public int WishlistCount { get; set; }
        public bool IsPrivate { get; set; }
        public bool IsFollowing { get; set; }
        public DateTime? Birthday { get; set; }
    }

    public class UpdateUserProfileDTO
    {
        [StringLength(50, MinimumLength = 3)]
        public string? Username { get; set; }

        [StringLength(500)]
        public string? Bio { get; set; }

        public List<string>? Interests { get; set; }
        public bool IsPrivate { get; set; }
        public DateTime? Birthday { get; set; }
    }

    public class UserSearchDTO
    {
        public required string Id { get; set; }
        public required string Username { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsFollowing { get; set; }
        public int MutualFriendsCount { get; set; }
    }

    public class UpdateBirthdayDTO
    {
        public DateTime? Birthday { get; set; }
    }
} 