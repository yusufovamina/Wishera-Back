using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace user_service.Services
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
            // Use cancellation token with very short timeout (200ms) to fail fast
            // If cache is slow, skip it and go directly to factory
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
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
                // Cache read timed out, skip cache and go directly to factory
                // Don't log every timeout to avoid log spam
            }
            catch (Exception ex)
            {
                // If cache read fails, skip cache and continue to factory
                // Only log if it's not a timeout/connection issue
                if (!(ex is TimeoutException || ex.Message.Contains("timeout") || ex.Message.Contains("UnableToConnect")))
                {
                    _logger.LogWarning(ex, "Failed to read from cache for key: {Key}", key);
                }
            }

            // Execute factory to get the value (this is the important part)
            var value = await factory();

            // Try to cache the value asynchronously (fire and forget - don't wait for it)
            // Use Task.Run to avoid blocking the response
            _ = Task.Run(async () =>
            {
                try
                {
                    using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                    var json = JsonSerializer.Serialize(value);
                    var options = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = ttl
                    };
                    await _cache.SetStringAsync(key, json, options, writeCts.Token);
                }
                catch
                {
                    // Silently ignore cache write failures - caching is optional
                }
            });

            return value;
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


