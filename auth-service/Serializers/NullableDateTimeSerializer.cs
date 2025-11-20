using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace auth_service.Serializers
{
    /// <summary>
    /// Custom serializer for nullable DateTime that handles empty strings by converting them to null.
    /// This fixes deserialization errors when MongoDB contains empty strings instead of null values.
    /// </summary>
    public class NullableDateTimeSerializer : SerializerBase<DateTime?>
    {
        private static readonly DateTimeSerializer _dateTimeSerializer = new DateTimeSerializer();

        public override DateTime? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonType = context.Reader.GetCurrentBsonType();
            
            // Handle null or missing values
            if (bsonType == BsonType.Null)
            {
                context.Reader.ReadNull();
                return null;
            }
            
            // Handle empty strings by returning null
            if (bsonType == BsonType.String)
            {
                var stringValue = context.Reader.ReadString();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    return null;
                }
                // Try to parse the string as DateTime
                if (DateTime.TryParse(stringValue, out var dateTime))
                {
                    return dateTime;
                }
                // If parsing fails, return null instead of throwing
                return null;
            }
            
            // For other types (DateTime, Int64, etc.), use the default DateTime serializer
            try
            {
                var dateTime = _dateTimeSerializer.Deserialize(context, args);
                return dateTime;
            }
            catch
            {
                // If deserialization fails, return null instead of throwing
                return null;
            }
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateTime? value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                _dateTimeSerializer.Serialize(context, args, value.Value);
            }
        }
    }
}

