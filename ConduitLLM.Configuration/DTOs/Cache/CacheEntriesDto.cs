using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache entries listing
    /// </summary>
    public class CacheEntriesDto
    {
        /// <summary>
        /// Region identifier
        /// </summary>
        public string RegionId { get; set; } = string.Empty;

        /// <summary>
        /// List of cache entries
        /// </summary>
        public List<CacheEntryDto> Entries { get; set; } = new();

        /// <summary>
        /// Total number of entries in the region
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of entries skipped
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// Number of entries returned
        /// </summary>
        public int Take { get; set; }

        /// <summary>
        /// Optional message (e.g., for restricted regions)
        /// </summary>
        public string? Message { get; set; }
    }
}