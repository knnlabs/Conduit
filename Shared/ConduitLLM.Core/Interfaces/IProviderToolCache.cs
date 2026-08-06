using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces;

/// <summary>
/// Cache interface for provider tool lookups used in the billing pipeline.
/// Caches active tools per provider type to avoid per-request database queries.
/// </summary>
public interface IProviderToolCache
{
    /// <summary>
    /// Gets all active tools for a provider, using cache with database fallback.
    /// </summary>
    /// <param name="providerType">The provider type to look up tools for</param>
    /// <param name="databaseFallback">Function to load from database on cache miss</param>
    /// <returns>List of active provider tools</returns>
    Task<List<ProviderTool>> GetActiveToolsForProviderAsync(
        ProviderType providerType,
        Func<ProviderType, Task<List<ProviderTool>>> databaseFallback);

    /// <summary>
    /// Invalidates all cached tools for a specific provider type.
    /// </summary>
    Task InvalidateProviderAsync(ProviderType providerType);

    /// <summary>
    /// Clears all cached provider tool entries.
    /// </summary>
    Task ClearAllAsync();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    Task<CacheStats> GetStatsAsync();
}
