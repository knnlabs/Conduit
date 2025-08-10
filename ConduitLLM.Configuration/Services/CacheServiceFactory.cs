using System;

using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Options;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace ConduitLLM.Configuration.Services
{
    /// <summary>
    /// Factory that creates the appropriate cache service based on configuration
    /// </summary>
    /// <remarks>
    /// This factory creates either a memory-based or Redis-based cache service
    /// depending on the configuration. It encapsulates the creation logic and dependency
    /// injection for the different cache implementations.
    /// </remarks>
    public class CacheServiceFactory
    {
        private readonly IOptions<CacheOptions> _cacheOptions;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private readonly ILoggerFactory _loggerFactory;
        private readonly RedisConnectionFactory _redisConnectionFactory;

        /// <summary>
        /// Creates a new instance of CacheServiceFactory
        /// </summary>
        /// <param name="cacheOptions">Cache configuration options</param>
        /// <param name="memoryCache">Memory cache instance</param>
        /// <param name="distributedCache">Distributed cache instance</param>
        /// <param name="loggerFactory">Logger factory</param>
        /// <param name="redisConnectionFactory">Redis connection factory</param>
        public CacheServiceFactory(
            IOptions<CacheOptions> cacheOptions,
            IMemoryCache memoryCache,
            IDistributedCache distributedCache,
            ILoggerFactory loggerFactory,
            RedisConnectionFactory redisConnectionFactory)
        {
            _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _redisConnectionFactory = redisConnectionFactory ?? throw new ArgumentNullException(nameof(redisConnectionFactory));
        }

        /// <summary>
        /// Creates the appropriate cache service based on configuration
        /// </summary>
        /// <returns>An implementation of ICacheService</returns>
        public async Task<ICacheService> CreateCacheServiceAsync()
        {
            var options = _cacheOptions.Value;

            // If cache is disabled, return a null cache implementation
            if (!options.IsEnabled)
            {
                return new NullCacheService(_loggerFactory.CreateLogger<NullCacheService>());
            }

            // Based on the cache type, create the appropriate service
            switch (options.CacheType?.ToLowerInvariant())
            {
                case "redis":
                    try
                    {
                        var connection = await _redisConnectionFactory.GetConnectionAsync();
                        return new RedisCacheService(
                            _distributedCache,
                            connection,
                            _cacheOptions,
                            _loggerFactory.CreateLogger<RedisCacheService>());
                    }
                    catch (Exception ex)
                    {
                        // Log the error and fall back to memory cache
                        var logger = _loggerFactory.CreateLogger<CacheServiceFactory>();
                        logger.LogError(ex, "Failed to create Redis cache service, falling back to memory cache");
                        return CreateMemoryCacheService();
                    }

                case "memory":
                default:
                    return CreateMemoryCacheService();
            }
        }

        private CacheService CreateMemoryCacheService()
        {
            return new CacheService(
                _memoryCache,
                _cacheOptions);
        }
    }

    /// <summary>
    /// A no-op implementation of ICacheService that doesn't cache anything
    /// </summary>
    /// <remarks>
    /// This implementation is used when caching is disabled.
    /// All operations are no-ops and simply pass through to the factory method.
    /// </remarks>
    public class NullCacheService : ICacheService
    {
        private readonly ILogger<NullCacheService> _logger;

        /// <summary>
        /// Creates a new instance of NullCacheService
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public NullCacheService(ILogger<NullCacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("Using null cache service - caching is disabled");
        }

        /// <inheritdoc/>
        public T? Get<T>(string key)
        {
            return default;
        }

        /// <inheritdoc/>
        public async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            return await factory();
        }

        /// <inheritdoc/>
        public void Remove(string key)
        {
            // No-op
        }

        /// <inheritdoc/>
        public void RemoveByPrefix(string prefix)
        {
            // No-op
        }

        /// <inheritdoc/>
        public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null, TimeSpan? slidingExpiration = null)
        {
            // No-op
        }
    }
}
