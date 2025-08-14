namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for global cache configuration
    /// </summary>
    public class CacheGlobalConfigDto
    {
        /// <summary>
        /// Default TTL in seconds
        /// </summary>
        public int DefaultTTL { get; set; }

        /// <summary>
        /// Maximum memory size
        /// </summary>
        public string MaxMemorySize { get; set; } = string.Empty;

        /// <summary>
        /// Global eviction policy
        /// </summary>
        public string EvictionPolicy { get; set; } = string.Empty;

        /// <summary>
        /// Whether compression is enabled
        /// </summary>
        public bool CompressionEnabled { get; set; }

        /// <summary>
        /// Redis connection string (always redacted for security)
        /// </summary>
        public string? RedisConnectionString { get; set; }
    }
}