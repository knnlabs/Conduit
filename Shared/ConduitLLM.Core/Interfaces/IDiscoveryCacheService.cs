using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for caching discovery endpoint results
    /// </summary>
    public interface IDiscoveryCacheService
    {
        /// <summary>
        /// Gets cached discovery results for models
        /// </summary>
        /// <param name="cacheKey">Cache key for the results</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cached discovery results or null if not found</returns>
        Task<DiscoveryModelsResult?> GetDiscoveryResultsAsync(string cacheKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets discovery results in cache
        /// </summary>
        /// <param name="cacheKey">Cache key for the results</param>
        /// <param name="results">Discovery results to cache</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task SetDiscoveryResultsAsync(string cacheKey, DiscoveryModelsResult results, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates all discovery cache entries
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidateAllDiscoveryAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates discovery cache entries matching a pattern
        /// </summary>
        /// <param name="pattern">Pattern to match (e.g., "discovery:models:*")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidatePatternAsync(string pattern, CancellationToken cancellationToken = default);

        /// <summary>
        /// Warms the discovery cache with common queries
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task WarmDiscoveryCacheAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets discovery cache statistics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Cache statistics</returns>
        Task<DiscoveryCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Represents cached discovery models result
    /// </summary>
    public class DiscoveryModelsResult
    {
        /// <summary>
        /// List of discovered models
        /// </summary>
        public List<object> Data { get; set; } = new();

        /// <summary>
        /// Total count of models
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// When this result was cached
        /// </summary>
        public DateTime CachedAt { get; set; }

        /// <summary>
        /// Optional capability filter that was applied
        /// </summary>
        public string? CapabilityFilter { get; set; }
    }

    /// <summary>
    /// Statistics for discovery cache
    /// </summary>
    public class DiscoveryCacheStatistics
    {
        /// <summary>
        /// Total number of cache hits
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Total number of cache misses
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Cache hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Number of cached entries
        /// </summary>
        public int CachedEntries { get; set; }

        /// <summary>
        /// Last cache invalidation time
        /// </summary>
        public DateTime? LastInvalidation { get; set; }

        /// <summary>
        /// Last cache warming time
        /// </summary>
        public DateTime? LastWarmingTime { get; set; }
    }
}