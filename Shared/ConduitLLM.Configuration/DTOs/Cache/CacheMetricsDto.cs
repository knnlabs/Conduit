namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache metrics
    /// </summary>
    public class CacheMetricsDto
    {
        /// <summary>
        /// Current size (formatted string)
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Number of items in cache
        /// </summary>
        public long Items { get; set; }

        /// <summary>
        /// Cache hit rate percentage
        /// </summary>
        public double HitRate { get; set; }

        /// <summary>
        /// Cache miss rate percentage
        /// </summary>
        public double MissRate { get; set; }

        /// <summary>
        /// Eviction rate percentage
        /// </summary>
        public double EvictionRate { get; set; }
    }
}