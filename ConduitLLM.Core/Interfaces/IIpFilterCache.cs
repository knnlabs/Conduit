using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for IP Filter operations with event-driven invalidation
    /// </summary>
    public interface IIpFilterCache
    {
        /// <summary>
        /// Get all global IP filters from cache with database fallback
        /// Global filters apply to all API requests regardless of virtual key
        /// </summary>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>List of global IP filter entities</returns>
        Task<List<IpFilterEntity>> GetGlobalFiltersAsync(Func<Task<List<IpFilterEntity>>> databaseFallback);

        /// <summary>
        /// Get IP filters for a specific virtual key from cache with database fallback
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to get filters for</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>List of IP filter entities for the virtual key</returns>
        Task<List<IpFilterEntity>> GetVirtualKeyFiltersAsync(int virtualKeyId, Func<int, Task<List<IpFilterEntity>>> databaseFallback);

        /// <summary>
        /// Check if an IP address is allowed for a virtual key (considering both global and key-specific filters)
        /// </summary>
        /// <param name="ipAddress">The IP address to check</param>
        /// <param name="virtualKeyId">The virtual key ID to check for (null for global check only)</param>
        /// <param name="databaseFallback">Function to fetch filters from database on cache miss</param>
        /// <returns>True if the IP is allowed, false otherwise</returns>
        Task<bool> IsIpAllowedAsync(string ipAddress, int? virtualKeyId, Func<string, int?, Task<bool>> databaseFallback);

        /// <summary>
        /// Invalidate a specific IP filter in cache
        /// </summary>
        /// <param name="filterId">The filter ID to invalidate</param>
        Task InvalidateFilterAsync(int filterId);

        /// <summary>
        /// Invalidate all global IP filters in cache
        /// </summary>
        Task InvalidateGlobalFiltersAsync();

        /// <summary>
        /// Invalidate all IP filters for a specific virtual key
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to invalidate filters for</param>
        Task InvalidateVirtualKeyFiltersAsync(int virtualKeyId);

        /// <summary>
        /// Clear all IP filter entries from cache
        /// Used when bulk changes occur or during system reinitialization
        /// </summary>
        Task ClearAllFiltersAsync();

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        Task<IpFilterCacheStats> GetStatsAsync();
    }

    /// <summary>
    /// Cache performance statistics for IP Filters
    /// </summary>
    public class IpFilterCacheStats
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long InvalidationCount { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public DateTime LastResetTime { get; set; }
        public long EntryCount { get; set; }
        public long IpCheckCount { get; set; }
        public long GlobalFilterCount { get; set; }
        public long KeySpecificFilterCount { get; set; }
    }
}