namespace ConduitLLM.Configuration.DTOs.Cache
{
    /// <summary>
    /// Internal metadata stored in GlobalSetting for LLM cache state.
    /// Serialized to JSON and stored in GlobalSetting.Value
    /// </summary>
    public class LLMCacheMetadata
    {
        /// <summary>
        /// Whether LLM caching is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// When the state was last changed
        /// </summary>
        public DateTime LastChangedAt { get; set; }

        /// <summary>
        /// Who last changed the state
        /// </summary>
        public string LastChangedBy { get; set; } = string.Empty;

        /// <summary>
        /// Reason for last change (audit trail)
        /// </summary>
        public string? LastChangeReason { get; set; }
    }
}
