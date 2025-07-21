using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for embedding responses to improve performance and reduce costs.
    /// </summary>
    /// <remarks>
    /// Embeddings are deterministic for identical inputs, making them ideal for caching.
    /// This cache helps reduce API calls and improves response times for repeated queries.
    /// </remarks>
    public interface IEmbeddingCache
    {
        /// <summary>
        /// Retrieves a cached embedding response for the given input.
        /// </summary>
        /// <param name="cacheKey">The cache key derived from the input and model parameters.</param>
        /// <returns>The cached embedding response, or null if not found.</returns>
        Task<EmbeddingResponse?> GetEmbeddingAsync(string cacheKey);

        /// <summary>
        /// Stores an embedding response in the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key derived from the input and model parameters.</param>
        /// <param name="response">The embedding response to cache.</param>
        /// <param name="ttl">Time-to-live for the cached entry. If null, uses default TTL.</param>
        Task SetEmbeddingAsync(string cacheKey, EmbeddingResponse response, TimeSpan? ttl = null);

        /// <summary>
        /// Generates a cache key for the given embedding request.
        /// </summary>
        /// <param name="request">The embedding request to generate a key for.</param>
        /// <returns>A unique cache key for the request.</returns>
        string GenerateCacheKey(EmbeddingRequest request);

        /// <summary>
        /// Invalidates cached embeddings for a specific model.
        /// </summary>
        /// <param name="modelName">The model name to invalidate cache entries for.</param>
        Task InvalidateModelCacheAsync(string modelName);

        /// <summary>
        /// Bulk invalidates multiple cache entries.
        /// </summary>
        /// <param name="cacheKeys">The cache keys to invalidate.</param>
        Task InvalidateBulkAsync(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Gets cache performance statistics.
        /// </summary>
        /// <returns>Cache performance metrics.</returns>
        Task<EmbeddingCacheStats> GetStatsAsync();

        /// <summary>
        /// Checks if caching is enabled and available.
        /// </summary>
        /// <returns>True if caching is available, false otherwise.</returns>
        bool IsAvailable { get; }

        /// <summary>
        /// Clears all cached embeddings (use with caution).
        /// </summary>
        Task ClearAllAsync();
    }

    /// <summary>
    /// Cache performance statistics for embedding cache.
    /// </summary>
    public class EmbeddingCacheStats
    {
        /// <summary>
        /// Number of cache hits.
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Number of cache misses.
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Number of cache invalidations performed.
        /// </summary>
        public long InvalidationCount { get; set; }

        /// <summary>
        /// Number of entries currently in cache.
        /// </summary>
        public long EntryCount { get; set; }

        /// <summary>
        /// Total size of cached data in bytes.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Cache hit rate as a percentage.
        /// </summary>
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;

        /// <summary>
        /// Average time to retrieve from cache.
        /// </summary>
        public TimeSpan AverageGetTime { get; set; }

        /// <summary>
        /// Average time to store in cache.
        /// </summary>
        public TimeSpan AverageSetTime { get; set; }

        /// <summary>
        /// When statistics were last reset.
        /// </summary>
        public DateTime LastResetTime { get; set; }

        /// <summary>
        /// Estimated cost savings from cache hits (based on model pricing).
        /// </summary>
        public decimal EstimatedCostSavings { get; set; }
    }
}