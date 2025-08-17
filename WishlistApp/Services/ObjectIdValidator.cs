using System.Text.RegularExpressions;

namespace WishlistApp.Services
{
    public static class ObjectIdValidator
    {
        public static void ValidateObjectId(string objectId, string parameterName)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                throw new ArgumentException($"{parameterName} cannot be null or empty.");
            }
            
            if (objectId.Length != 24)
            {
                throw new ArgumentException($"{parameterName} must be exactly 24 characters long. Received: '{objectId}' (length: {objectId.Length})");
            }
            
            if (!Regex.IsMatch(objectId, "^[0-9a-fA-F]{24}$"))
            {
                throw new ArgumentException($"{parameterName} must be a valid 24-character hexadecimal string. Received: '{objectId}'");
            }
        }
    }
} 