using MongoDB.Bson;

namespace gift_wishlist_service.Services
{
    public static class ObjectIdValidator
    {
        public static bool IsValidObjectId(string id)
        {
            return ObjectId.TryParse(id, out _);
        }
    }
}
