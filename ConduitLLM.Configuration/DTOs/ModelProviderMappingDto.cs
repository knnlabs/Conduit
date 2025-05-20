using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for model-provider mappings
    /// </summary>
    public class ModelProviderMappingDto
    {
        /// <summary>
        /// Unique identifier for the mapping
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// The model identifier used in client requests
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// The provider-specific model identifier
        /// </summary>
        public string ProviderModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// The provider identifier
        /// </summary>
        public string ProviderId { get; set; } = string.Empty;
        
        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// Optional model capabilities (e.g., vision, function-calling)
        /// </summary>
        public string? Capabilities { get; set; }
        
        /// <summary>
        /// Optional maximum context length
        /// </summary>
        public int? MaxContextLength { get; set; }
        
        /// <summary>
        /// Date when the mapping was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Date when the mapping was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Optional notes or description for this mapping
        /// </summary>
        public string? Notes { get; set; }
    }
}