namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Data transfer object for cache policy information
    /// </summary>
    public class CachePolicyDto
    {
        /// <summary>
        /// Policy identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Policy name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Policy type (memory, distributed, etc.)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Time-to-live in seconds
        /// </summary>
        public int TTL { get; set; }

        /// <summary>
        /// Maximum size (items or bytes)
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// Eviction strategy (LRU, LFU, etc.)
        /// </summary>
        public string Strategy { get; set; } = string.Empty;

        /// <summary>
        /// Whether the policy is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Policy description
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}