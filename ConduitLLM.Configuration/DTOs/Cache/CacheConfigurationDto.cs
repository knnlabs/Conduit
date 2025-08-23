namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache configuration response
    /// </summary>
    public class CacheConfigurationDto
    {
        /// <summary>
        /// Response timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// List of cache policies
        /// </summary>
        public List<CachePolicyDto> CachePolicies { get; set; } = new();

        /// <summary>
        /// List of cache regions
        /// </summary>
        public List<CacheRegionDto> CacheRegions { get; set; } = new();

        /// <summary>
        /// Overall cache statistics
        /// </summary>
        public CacheStatisticsDto Statistics { get; set; } = new();

        /// <summary>
        /// Global cache configuration
        /// </summary>
        public CacheGlobalConfigDto Configuration { get; set; } = new();
    }
}