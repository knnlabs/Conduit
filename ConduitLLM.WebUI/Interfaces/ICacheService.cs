using System.Threading.Tasks;

namespace ConduitLLM.WebUI.Interfaces
{
    /// <summary>
    /// Interface for the cache status service that provides information about LLM response caching
    /// </summary>
    public interface ICacheStatusService
    {
        /// <summary>
        /// Gets the current cache status
        /// </summary>
        /// <returns>The current cache status</returns>
        Task<CacheStatus> GetCacheStatusAsync();
        
        /// <summary>
        /// Enables or disables the cache
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task SetCacheEnabledAsync(bool enabled);
        
        /// <summary>
        /// Clears all items from the cache
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ClearCacheAsync();
    }
    
    /// <summary>
    /// Represents the status of the cache
    /// </summary>
    public class CacheStatus
    {
        /// <summary>
        /// Whether the cache is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// The type of cache being used (Memory, Redis, etc.)
        /// </summary>
        public string CacheType { get; set; } = "Memory";
        
        /// <summary>
        /// The total number of items in the cache
        /// </summary>
        public int TotalItems { get; set; }
        
        /// <summary>
        /// The cache hit rate (0.0 to 1.0)
        /// </summary>
        public double HitRate { get; set; }
        
        /// <summary>
        /// The memory usage of the cache in bytes
        /// </summary>
        public long MemoryUsageBytes { get; set; }
        
        /// <summary>
        /// The average response time in milliseconds
        /// </summary>
        public double AvgResponseTime { get; set; }
    }
}
