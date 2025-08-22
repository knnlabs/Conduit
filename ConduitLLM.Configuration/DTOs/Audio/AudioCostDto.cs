using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.Audio
{
    /// <summary>
    /// DTO for audio cost configuration.
    /// </summary>
    public class AudioCostDto
    {
        /// <summary>
        /// Unique identifier for the cost configuration.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Provider ID.
        /// </summary>
        [Required]
        public int ProviderId { get; set; }

        /// <summary>
        /// Provider name (from navigation property).
        /// </summary>
        public string? ProviderName { get; set; }

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
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Minimum charge amount (if applicable).
        /// </summary>
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
        public DateTime EffectiveFrom { get; set; }

        /// <summary>
        /// End date for this pricing (null if current).
        /// </summary>
        public DateTime? EffectiveTo { get; set; }

        /// <summary>
        /// When the cost configuration was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// When the cost configuration was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for creating a new audio cost configuration.
    /// </summary>
    public class CreateAudioCostDto
    {
        /// <summary>
        /// Provider ID.
        /// </summary>
        [Required]
        public int ProviderId { get; set; }

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
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Cost per unit must be non-negative")]
        public decimal CostPerUnit { get; set; }

        /// <summary>
        /// Minimum charge amount (if applicable).
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "Minimum charge must be non-negative")]
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
    }

    /// <summary>
    /// DTO for updating an audio cost configuration.
    /// </summary>
    public class UpdateAudioCostDto : CreateAudioCostDto
    {
    }
}
