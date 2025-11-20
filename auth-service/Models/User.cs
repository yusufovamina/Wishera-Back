using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using auth_service.Serializers;

namespace auth_service.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        
        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;
        
        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        // Normalized (lowercased) fields for uniqueness lookups
        [BsonElement("emailNormalized")]
        public string EmailNormalized { get; set; } = string.Empty;
        
        [BsonElement("usernameNormalized")]
        public string UsernameNormalized { get; set; } = string.Empty;
        
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;
        
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }
        
        [BsonElement("lastActive")]
        public DateTime LastActive { get; set; }
        
        [BsonElement("isEmailVerified")]
        public bool IsEmailVerified { get; set; } = false;
        
        [BsonElement("emailVerificationToken")]
        public string? EmailVerificationToken { get; set; }
        
        [BsonElement("emailVerificationTokenExpiry")]
        public DateTime? EmailVerificationTokenExpiry { get; set; }

        [BsonElement("emailVerificationCode")]
        public string? EmailVerificationCode { get; set; }
        
        [BsonElement("emailVerificationCodeExpiry")]
        public DateTime? EmailVerificationCodeExpiry { get; set; }
        
        [BsonElement("resetPasswordToken")]
        public string? ResetPasswordToken { get; set; }
        
        [BsonElement("resetPasswordTokenExpiry")]
        public DateTime? ResetPasswordTokenExpiry { get; set; }

        [BsonElement("resetPasswordCode")]
        public string? ResetPasswordCode { get; set; }
        
        [BsonElement("resetPasswordCodeExpiry")]
        public DateTime? ResetPasswordCodeExpiry { get; set; }

        [BsonElement("loginConfirmationCode")]
        public string? LoginConfirmationCode { get; set; }
        
        [BsonElement("loginConfirmationCodeExpiry")]
        public DateTime? LoginConfirmationCodeExpiry { get; set; }

        [BsonElement("hasCompletedFirstLogin")]
        public bool HasCompletedFirstLogin { get; set; } = false;

        // Fields expected by other services (keep defaults so downstream reads are safe)
        [BsonElement("role")]
        public string Role { get; set; } = "user";

        [BsonElement("bio")]
        public string Bio { get; set; } = string.Empty;

        [BsonElement("interests")]
        public List<string> Interests { get; set; } = new List<string>();

        [BsonElement("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        [BsonElement("followerIds")]
        public List<string> FollowerIds { get; set; } = new List<string>();

        [BsonElement("followingIds")]
        public List<string> FollowingIds { get; set; } = new List<string>();

        [BsonElement("wishlistIds")]
        public List<string> WishlistIds { get; set; } = new List<string>();

        [BsonElement("isPrivate")]
        public bool IsPrivate { get; set; } = false;

        [BsonElement("allowedViewerIds")]
        public List<string> AllowedViewerIds { get; set; } = new List<string>();

        [BsonElement("birthday")]
        [BsonSerializer(typeof(NullableDateTimeSerializer))]
        public DateTime? Birthday { get; set; }

        // OAuth provider IDs for external authentication
        [BsonElement("googleId")]
        public string? GoogleId { get; set; }
    }
}
