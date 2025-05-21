using ConduitLLM.WebUI.Services;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Service for managing the Admin API client cache.
    /// </summary>
    public interface IAdminApiCacheService
    {
        /// <summary>
        /// Gets statistics about the Admin API client cache.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        CacheStatistics GetCacheStatistics();
        
        /// <summary>
        /// Clears all caches in the Admin API client.
        /// </summary>
        void ClearAllCaches();
        
        /// <summary>
        /// Gets whether caching is enabled for the Admin API client.
        /// </summary>
        /// <returns>True if caching is enabled, false otherwise.</returns>
        bool IsCachingEnabled();
    }
}