using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace WisheraApp.Models
{
    public class BirthdaySerializer : IBsonSerializer<string?>
    {
        public Type ValueType => typeof(string);

        public string? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            var bsonType = context.Reader.GetCurrentBsonType();
            
            switch (bsonType)
            {
                case BsonType.String:
                    return context.Reader.ReadString();
                case BsonType.DateTime:
                    var dateTime = context.Reader.ReadDateTime();
                    return dateTime.ToString("yyyy-MM-dd");
                case BsonType.Null:
                    context.Reader.ReadNull();
                    return null;
                default:
                    throw new FormatException($"Cannot deserialize a {bsonType} to a string.");
            }
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string? value)
        {
            if (value == null)
            {
                context.Writer.WriteNull();
            }
            else
            {
                context.Writer.WriteString(value);
            }
        }

        object IBsonSerializer.Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            return Deserialize(context, args) ?? string.Empty;
        }

        public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
        {
            Serialize(context, args, (string?)value);
        }
    }
}
