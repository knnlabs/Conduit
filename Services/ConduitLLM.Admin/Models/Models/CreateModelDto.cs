namespace ConduitLLM.Admin.Models.Models
{
    /// <summary>
    /// Data transfer object for creating a new AI model in the system.
    /// </summary>
    /// <remarks>
    /// This DTO contains the minimum required information to register a new model.
    /// When creating a model, you must specify its canonical name, the series it belongs to,
    /// and its capabilities configuration. The model will inherit characteristics from
    /// its series and capabilities.
    /// 
    /// Before creating a model, ensure that:
    /// 1. The ModelSeries exists (or create it first)
    /// 2. The ModelCapabilities configuration exists (or create it first)
    /// 3. The model name is unique within the system
    /// 
    /// Models created through this DTO will need ModelProviderMappings to be actually
    /// usable by the system, as the mappings connect the canonical model to specific
    /// provider implementations.
    /// </remarks>
    public class CreateModelDto
    {
        /// <summary>
        /// Gets or sets the canonical name of the model to create.
        /// </summary>
        /// <remarks>
        /// This should be a standardized name that uniquely identifies the model,
        /// following naming conventions like "gpt-4", "claude-3-opus", "llama-3.1-70b".
        /// The name should be lowercase with hyphens separating components.
        /// This name will be used for model selection and routing.
        /// </remarks>
        /// <example>gpt-4-turbo</example>
        /// <example>claude-3-opus-20240229</example>
        /// <value>The canonical model name.</value>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ID of the model series this model will belong to.
        /// </summary>
        /// <remarks>
        /// The series groups related models together and defines their author/organization.
        /// For example, all GPT models belong to the "GPT" series by OpenAI,
        /// all Claude models belong to the "Claude" series by Anthropic.
        /// The series must exist before creating the model.
        /// </remarks>
        /// <value>The foreign key reference to an existing ModelSeries.</value>
        public int ModelSeriesId { get; set; }

        // Capability fields embedded directly in CreateModelDto
        
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
        /// Gets or sets whether the model should be active upon creation.
        /// </summary>
        /// <remarks>
        /// If not specified, defaults to true. Set to false if you want to create
        /// the model in an inactive state (e.g., for preparation before launch).
        /// Inactive models cannot be used for requests but can still be configured
        /// with provider mappings and costs.
        /// </remarks>
        /// <value>True to create an active model; false for inactive; null for default (true).</value>
        public bool? IsActive { get; set; }

        /// <summary>
        /// Gets or sets the model-specific parameter configuration for UI generation.
        /// </summary>
        /// <remarks>
        /// This optional JSON string contains parameter definitions that override the series-level
        /// parameters. When null or empty, the model uses its series' parameter configuration.
        /// This allows for model-specific customization while maintaining series defaults.
        /// 
        /// The JSON should follow the same schema as series parameters, defining UI controls
        /// like sliders, selects, and inputs for model-specific parameters.
        /// </remarks>
        /// <value>JSON string containing parameter definitions, or null to use series defaults.</value>
        public string? ModelParameters { get; set; }
    }
}