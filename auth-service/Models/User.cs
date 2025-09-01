using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

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
        
        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;
        
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; }
        
        [BsonElement("lastActive")]
        public DateTime LastActive { get; set; }
        
        [BsonElement("isEmailVerified")]
        public bool IsEmailVerified { get; set; } = false;
        
        [BsonElement("resetPasswordToken")]
        public string? ResetPasswordToken { get; set; }
        
        [BsonElement("resetPasswordTokenExpiry")]
        public DateTime? ResetPasswordTokenExpiry { get; set; }
    }
}
