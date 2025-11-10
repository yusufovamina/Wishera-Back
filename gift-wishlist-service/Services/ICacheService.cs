using System;
using System.Threading.Tasks;

namespace gift_wishlist_service.Services
{
    public interface ICacheService
    {
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl);
        Task RemoveAsync(string key);
    }
}


