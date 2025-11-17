using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for updating cache configuration
    /// </summary>
    public class UpdateCacheConfigDto
    {
        /// <summary>
        /// Default TTL in seconds
        /// </summary>
        [Range(0, 86400, ErrorMessage = "TTL must be between 0 and 86400 seconds (24 hours)")]
        public int? DefaultTTLSeconds { get; set; }

        /// <summary>
        /// Maximum memory size
        /// </summary>
        [RegularExpression(@"^\d+(\.\d+)?\s*(B|KB|MB|GB|TB)$", ErrorMessage = "Invalid memory size format")]
        public string? MaxMemorySize { get; set; }

        /// <summary>
        /// Eviction policy
        /// </summary>
        [RegularExpression("^(LRU|LFU|FIFO|Random|Priority|TTL)$", ErrorMessage = "Invalid eviction policy")]
        public string? EvictionPolicy { get; set; }

        /// <summary>
        /// Enable or disable compression
        /// </summary>
        public bool EnableCompression { get; set; }

        /// <summary>
        /// Clear affected caches after update
        /// </summary>
        public bool ClearAffectedCaches { get; set; }

        /// <summary>
        /// Apply configuration globally to all regions
        /// </summary>
        public bool ApplyGlobally { get; set; }

        /// <summary>
        /// Specific region ID to update (if not applying globally)
        /// </summary>
        public string? RegionId { get; set; }
    }
}