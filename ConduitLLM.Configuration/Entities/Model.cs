using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Entity representing a machine learning model. Each Model can typically be found on one or more providers.
    /// This is a convenient way to associate costs, capabilities, and configurations with a specific model.
    /// We are assuming that the cost is primarily determined by the model variant and its associated provider.
    /// </summary>
    public class Model
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
        [ForeignKey("ModelSeriesId")]
        public ModelSeries Series { get; set; } = new ModelSeries();

        /// <summary>
        /// Foreign key for the shared model capabilities.
        /// </summary>
        public int ModelCapabilitiesId { get; set; }
        
        /// <summary>
        /// Navigation property to the model capabilities.
        /// Multiple models can share the same capabilities instance.
        /// </summary>
        [ForeignKey("ModelCapabilitiesId")]
        public ModelCapabilities Capabilities { get; set; } = new ModelCapabilities();

        /// <summary>
        /// Navigation property for all identifiers associated with this model.
        /// </summary>
        public virtual ICollection<ModelProviderTypeAssociation> Identifiers { get; set; } = new List<ModelProviderTypeAssociation>();

        /// <summary>
        /// Navigation property for all provider mappings using this model.
        /// </summary>
        public virtual ICollection<ModelProviderMapping> ProviderMappings { get; set; } = new List<ModelProviderMapping>();

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