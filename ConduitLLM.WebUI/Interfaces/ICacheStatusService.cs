using System.Threading.Tasks;

using ConduitLLM.WebUI.Models;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for the cache status service that provides information and management functionality for LLM response caching
    /// </summary>
    /// <remarks>
    /// This interface defines methods for:
    /// - Retrieving current cache status and metrics
    /// - Enabling or disabling the cache
    /// - Clearing all items from the cache
    /// - Managing Redis-specific cache settings
    /// 
    /// Implementations of this interface are responsible for tracking cache performance,
    /// persisting cache configuration, and providing cache management operations.
    /// </remarks>
    public interface ICacheStatusService
    {
        /// <summary>
        /// Gets the current cache status and performance metrics
        /// </summary>
        /// <returns>A CacheStatus object containing information about the cache configuration and usage metrics</returns>
        /// <remarks>
        /// This method retrieves current metrics about the cache including:
        /// - Whether the cache is enabled
        /// - The type of cache (Memory, Redis, etc.)
        /// - The number of items in the cache
        /// - The cache hit rate
        /// - Memory usage estimates
        /// - Average response time
        /// - Redis-specific metrics when using Redis cache
        /// 
        /// This information is useful for monitoring cache performance and making decisions
        /// about cache configuration adjustments.
        /// </remarks>
        Task<CacheStatus> GetCacheStatusAsync();

        /// <summary>
        /// Enables or disables the LLM response cache
        /// </summary>
        /// <param name="enabled">True to enable the cache, false to disable it</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// When enabled, the LLM service will attempt to cache responses for identical or similar requests
        /// according to configured rules. When disabled, every request will be sent to the LLM provider
        /// regardless of whether a cached response exists.
        /// 
        /// The cache state persists between application restarts as it is saved to the database.
        /// </remarks>
        Task SetCacheEnabledAsync(bool enabled);

        /// <summary>
        /// Clears all items from the LLM response cache
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method removes all cached LLM responses from the cache, forcing subsequent
        /// requests to be processed by the LLM provider. It also resets the cache metrics.
        /// 
        /// This is useful when:
        /// - Cache has become stale and needs to be refreshed
        /// - Cache is consuming too much memory
        /// - Testing responses from the LLM provider directly
        /// - After updating model configuration or credentials
        /// </remarks>
        Task ClearCacheAsync();

        /// <summary>
        /// Updates the cache type (Memory or Redis)
        /// </summary>
        /// <param name="cacheType">The type of cache to use</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method changes the type of cache being used. When changing from memory
        /// to Redis or vice versa, the cache is cleared and reconfigured.
        /// </remarks>
        Task SetCacheTypeAsync(string cacheType);

        /// <summary>
        /// Updates the Redis connection settings
        /// </summary>
        /// <param name="connectionString">The Redis connection string</param>
        /// <param name="instanceName">The Redis instance name</param>
        /// <returns>A task representing the asynchronous operation</returns>
        /// <remarks>
        /// This method updates the Redis connection settings. If the cache type is
        /// Redis, the connection will be re-established with the new settings.
        /// </remarks>
        Task UpdateRedisSettingsAsync(string connectionString, string instanceName);

        /// <summary>
        /// Tests the Redis connection with the specified settings
        /// </summary>
        /// <param name="connectionString">The Redis connection string to test</param>
        /// <returns>A result object indicating success or failure with error details</returns>
        /// <remarks>
        /// This method tests the Redis connection with the specified settings without
        /// changing the current cache configuration. It's useful for validating
        /// connection settings before applying them.
        /// </remarks>
        Task<RedisConnectionTestResult> TestRedisConnectionAsync(string connectionString);

        /// <summary>
        /// Gets the full cache configuration from the backend
        /// </summary>
        /// <returns>The cache configuration object or null if not configured</returns>
        /// <remarks>
        /// This method retrieves the complete cache configuration from the Admin API,
        /// which is the authoritative source for all cache settings and defaults.
        /// </remarks>
        Task<CacheConfiguration?> GetCacheConfigurationAsync();
    }

    /// <summary>
    /// Results of a Redis connection test
    /// </summary>
    public class RedisConnectionTestResult
    {
        /// <summary>
        /// Whether the connection test was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Error message if the connection test failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Redis server version if connection was successful
        /// </summary>
        public string? ServerVersion { get; set; }

        /// <summary>
        /// Latency of the connection test in milliseconds
        /// </summary>
        public double LatencyMs { get; set; }
    }
}
