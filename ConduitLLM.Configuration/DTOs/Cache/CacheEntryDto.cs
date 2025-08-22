namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for individual cache entry information
    /// </summary>
    public class CacheEntryDto
    {
        /// <summary>
        /// Cache key
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Entry size (formatted string)
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Last access timestamp
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Expiration timestamp
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Number of times accessed
        /// </summary>
        public long AccessCount { get; set; }

        /// <summary>
        /// Entry priority
        /// </summary>
        public int Priority { get; set; }
    }
}