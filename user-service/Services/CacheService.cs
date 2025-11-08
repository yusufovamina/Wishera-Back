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

        private static bool _redisAvailable = true;
        private static DateTime _lastRedisFailure = DateTime.MinValue;
        private static readonly TimeSpan _redisCircuitBreakerTimeout = TimeSpan.FromMinutes(5); // Skip Redis for 5 minutes after failure
        private static int _consecutiveFailures = 0;
        private static readonly int _maxFailuresBeforeCircuitBreak = 2;

        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
        {
            // Circuit breaker: If Redis has failed recently, skip it entirely
            if (!_redisAvailable)
            {
                var timeSinceLastFailure = DateTime.UtcNow - _lastRedisFailure;
                if (timeSinceLastFailure < _redisCircuitBreakerTimeout)
                {
                    // Skip cache completely, go directly to factory
                    return await factory();
                }
                else
                {
                    // Enough time has passed, reset circuit breaker and try again
                    _redisAvailable = true;
                    _consecutiveFailures = 0;
                }
            }

            // Try to get from cache with very short timeout (50ms max wait)
            // Use Task.Run with timeout to ensure we don't wait longer than 50ms
            var cacheTask = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                    return await _cache.GetStringAsync(key, cts.Token);
                }
                catch
                {
                    return null; // Return null on any error
                }
            });

            // Wait for cache with timeout - don't wait more than 50ms
            string? cached = null;
            try
            {
                var completedTask = await Task.WhenAny(cacheTask, Task.Delay(50));
                if (completedTask == cacheTask)
                {
                    cached = await cacheTask;
                }
            }
            catch
            {
                // Ignore any errors
            }

            // If we got a cached value, use it
            if (!string.IsNullOrEmpty(cached))
            {
                try
                {
                    var deserialized = JsonSerializer.Deserialize<T>(cached);
                    if (deserialized != null)
                    {
                        // Redis is working - reset failure counter
                        _redisAvailable = true;
                        _consecutiveFailures = 0;
                        return deserialized;
                    }
                }
                catch
                {
                    // Deserialization failed, continue to factory
                }
            }

            // Check if cache operation failed (timeout or error)
            // If cache didn't return quickly, mark as failure
            if (cached == null && _redisAvailable)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures >= _maxFailuresBeforeCircuitBreak)
                {
                    _redisAvailable = false;
                    _lastRedisFailure = DateTime.UtcNow;
                    _consecutiveFailures = 0;
                }
            }
            else if (cached != null)
            {
                // Cache returned (even if empty), reset failures
                _consecutiveFailures = 0;
            }

            // Execute factory to get the value (this is the important part)
            // Always execute factory, even if cache failed
            var value = await factory();

            // Try to cache the value asynchronously (fire and forget - don't wait for it)
            // Only try if Redis circuit breaker is open (available)
            if (_redisAvailable)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
                        var json = JsonSerializer.Serialize(value);
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = ttl
                        };
                        await _cache.SetStringAsync(key, json, options, writeCts.Token);
                        // If write succeeds, reset failures
                        _consecutiveFailures = 0;
                    }
                    catch
                    {
                        // Silently ignore cache write failures
                        // Increment failure counter
                        _consecutiveFailures++;
                        if (_consecutiveFailures >= _maxFailuresBeforeCircuitBreak)
                        {
                            _redisAvailable = false;
                            _lastRedisFailure = DateTime.UtcNow;
                            _consecutiveFailures = 0;
                        }
                    }
                });
            }

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


