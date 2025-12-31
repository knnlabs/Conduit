using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Service for caching function discovery results (tool definitions)
/// </summary>
public interface IFunctionDiscoveryCacheService
{
    /// <summary>
    /// Gets cached tool definitions for a set of function configuration IDs
    /// </summary>
    /// <param name="functionConfigurationIds">List of function configuration IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached tool definitions or null if not found or caching is disabled</returns>
    Task<List<Tool>?> GetCachedToolsAsync(
        List<int> functionConfigurationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Caches tool definitions for a set of function configuration IDs
    /// </summary>
    /// <param name="functionConfigurationIds">List of function configuration IDs</param>
    /// <param name="tools">Tool definitions to cache</param>
    /// <param name="ttlMinutes">Optional TTL override in minutes (uses per-function TTL if not provided)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetCachedToolsAsync(
        List<int> functionConfigurationIds,
        List<Tool> tools,
        int? ttlMinutes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates all function discovery cache entries
    /// Called when function configurations are created, updated, or deleted
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateAllFunctionDiscoveryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates cache entries for a specific function configuration
    /// </summary>
    /// <param name="functionConfigurationId">Function configuration ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateFunctionConfigurationAsync(
        int functionConfigurationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if function discovery caching is enabled globally
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if caching is enabled, false otherwise</returns>
    Task<bool> IsCachingEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets function discovery cache statistics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cache statistics</returns>
    Task<FunctionDiscoveryCacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics for function discovery cache
/// </summary>
public class FunctionDiscoveryCacheStatistics
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
    /// Whether caching is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; }
}
