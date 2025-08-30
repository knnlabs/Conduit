using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Maps various model identifiers used by different providers to a canonical Model entity.
    /// This allows us to recognize that "gpt-4-0125-preview", "gpt-4-turbo-preview", and "gpt-4-1106-preview"
    /// all refer to the same underlying model.
    /// </summary>
    public class ModelProviderTypeAssociation
    {
        /// <summary>
        /// Unique identifier for this model identifier mapping.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Foreign key to the canonical Model entity.
        /// </summary>
        public int ModelId { get; set; }

        /// <summary>
        /// Indicates whether this mapping is currently enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Provider-specific override for maximum input tokens.
        /// If null, uses Model.Capabilities.MaxTokens.
        /// Some providers may have different limits than the base model.
        /// This only applies to chat and embedding models.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Provider-specific override for maximum output tokens.
        /// If null, uses Model.Capabilities.MaxTokens.
        /// Some providers may have different limits than the base model.
        /// This only applies to chat and embedding models.
        /// </summary>
        public int? MaxOutputTokens { get; set; }


        /// <summary>
        /// Represents the variation of the model provided by the provider.
        /// Examples: "Q4_K_M", "GGUF", "4bit-128g", "fine-tuned-medical", "instruct"
        /// </summary>
        public string? ProviderVariation { get; set; }

        /// <summary>
        /// Represents the quality score of the model provided by the provider.
        /// 1.0 = identical to original
        /// 0.95 = 5% quality loss (typical for good quantization)
        /// 0.8 = 20% quality loss (aggressive quantization)
        /// </summary>

        [Range(0.0, 1.0)]
        public decimal? QualityScore { get; set; } // Provider's quality vs original

        /// <summary>
        /// Represents the speed score of the model provided by this provider.
        /// Generally there is a correlation between the speed and the quantization level and/or the hardware used.
        /// This is a best-effort representation of speed that can be applied to chat, images, video, and other types of models.
        /// Some providers like Groq use specialized hardware that result in a speed increase.
        /// For example, Groq's implementation might achieve 10x speedup.
        /// 1.0 = identical to original (e.g., speed on typical Nvidia H100 cards)
        /// 1.5 = 50% speed increase 
        /// </summary>
        [Range(0.01, 100.0)]
        public decimal? SpeedScore { get; set; } // Provider's speed vs original

        /// <summary>
        /// The identifier string used by a provider or in API calls.
        /// Examples: "gpt-4-0125-preview", "claude-3-opus-20240229", "llama-3-70b-instruct"
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// The provider or source that uses this identifier.
        /// Examples: "openai", "azure", "anthropic", "openrouter", "deepinfra"
        /// Null indicates a universal identifier.
        /// </summary>
        [MaxLength(100)]
        public string? Provider { get; set; }


        /// <summary>
        /// Foreign key to the cost configuration for this model on this provider.
        /// </summary>
        public int? ModelCostId { get; set; }

        /// <summary>
        /// Navigation property to the cost configuration for this model on this provider.
        /// </summary>
        [ForeignKey("ModelCostId")]
        public virtual ModelCost? ModelCost { get; set; }

        /// <summary>
        /// Indicates whether this is the primary/canonical identifier for the model.
        /// </summary>
        public bool IsPrimary { get; set; } = false;

        /// <summary>
        /// Navigation property to the associated Model.
        /// </summary>
        [ForeignKey("ModelId")]
        public virtual Model Model { get; set; } = null!;

        /// <summary>
        /// Additional metadata about this model on this provider (e.g., deprecated status, release date).
        /// Stored as JSON.
        /// </summary>
        public string? Metadata { get; set; }
    }
}