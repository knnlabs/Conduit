namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache region information
    /// </summary>
    public class CacheRegionDto
    {
        /// <summary>
        /// Region identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Region display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Cache type (memory, redis, etc.)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Current status (healthy, unhealthy, idle)
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Number of nodes (for distributed cache)
        /// </summary>
        public int Nodes { get; set; }

        /// <summary>
        /// Region metrics
        /// </summary>
        public CacheMetricsDto Metrics { get; set; } = new();
    }
}