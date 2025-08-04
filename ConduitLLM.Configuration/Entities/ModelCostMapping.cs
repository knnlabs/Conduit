using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Junction entity that links ModelCost records to ModelProviderMapping records.
    /// This enables a many-to-many relationship where one cost configuration can apply
    /// to multiple model mappings (e.g., Llama cost applies to Llama on multiple providers).
    /// </summary>
    public class ModelCostMapping
    {
        /// <summary>
        /// Gets or sets the unique identifier for this cost-to-model mapping.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the foreign key to the ModelCost entity.
        /// </summary>
        public int ModelCostId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the associated ModelCost.
        /// </summary>
        [ForeignKey("ModelCostId")]
        public virtual ModelCost ModelCost { get; set; } = null!;

        /// <summary>
        /// Gets or sets the foreign key to the ModelProviderMapping entity.
        /// </summary>
        public int ModelProviderMappingId { get; set; }

        /// <summary>
        /// Gets or sets the navigation property to the associated ModelProviderMapping.
        /// </summary>
        [ForeignKey("ModelProviderMappingId")]
        public virtual ModelProviderMapping ModelProviderMapping { get; set; } = null!;

        /// <summary>
        /// Gets or sets the UTC timestamp when this mapping was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets whether this cost mapping is active.
        /// </summary>
        /// <remarks>
        /// When false, this specific model-cost association is ignored during cost calculations.
        /// Allows temporarily disabling cost application without deleting the mapping.
        /// </remarks>
        public bool IsActive { get; set; } = true;
    }
}