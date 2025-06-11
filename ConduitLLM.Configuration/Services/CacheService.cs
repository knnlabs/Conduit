using System;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Generic caching service for the application
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly CacheOptions _cacheOptions;

        public CacheService(IMemoryCache memoryCache, IOptions<CacheOptions> cacheOptions)
        {
            _memoryCache = memoryCache;
            _cacheOptions = cacheOptions.Value;
        }

        /// <inheritdoc/>
        public T? Get<T>(string key)
        {
            _memoryCache.TryGetValue(key, out T? value);
            return value;
        }

        /// <inheritdoc/>
        public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            var options = new MemoryCacheEntryOptions();

            // Use provided expiration time or fall back to default from configuration
            var absExpiration = absoluteExpiration ?? _cacheOptions.DefaultAbsoluteExpiration;
            var slideExpiration = slidingExpiration ?? _cacheOptions.DefaultSlidingExpiration;

            if (absExpiration.HasValue)
                options.AbsoluteExpirationRelativeToNow = absExpiration.Value;

            if (slideExpiration.HasValue)
                options.SlidingExpiration = slideExpiration.Value;

            _memoryCache.Set(key, value, options);
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            _memoryCache.Remove(key);
        }

        /// <inheritdoc/>
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            // Check if the item exists in the cache
            if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
            {
                return cachedValue;
            }

            // Use semaphore to prevent multiple simultaneous initializations
            await _semaphore.WaitAsync();
            try
            {
                // Check again after acquiring the lock
                if (_memoryCache.TryGetValue(key, out T? lockCachedValue) && lockCachedValue != null)
                {
                    return lockCachedValue;
                }

                // Item is not in the cache, create it
                var newValue = await factory();

                // Set cache options
                var options = new MemoryCacheEntryOptions();

                // Use provided expiration time or fall back to default from configuration
                var absExpiration = absoluteExpiration ?? _cacheOptions.DefaultAbsoluteExpiration;
                var slideExpiration = slidingExpiration ?? _cacheOptions.DefaultSlidingExpiration;

                if (absExpiration.HasValue)
                    options.AbsoluteExpirationRelativeToNow = absExpiration.Value;

                if (slideExpiration.HasValue)
                    options.SlidingExpiration = slideExpiration.Value;

                // Save to cache and return
                _memoryCache.Set(key, newValue, options);
                return newValue;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void RemoveByPrefix(string prefix)
        {
            // Since MemoryCache doesn't support native prefix-based removal,
            // we would need a separate cache key tracking mechanism.
            // For simplicity, this is a basic implementation
            // In a production app, you might use a more sophisticated approach

            // Not implemented in this basic version
            // Would require tracking cache keys with a given prefix
        }
    }
}
