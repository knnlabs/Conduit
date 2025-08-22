using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for model cost mapping information
    /// </summary>
    public class ModelCostMappingDto
    {
        /// <summary>
        /// Unique identifier for the cost-to-model mapping
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the ModelCost entity
        /// </summary>
        public int ModelCostId { get; set; }

        /// <summary>
        /// Foreign key to the ModelProviderMapping entity
        /// </summary>
        public int ModelProviderMappingId { get; set; }

        /// <summary>
        /// Indicates whether this cost mapping is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Model alias from the associated ModelProviderMapping
        /// </summary>
        public string? ModelAlias { get; set; }

        /// <summary>
        /// Provider model ID from the associated ModelProviderMapping
        /// </summary>
        public string? ProviderModelId { get; set; }

        /// <summary>
        /// Cost name from the associated ModelCost
        /// </summary>
        public string? CostName { get; set; }
    }

    /// <summary>
    /// Data transfer object for creating model cost mappings
    /// </summary>
    public class CreateModelCostMappingDto
    {
        /// <summary>
        /// ID of the model cost to associate
        /// </summary>
        [Required]
        public int ModelCostId { get; set; }

        /// <summary>
        /// List of ModelProviderMapping IDs to associate with this cost
        /// </summary>
        [Required]
        public List<int> ModelProviderMappingIds { get; set; } = new List<int>();

        /// <summary>
        /// Whether the mappings should be active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Data transfer object for updating model cost mappings
    /// </summary>
    public class UpdateModelCostMappingDto
    {
        /// <summary>
        /// ID of the model cost
        /// </summary>
        [Required]
        public int ModelCostId { get; set; }

        /// <summary>
        /// List of ModelProviderMapping IDs that should be associated with this cost
        /// </summary>
        /// <remarks>
        /// This will replace all existing mappings. Any mappings not in this list will be removed.
        /// </remarks>
        [Required]
        public List<int> ModelProviderMappingIds { get; set; } = new List<int>();
    }
}