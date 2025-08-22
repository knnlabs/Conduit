using System.ComponentModel.DataAnnotations;

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
        /// The model alias used in client requests
        /// </summary>
        [Required(ErrorMessage = "Model Alias is required")]
        public string ModelAlias { get; set; } = string.Empty;
        
        /// <summary>
        /// The ID of the canonical Model entity
        /// </summary>
        [Required(ErrorMessage = "Model ID is required")]
        public int ModelId { get; set; }

        /// <summary>
        /// The provider-specific model identifier
        /// </summary>
        [Required(ErrorMessage = "Provider Model ID is required")]
        public string ProviderModelId { get; set; } = string.Empty;

        /// <summary>
        /// The ID of the provider
        /// </summary>
        [Required(ErrorMessage = "Provider ID is required")]
        public int ProviderId { get; set; }

        /// <summary>
        /// Provider reference information (populated when retrieving mappings)
        /// </summary>
        public ProviderReferenceDto? Provider { get; set; }

        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Provider-specific override for maximum context tokens.
        /// If null, uses Model.Capabilities.MaxTokens.
        /// </summary>
        public int? MaxContextTokensOverride { get; set; }


        /// <summary>
        /// Provider variation of the model (e.g., "Q4_K_M", "GGUF", "4bit-128g", "instruct")
        /// </summary>
        public string? ProviderVariation { get; set; }

        /// <summary>
        /// Quality score of the provider's model version.
        /// 1.0 = identical to original, 0.95 = 5% quality loss, etc.
        /// </summary>
        public decimal? QualityScore { get; set; }

        /// <summary>
        /// Whether this model is the default for its capability type
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>
        /// The capability type this model is default for (if IsDefault is true)
        /// </summary>
        public string? DefaultCapabilityType { get; set; }

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
