namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// DTO for LLM cache control status
    /// </summary>
    public class LLMCacheControlDto
    {
        /// <summary>
        /// Current state of LLM caching
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// When the state was last changed
        /// </summary>
        public DateTime? LastChangedAt { get; set; }

        /// <summary>
        /// Who last changed the state
        /// </summary>
        public string? LastChangedBy { get; set; }

        /// <summary>
        /// Reason for last change
        /// </summary>
        public string? LastChangeReason { get; set; }

        /// <summary>
        /// Number of active ConduitLLM.Gateway instances (if available)
        /// </summary>
        public int? ActiveInstances { get; set; }
    }

    /// <summary>
    /// Request to toggle LLM caching
    /// </summary>
    public class ToggleLLMCacheRequest
    {
        /// <summary>
        /// Enable or disable LLM caching
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Reason for the change (audit trail)
        /// </summary>
        public string? Reason { get; set; }
    }
}
