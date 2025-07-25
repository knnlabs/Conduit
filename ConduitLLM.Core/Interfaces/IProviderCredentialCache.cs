using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Cache interface for Provider Credential operations with event-driven invalidation
    /// </summary>
    public interface IProviderCredentialCache
    {
        /// <summary>
        /// Get Provider Credential from cache with database fallback
        /// </summary>
        /// <param name="providerId">The provider ID to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Cached provider credential with all keys or null if not found</returns>
        Task<CachedProviderCredential?> GetProviderCredentialAsync(int providerId, Func<int, Task<CachedProviderCredential?>> databaseFallback);

        /// <summary>
        /// Get Provider Credential by name from cache with database fallback
        /// </summary>
        /// <param name="providerName">The provider name to look up</param>
        /// <param name="databaseFallback">Function to fetch from database on cache miss</param>
        /// <returns>Cached provider credential with all keys or null if not found</returns>
        Task<CachedProviderCredential?> GetProviderCredentialByNameAsync(string providerName, Func<string, Task<CachedProviderCredential?>> databaseFallback);

        /// <summary>
        /// Invalidate a Provider Credential in cache
        /// </summary>
        /// <param name="providerId">The provider ID to invalidate</param>
        Task InvalidateProviderAsync(int providerId);

        /// <summary>
        /// Invalidate a Provider Credential by name in cache
        /// </summary>
        /// <param name="providerName">The provider name to invalidate</param>
        Task InvalidateProviderByNameAsync(string providerName);

        /// <summary>
        /// Clear all Provider Credential entries from cache
        /// Used when bulk changes occur or during system reinitialization
        /// </summary>
        Task ClearAllProvidersAsync();

        /// <summary>
        /// Get cache performance statistics
        /// </summary>
        Task<ProviderCredentialCacheStats> GetStatsAsync();
    }

    /// <summary>
    /// Cache performance statistics for Provider Credentials
    /// </summary>
    public class ProviderCredentialCacheStats
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long InvalidationCount { get; set; }
        public double HitRate => HitCount + MissCount > 0 ? (double)HitCount / (HitCount + MissCount) : 0;
        public TimeSpan AverageGetTime { get; set; }
        public DateTime LastResetTime { get; set; }
        public long EntryCount { get; set; }
    }
}