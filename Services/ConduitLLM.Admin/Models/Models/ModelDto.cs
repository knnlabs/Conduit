using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ConduitLLM.Admin.Models.ModelSeries;
using ConduitLLM.Configuration.Models;
using System.Text.Json;

namespace ConduitLLM.Admin.Models.Models
{
    /// <summary>
    /// Lightweight DTO for a model's provider type association (identifier).
    /// </summary>
    public class ModelIdentifierDto
    {
        /// <summary>Gets or sets the unique identifier for this model-provider association.</summary>
        [Required]
        public int Id { get; set; }

        /// <summary>Gets or sets the provider-specific model identifier string (e.g., "gpt-4-turbo" for OpenAI).</summary>
        [Required]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>Gets or sets the provider ID that offers this model, or null if unassigned.</summary>
        [Required]
        public int? Provider { get; set; }

        /// <summary>Gets or sets whether this is the primary (preferred) provider for the model.</summary>
        [Required]
        public bool IsPrimary { get; set; }

        /// <summary>Gets or sets provider-specific metadata.</summary>
        public Dictionary<string, JsonElement>? Metadata { get; set; }

        /// <summary>Gets or sets the maximum input token limit for this provider's offering, or null if unknown.</summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>Gets or sets the maximum output token limit for this provider's offering, or null if unknown.</summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>Gets or sets the relative speed score for this provider's offering, used for routing decisions.</summary>
        public decimal? SpeedScore { get; set; }

        /// <summary>Gets or sets the relative quality score for this provider's offering, used for routing decisions.</summary>
        public decimal? QualityScore { get; set; }

        /// <summary>Gets or sets the provider-specific variation label (e.g., "turbo", "mini") if applicable.</summary>
        public string? ProviderVariation { get; set; }

        /// <summary>Gets or sets the associated model cost configuration ID, or null if no cost tracking is configured.</summary>
        public int? ModelCostId { get; set; }

        /// <summary>Provider-specific accepted modality override; null inherits the model.</summary>
        public IReadOnlyList<string>? InputModalities { get; set; }

        /// <summary>Provider-specific output modality override; null inherits the model.</summary>
        public IReadOnlyList<string>? OutputModalities { get; set; }

        /// <summary>Provider-specific operation overrides; null members inherit the model.</summary>
        public ProviderOperationalCapabilities? OperationalCapabilities { get; set; }

        public ModelCapabilitySource? CapabilitySource { get; set; }
        public DateTime? CapabilitiesLastVerifiedAt { get; set; }
    }

    /// <summary>
    /// A configured provider instance that can serve a model identifier.
    /// </summary>
    public class AvailableProviderDto
    {
        /// <summary>Gets or sets the provider instance ID.</summary>
        [Required]
        public int ProviderId { get; set; }

        /// <summary>Gets or sets the provider instance display name.</summary>
        [Required]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>Gets or sets the provider type name.</summary>
        [Required]
        public string ProviderType { get; set; } = string.Empty;
    }

    /// <summary>
    /// A model-provider association together with the configured provider instances that can serve it.
    /// </summary>
    public class ModelProviderAvailabilityDto
    {
        /// <summary>Gets or sets the model-provider association ID.</summary>
        [Required]
        public int AssociationId { get; set; }

        /// <summary>Gets or sets the provider-specific model identifier.</summary>
        [Required]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>Gets or sets the numeric provider type, or null when unassigned.</summary>
        [Required]
        public int? Provider { get; set; }

        /// <summary>Gets or sets the provider-specific variation label.</summary>
        [Required]
        public string? ProviderVariation { get; set; }

        /// <summary>Gets or sets the provider-specific maximum input token count.</summary>
        [Required]
        public int? MaxInputTokens { get; set; }

        /// <summary>Gets or sets the provider-specific maximum output token count.</summary>
        [Required]
        public int? MaxOutputTokens { get; set; }

        /// <summary>Gets or sets the relative speed score.</summary>
        [Required]
        public decimal? SpeedScore { get; set; }

        /// <summary>Gets or sets the relative quality score.</summary>
        [Required]
        public decimal? QualityScore { get; set; }

        /// <summary>Gets or sets whether this association is primary.</summary>
        [Required]
        public bool IsPrimary { get; set; }

        /// <summary>Provider-specific input modality override; null inherits the canonical model.</summary>
        public IReadOnlyList<string>? InputModalities { get; set; }

        /// <summary>Provider-specific output modality override; null inherits the canonical model.</summary>
        public IReadOnlyList<string>? OutputModalities { get; set; }

        /// <summary>Provider-specific operation overrides.</summary>
        public ProviderOperationalCapabilities? OperationalCapabilities { get; set; }

        /// <summary>Source of provider-specific capability metadata.</summary>
        public ModelCapabilitySource? CapabilitySource { get; set; }

        /// <summary>When provider-specific capability metadata was last verified.</summary>
        public DateTime? CapabilitiesLastVerifiedAt { get; set; }

        /// <summary>Gets or sets configured provider instances matching this association.</summary>
        [Required]
        public List<AvailableProviderDto> AvailableProviders { get; set; } = new();
    }

    /// <summary>
    /// Data transfer object representing a canonical AI model in the system.
    /// </summary>
    /// <remarks>
    /// This DTO provides a complete view of a model including its capabilities, series information,
    /// and metadata. Models represent the canonical definition of AI models available across different
    /// providers (e.g., GPT-4, Claude, Llama). Each model can be offered by multiple providers through
    /// ModelProviderMapping relationships.
    /// 
    /// The model serves as the single source of truth for capabilities and characteristics,
    /// independent of which provider is actually serving the model at runtime.
    /// </remarks>
    public class ModelDto
    {
        /// <summary>
        /// Gets or sets the unique identifier for the model.
        /// </summary>
        /// <value>The database-generated ID that uniquely identifies this model across the system.</value>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the canonical name of the model.
        /// </summary>
        /// <remarks>
        /// This is the standardized name used internally to identify the model,
        /// such as "gpt-4", "claude-3-opus", or "llama-3.1-70b". This name is used
        /// for model selection and should be consistent across providers offering
        /// the same model.
        /// </remarks>
        /// <value>The canonical model name.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the model series this model belongs to.
        /// </summary>
        /// <remarks>
        /// Models are grouped into series (e.g., GPT-4 series, Claude series, Llama series)
        /// which share common characteristics and typically come from the same author/organization.
        /// This relationship helps with organizing models and understanding their lineage.
        /// </remarks>
        /// <value>The foreign key reference to the ModelSeries entity.</value>
        public int ModelSeriesId { get; set; }

        // Capability fields embedded directly in ModelDto

        /// <summary>Modalities accepted by the model, or null when unknown.</summary>
        public IReadOnlyList<string>? InputModalities { get; set; }

        /// <summary>Modalities produced by the model, or null when unknown.</summary>
        public IReadOnlyList<string>? OutputModalities { get; set; }

        /// <summary>Where the directional capability metadata originated.</summary>
        public ModelCapabilitySource CapabilitySource { get; set; }

        /// <summary>When the directional capability metadata was last verified.</summary>
        public DateTime? CapabilitiesLastVerifiedAt { get; set; }

        /// <summary>Whether image input is explicitly supported.</summary>
        public bool SupportsImageInput { get; set; }

        /// <summary>Whether video input is explicitly supported.</summary>
        public bool SupportsVideoInput { get; set; }

        /// <summary>Whether audio input is explicitly supported.</summary>
        public bool SupportsAudioInput { get; set; }

        /// <summary>Whether file input is explicitly supported.</summary>
        public bool SupportsFileInput { get; set; }

        /// <summary>Whether the model can accept video and produce a text response.</summary>
        public bool SupportsVideoUnderstanding { get; set; }
        
        /// <summary>
        /// Gets or sets whether the model supports chat/conversation interactions.
        /// </summary>
        public bool SupportsChat { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports vision/image understanding.
        /// </summary>
        public bool SupportsVision { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports function/tool calling.
        /// </summary>
        public bool SupportsFunctionCalling { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports streaming responses.
        /// </summary>
        public bool SupportsStreaming { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports image generation.
        /// </summary>
        public bool SupportsImageGeneration { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports video generation.
        /// </summary>
        public bool SupportsVideoGeneration { get; set; }

        /// <summary>Whether the model supports speech-to-text transcription.</summary>
        public bool SupportsSpeechToText { get; set; }

        /// <summary>Whether the model supports text-to-speech synthesis.</summary>
        public bool SupportsTextToSpeech { get; set; }

        /// <summary>Whether the model supports document reranking.</summary>
        public bool SupportsRerank { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports text embeddings generation.
        /// </summary>
        public bool SupportsEmbeddings { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of input tokens the model can process.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of output tokens the model can generate.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer type used by this model.
        /// </summary>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// Gets or sets whether this model is currently active and available for use.
        /// </summary>
        /// <remarks>
        /// Inactive models are retained in the database for historical purposes but
        /// are not available for new requests. Models might be deactivated when deprecated,
        /// experiencing issues, or being phased out.
        /// </remarks>
        /// <value>True if the model is active and available; otherwise, false.</value>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this model was first created in the system.
        /// </summary>
        /// <remarks>
        /// This timestamp is set when the model is initially added to the database,
        /// typically during seed data loading or when a new model is discovered and added.
        /// </remarks>
        /// <value>The UTC timestamp of model creation.</value>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this model was last updated.
        /// </summary>
        /// <remarks>
        /// This timestamp is updated whenever any property of the model changes,
        /// including activation status, capabilities, or series assignment.
        /// </remarks>
        /// <value>The UTC timestamp of the last update.</value>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the model series information.
        /// </summary>
        /// <remarks>
        /// This includes the series metadata like name, author, tokenizer type,
        /// and importantly the UI parameters configuration. This is populated
        /// when the model is fetched with details.
        /// </remarks>
        /// <value>The series object, or null if not loaded.</value>
        public ModelSeriesDto? Series { get; set; }

        /// <summary>
        /// Gets or sets the model-specific parameter configuration for UI generation.
        /// </summary>
        /// <remarks>
        /// This JSON string contains parameter definitions that override the series-level
        /// parameters. When null, the model uses its series' parameter configuration.
        /// This allows for model-specific customization while maintaining series defaults.
        /// </remarks>
        /// <value>JSON string containing parameter definitions, or null to use series defaults.</value>
        public Dictionary<string, JsonElement>? ModelParameters { get; set; }

        /// <summary>
        /// Gets or sets the provider type associations (identifiers) for this model.
        /// </summary>
        /// <remarks>
        /// Included when the model is fetched with details. Each identifier represents
        /// a provider-specific mapping showing which providers offer this model and under
        /// what identifier string.
        /// </remarks>
        public List<ModelIdentifierDto>? Identifiers { get; set; }
    }
}
