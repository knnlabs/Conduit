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

        /// <summary>
        /// Gets or sets the ID of the capabilities configuration for this model.
        /// </summary>
        /// <remarks>
        /// The capabilities define what the model can do (chat, vision, function calling, etc.).
        /// Multiple models can share the same capabilities configuration. For example,
        /// all GPT-4 variants might reference the same capabilities. The capabilities
        /// configuration must exist before creating the model.
        /// </remarks>
        /// <value>The foreign key reference to an existing ModelCapabilities.</value>
        public int ModelCapabilitiesId { get; set; }

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
    }
}