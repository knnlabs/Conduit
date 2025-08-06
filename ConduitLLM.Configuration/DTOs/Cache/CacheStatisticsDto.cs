using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache statistics
    /// </summary>
    public class CacheStatisticsDto
    {
        /// <summary>
        /// Total cache hits
        /// </summary>
        public long TotalHits { get; set; }

        /// <summary>
        /// Total cache misses
        /// </summary>
        public long TotalMisses { get; set; }

        /// <summary>
        /// Overall hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Average response times
        /// </summary>
        public ResponseTimeDto AvgResponseTime { get; set; } = new();

        /// <summary>
        /// Memory usage information
        /// </summary>
        public MemoryUsageDto MemoryUsage { get; set; } = new();

        /// <summary>
        /// Top cached items by hit count
        /// </summary>
        public List<TopCachedItemDto> TopCachedItems { get; set; } = new();
    }
}