namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Event published when LLM caching is enabled or disabled at runtime.
    /// All ConduitLLM.Http instances consume this to update their caching behavior.
    /// </summary>
    public class LLMCacheToggleEvent
    {
        /// <summary>
        /// Whether LLM caching should be enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Who toggled the cache (admin username/system)
        /// </summary>
        public string ToggledBy { get; set; } = string.Empty;

        /// <summary>
        /// When the toggle occurred
        /// </summary>
        public DateTime ToggledAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional reason for the toggle
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Apply immediately (always true for this use case)
        /// </summary>
        public bool ApplyImmediately { get; set; } = true;
    }
}
