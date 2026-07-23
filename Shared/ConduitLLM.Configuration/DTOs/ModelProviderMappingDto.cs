using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ConduitLLM.Configuration.Models;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Request used to fully update a model-provider mapping. The route ID is authoritative.
    /// </summary>
    public class UpdateModelProviderMappingDto
    {
        public string? ModelAlias { get; set; }

        public string? ProviderModelId { get; set; }

        public int? ProviderId { get; set; }

        public int? ModelProviderTypeAssociationId { get; set; }

        public int? Priority { get; set; }

        [Range(0.1, 2.0)]
        public decimal? Weight { get; set; }

        public bool? IsEnabled { get; set; }

        public Dictionary<string, JsonElement>? ProviderOptions { get; set; }
    }

    /// <summary>
    /// Data transfer object for model-provider mappings
    /// </summary>
    public class ModelProviderMappingDto
    {
        /// <summary>
        /// Unique identifier for the mapping
        /// </summary>
        [Required]
        public int Id { get; set; }

        /// <summary>
        /// The model alias used in client requests
        /// </summary>
        [Required(ErrorMessage = "Model Alias is required")]
        public string ModelAlias { get; set; } = string.Empty;

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
        /// The ID of the ModelProviderTypeAssociation entity.
        /// Links this mapping to provider-specific model metadata including variations, quality scores, and costs.
        /// This association provides the link to the canonical Model entity.
        /// </summary>
        [Required(ErrorMessage = "Model Provider Type Association is required")]
        public int ModelProviderTypeAssociationId { get; set; }

        /// <summary>
        /// The priority of this mapping (lower values have higher priority)
        /// </summary>
        [Required]
        public int Priority { get; set; }

        /// <summary>Balanced-score multiplier in the range 0.1 through 2.0.</summary>
        [Range(0.1, 2.0)]
        [Required]
        public decimal Weight { get; set; } = 1.0m;

        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        [Required]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Date when the mapping was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the mapping was last updated
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional provider-specific request options as a JSON object (OpenRouter: provider/plugins/
        /// transforms/models/route), merged into outgoing requests routed through this mapping.
        /// </summary>
        public Dictionary<string, JsonElement>? ProviderOptions { get; set; }

        /// <summary>
        /// Model capability flags (populated from Model entity)
        /// </summary>
        public ModelCapabilitiesDto? Capabilities { get; set; }
    }

    /// <summary>
    /// Data transfer object for model capabilities
    /// </summary>
    public class ModelCapabilitiesDto
    {
        /// <summary>Modalities accepted by this provider/model combination, or null when unknown.</summary>
        public IReadOnlyList<string>? InputModalities { get; set; }

        /// <summary>Modalities produced by this provider/model combination, or null when unknown.</summary>
        public IReadOnlyList<string>? OutputModalities { get; set; }

        /// <summary>Where the effective capability metadata originated.</summary>
        public ModelCapabilitySource CapabilitySource { get; set; }

        /// <summary>When the effective capability metadata was last verified.</summary>
        public DateTime? CapabilitiesLastVerifiedAt { get; set; }

        [Required]
        public bool SupportsImageInput { get; set; }

        [Required]
        public bool SupportsVideoInput { get; set; }

        [Required]
        public bool SupportsAudioInput { get; set; }

        [Required]
        public bool SupportsFileInput { get; set; }

        [Required]
        public bool SupportsVideoUnderstanding { get; set; }

        /// <summary>
        /// Indicates whether this model supports vision/image inputs
        /// </summary>
        [Required]
        public bool SupportsVision { get; set; }

        /// <summary>
        /// Indicates whether this model supports image generation
        /// </summary>
        [Required]
        public bool SupportsImageGeneration { get; set; }

        /// <summary>
        /// Indicates whether this model supports video generation
        /// </summary>
        [Required]
        public bool SupportsVideoGeneration { get; set; }

        /// <summary>
        /// Indicates whether this model supports embedding generation
        /// </summary>
        [Required]
        public bool SupportsEmbeddings { get; set; }

        /// <summary>
        /// Indicates whether the model supports speech-to-text transcription.
        /// </summary>
        [Required]
        public bool SupportsSpeechToText { get; set; }

        /// <summary>
        /// Indicates whether the model supports text-to-speech synthesis.
        /// </summary>
        [Required]
        public bool SupportsTextToSpeech { get; set; }

        /// <summary>
        /// Indicates whether the model supports document reranking.
        /// </summary>
        [Required]
        public bool SupportsRerank { get; set; }

        /// <summary>
        /// Indicates whether this model supports chat completions
        /// </summary>
        [Required]
        public bool SupportsChat { get; set; }

        /// <summary>
        /// Indicates whether this model supports function calling
        /// </summary>
        [Required]
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Indicates whether this model supports streaming responses
        /// </summary>
        [Required]
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Maximum input tokens of the model
        /// </summary>
        [Required]
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Maximum output tokens of the model
        /// </summary>
        [Required]
        public int? MaxOutputTokens { get; set; }
    }
}
