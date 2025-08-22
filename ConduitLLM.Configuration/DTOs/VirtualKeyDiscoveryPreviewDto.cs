namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Response for virtual key discovery preview
    /// </summary>
    public class VirtualKeyDiscoveryPreviewDto
    {
        /// <summary>
        /// List of models visible to the virtual key
        /// </summary>
        public List<DiscoveredModelDto> Data { get; set; } = new List<DiscoveredModelDto>();

        /// <summary>
        /// Total count of models
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Represents a discovered model
    /// </summary>
    public class DiscoveredModelDto
    {
        /// <summary>
        /// Model identifier/alias
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Provider type
        /// </summary>
        public ProviderType? ProviderType { get; set; }

        /// <summary>
        /// Display name for the model
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Model capabilities
        /// </summary>
        public Dictionary<string, object> Capabilities { get; set; } = new Dictionary<string, object>();
    }
}