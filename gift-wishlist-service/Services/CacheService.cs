using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace gift_wishlist_service.Services
{
    public class CacheService : ICacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<CacheService> _logger;

        public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            // Use cancellation token with short timeout (500ms) to fail fast
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            try
            {
                var cached = await _cache.GetStringAsync(key, cts.Token);
                if (!string.IsNullOrEmpty(cached))
                {
                    var deserialized = JsonSerializer.Deserialize<T>(cached);
                    if (deserialized != null)
                    {
                        return deserialized;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cache read timed out, log and continue to factory
                _logger.LogWarning("Cache read timed out for key: {Key}", key);
            }
            catch (Exception ex)
            {
                // If cache read fails, log and continue to factory
                _logger.LogWarning(ex, "Failed to read from cache for key: {Key}", key);
            }

            // Execute factory to get the value
            try
            {
                var value = await factory();

                // Try to cache the value (fire and forget with short timeout)
                try
                {
                    using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                    var json = JsonSerializer.Serialize(value);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl
                    };
                    await _cache.SetStringAsync(key, json, options, writeCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Cache write timed out, log but don't fail the request
                    _logger.LogWarning("Cache write timed out for key: {Key}", key);
                }
                catch (Exception ex)
                {
                    // If cache write fails, log but don't fail the request
                    _logger.LogWarning(ex, "Failed to write to cache for key: {Key}", key);
                }

                return value;
            }
            catch (Exception ex)
            {
                // If factory fails, log and rethrow so the caller can handle it
                _logger.LogError(ex, "Factory method failed for cache key: {Key}", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                // Use cancellation token with short timeout (500ms) to fail fast
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await _cache.RemoveAsync(key, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Cache removal timed out, log but don't fail
                _logger.LogWarning("Cache removal timed out for key: {Key}", key);
            }
            catch (Exception ex)
            {
                // If cache removal fails, log but don't fail
                _logger.LogWarning(ex, "Failed to remove from cache for key: {Key}", key);
            }
        }
    }
}


