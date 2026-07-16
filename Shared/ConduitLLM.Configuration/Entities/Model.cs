using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

using ConduitLLM.Configuration.Entities.Interfaces;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Entity representing a machine learning model. Each Model can typically be found on one or more providers.
    /// This is a convenient way to associate costs, capabilities, and configurations with a specific model.
    /// We are assuming that the cost is primarily determined by the model variant and its associated provider.
    /// </summary>
    public class Model : IEntity<int>, IAuditableEntity
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The name of the model (e.g., GPT-4, Claude, etc.)
        /// </summary>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The version of the model (e.g., "v1", "v2", etc.)
        /// </summary>
        public string? Version { get; set; } = string.Empty;

        /// <summary>
        /// A brief description of the model
        /// </summary>
        public string? Description { get; set; } = string.Empty;

        /// <summary>
        /// The URL to the model's card (e.g., documentation, specifications, etc.)
        /// </summary>
        public string? ModelCardUrl { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key for the model series this model belongs to.
        /// </summary>
        public int ModelSeriesId { get; set; }
        
        /// <summary>
        /// Navigation property to the model series.
        /// </summary>
        /// <remarks>
        /// JsonIgnore is applied to prevent circular reference during serialization.
        /// The cycle is: Model → Series → Models → Model
        /// </remarks>
        [ForeignKey("ModelSeriesId")]
        [JsonIgnore]
        public ModelSeries Series { get; set; } = new ModelSeries();
        
        /// <summary>
        /// Indicates whether this model supports vision/image inputs.
        /// </summary>
        public bool SupportsVision { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports image generation.
        /// </summary>
        public bool SupportsImageGeneration { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports video generation.
        /// </summary>
        public bool SupportsVideoGeneration { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports embedding generation.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports chat completions.
        /// </summary>
        public bool SupportsChat { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports function calling.
        /// </summary>
        public bool SupportsFunctionCalling { get; set; } = false;

        /// <summary>
        /// Indicates whether this model supports streaming responses.
        /// </summary>
        public bool SupportsStreaming { get; set; } = false;

        /// <summary>
        /// The tokenizer type used by this model (e.g., "cl100k_base", "p50k_base", "claude").
        /// </summary>
        public TokenizerType TokenizerType { get; set; }

        /// <summary>
        /// Maximum input tokens of the model
        /// Some providers may have different limits than the base model, and can override this.
        /// This only applies to chat and embedding models.
        /// </summary>
        [Range(1024, int.MaxValue)]
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Maximum output tokens of the model
        /// Some providers may have different limits than the base model, and can override this.
        /// This only applies to chat and embedding models.
        /// </summary>
        [Range(1024, int.MaxValue)]
        public int? MaxOutputTokens { get; set; }


        /// <summary>
        /// Navigation property for all identifiers associated with this model.
        /// </summary>
        /// <remarks>
        /// JsonIgnore is applied to prevent circular reference during serialization.
        /// The cycle is: Model → Identifiers → ModelProviderTypeAssociation → Model
        /// </remarks>
        [JsonIgnore]
        public virtual ICollection<ModelProviderTypeAssociation> Identifiers { get; set; } = new List<ModelProviderTypeAssociation>();

        /// <summary>
        /// Whether the model is active and available for use.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Internal storage for model-specific parameters.
        /// When null, Parameters property will fall back to ModelSeries.Parameters.
        /// </summary>
        [Column("Parameters")]
        public string? ModelParameters { get; set; }

        /// <summary>
        /// JSON string containing parameter definitions for UI generation.
        /// If ModelParameters is null, falls back to ModelSeries.Parameters.
        /// Allows model-specific parameter overrides while inheriting series defaults.
        /// </summary>
        [NotMapped]
        public string? Parameters 
        { 
            get => ModelParameters ?? Series?.Parameters ?? "{}";
            set => ModelParameters = value;
        }

        /// <summary>
        /// Date the model was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date the model was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

}