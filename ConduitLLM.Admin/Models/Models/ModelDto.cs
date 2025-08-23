using ConduitLLM.Admin.Models.ModelCapabilities;
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

        /// <summary>
        /// Gets or sets the ID of the capabilities configuration for this model.
        /// </summary>
        /// <remarks>
        /// Multiple models can share the same capabilities configuration to avoid duplication.
        /// For example, all GPT-4 variants might share the same capability set.
        /// </remarks>
        /// <value>The foreign key reference to the ModelCapabilities entity.</value>
        public int ModelCapabilitiesId { get; set; }

        /// <summary>
        /// Gets or sets the detailed capabilities of this model.
        /// </summary>
        /// <remarks>
        /// This nested object provides comprehensive information about what the model can do,
        /// including support for chat, vision, function calling, streaming, and various
        /// generation capabilities. This is populated when the model is fetched with details.
        /// </remarks>
        /// <value>The capabilities object, or null if not loaded.</value>
        public ModelCapabilitiesDto? Capabilities { get; set; }

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
    }
}