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
        public int Priority { get; set; }

        /// <summary>
        /// Whether this mapping is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

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
