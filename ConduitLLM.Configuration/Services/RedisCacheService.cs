using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Redis implementation of the cache service
    /// </summary>
    /// <remarks>
    /// This implementation uses StackExchange.Redis and IDistributedCache for Redis caching.
    /// It provides thread-safe operations and implements all ICacheService methods.
    /// </remarks>
    public class RedisCacheService : ICacheService, IDisposable
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _redisDatabase;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly CacheOptions _cacheOptions;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Creates a new instance of RedisCacheService
        /// </summary>
        /// <param name="distributedCache">The distributed cache implementation</param>
        /// <param name="connectionMultiplexer">The Redis connection multiplexer</param>
        /// <param name="cacheOptions">Cache configuration options</param>
        /// <param name="logger">Logger instance</param>
        public RedisCacheService(
            IDistributedCache distributedCache,
            IConnectionMultiplexer connectionMultiplexer,
            IOptions<CacheOptions> cacheOptions,
            ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            _redisDatabase = _connectionMultiplexer.GetDatabase();
            _cacheOptions = cacheOptions?.Value ?? throw new ArgumentNullException(nameof(cacheOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public T? Get<T>(string key)
        {
            try
            {
                var value = _distributedCache.GetString(key);
                if (string.IsNullOrEmpty(value))
                {
                    return default;
                }

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item from Redis cache with key {Key}", key);
                return default;
            }
        }

        /// <inheritdoc/>
        public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            try
            {
                var options = new DistributedCacheEntryOptions();
                
                // Use provided expiration time or fall back to default from configuration
                var absExpiration = absoluteExpiration ?? _cacheOptions.DefaultAbsoluteExpiration;
                var slideExpiration = slidingExpiration ?? _cacheOptions.DefaultSlidingExpiration;
                
                if (absExpiration.HasValue)
                    options.AbsoluteExpirationRelativeToNow = absExpiration.Value;
                    
                if (slideExpiration.HasValue)
                    options.SlidingExpiration = slideExpiration.Value;

                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                _distributedCache.SetString(key, serializedValue, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting item in Redis cache with key {Key}", key);
            }
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            try
            {
                _distributedCache.Remove(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from Redis cache with key {Key}", key);
            }
        }

        /// <inheritdoc/>
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            // Check if the item exists in the cache
            var cachedValue = Get<T>(key);
            if (cachedValue != null)
            {
                return cachedValue;
            }

            // Use semaphore to prevent multiple simultaneous initializations
            await _semaphore.WaitAsync();
            try
            {
                // Check again after acquiring the lock
                cachedValue = Get<T>(key);
                if (cachedValue != null)
                {
                    return cachedValue;
                }

                // Item is not in the cache, create it
                var newValue = await factory();

                // Save to cache and return
                Set(key, newValue, absoluteExpiration, slidingExpiration);
                return newValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateAsync for key {Key}", key);
                // If there's an error, try to execute the factory directly
                try
                {
                    return await factory();
                }
                catch
                {
                    // If the factory also fails, re-throw the original exception
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void RemoveByPrefix(string prefix)
        {
            try
            {
                // Redis supports pattern matching for key deletion
                // This will find all keys that match the specified pattern
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: $"{prefix}*");
                
                foreach (var key in keys)
                {
                    _redisDatabase.KeyDelete(key);
                }
                
                _logger.LogInformation("Removed all items with prefix {Prefix} from Redis cache", prefix);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing items by prefix {Prefix} from Redis cache", prefix);
            }
        }

        /// <summary>
        /// Disposes of resources used by the service
        /// </summary>
        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}