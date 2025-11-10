using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace user_service.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _cache;

        public CacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            var cached = await _cache.GetStringAsync(key);
            if (!string.IsNullOrEmpty(cached))
            {
                return JsonSerializer.Deserialize<T>(cached);
            }

            var value = await factory();
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            await _cache.SetStringAsync(key, json, options);
            return value;
        }

        public Task RemoveAsync(string key)
        {
            return _cache.RemoveAsync(key);
        }
    }
}


