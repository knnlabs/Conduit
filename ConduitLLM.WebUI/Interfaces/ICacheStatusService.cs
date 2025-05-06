using System.Threading.Tasks;

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
    }
    
    /// <summary>
    /// Represents the status and metrics of the LLM response cache
    /// </summary>
    /// <remarks>
    /// This class encapsulates information about the current state of the cache,
    /// including configuration settings and performance metrics. It is used to
    /// provide cache monitoring data to the UI and API consumers.
    /// </remarks>
    public class CacheStatus
    {
        /// <summary>
        /// Whether the LLM response cache is currently enabled
        /// </summary>
        /// <remarks>
        /// When true, identical or similar LLM requests may return cached responses.
        /// When false, all requests are sent directly to the LLM provider.
        /// </remarks>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// The type of cache being used (Memory, Redis, etc.)
        /// </summary>
        /// <remarks>
        /// Different cache types have different performance characteristics and persistence models:
        /// - Memory: Fast but limited by available RAM and lost on application restart
        /// - Redis: Potentially larger capacity and shared across instances but higher latency
        /// </remarks>
        public string CacheType { get; set; } = "Memory";
        
        /// <summary>
        /// The total number of items currently stored in the cache
        /// </summary>
        /// <remarks>
        /// This represents the number of unique request/response pairs that have been
        /// cached. Each item typically corresponds to a specific LLM prompt or request
        /// configuration.
        /// </remarks>
        public int TotalItems { get; set; }
        
        /// <summary>
        /// The cache hit rate (0.0 to 1.0) representing the proportion of requests served from cache
        /// </summary>
        /// <remarks>
        /// A higher hit rate indicates better cache efficiency. For example:
        /// - 0.0 means no requests are being served from cache
        /// - 0.5 means half of all requests are served from cache
        /// - 1.0 means all requests are served from cache
        /// 
        /// The hit rate is an important metric for evaluating cache effectiveness and
        /// potential cost savings from reduced LLM API calls.
        /// </remarks>
        public double HitRate { get; set; }
        
        /// <summary>
        /// The estimated memory usage of the cache in bytes
        /// </summary>
        /// <remarks>
        /// This is an approximation of how much memory the cached responses are consuming.
        /// For in-memory caches, this helps monitor resource usage. For Redis caches,
        /// this is a rougher estimate based on item count and average response size.
        /// </remarks>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// The average cache retrieval time in milliseconds
        /// </summary>
        /// <remarks>
        /// This measures how quickly responses are retrieved from the cache, which is typically
        /// much faster than getting responses from the LLM provider. This metric helps evaluate
        /// the performance benefits of caching in addition to the cost savings.
        /// </remarks>
        public double AvgResponseTime { get; set; }
    }
}