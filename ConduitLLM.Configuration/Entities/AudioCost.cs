using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Cost configuration for audio operations.
    /// </summary>
    public class AudioCost
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Provider type.
        /// </summary>
        [Required]
        public ProviderType Provider { get; set; }

        /// <summary>
        /// Operation type (transcription, tts, realtime).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>
        /// Model name (optional, for model-specific pricing).
        /// </summary>
        [MaxLength(100)]
        public string? Model { get; set; }

        /// <summary>
        /// Cost unit type (per_minute, per_character, per_second).
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string CostUnit { get; set; } = string.Empty;

        /// <summary>
        /// Cost per unit in USD.
        /// </summary>
        [Column(TypeName = "decimal(10, 6)")]
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Minimum charge amount (if applicable).
        /// </summary>
        [Column(TypeName = "decimal(10, 6)")]
        public decimal? MinimumCharge { get; set; }

        /// <summary>
        /// Additional cost factors as JSON.
        /// </summary>
        public string? AdditionalFactors { get; set; }

        /// <summary>
        /// Whether this cost entry is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Effective date for this pricing.
        /// </summary>
        public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End date for this pricing (null if current).
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
