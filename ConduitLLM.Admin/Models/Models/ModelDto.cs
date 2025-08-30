using ConduitLLM.Admin.Models.ModelSeries;

namespace ConduitLLM.Admin.Models.Models
{
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
        public string? ModelParameters { get; set; }
    }
}