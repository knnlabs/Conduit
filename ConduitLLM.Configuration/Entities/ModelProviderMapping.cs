using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Maps a generic model alias (e.g., "gpt-4-turbo") to a specific provider's model name 
    /// and associates it with provider credentials.
    /// Placeholder entity definition.
    /// </summary>
    public class ModelProviderMapping
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string ModelAlias { get; set; } = string.Empty; // Generic name used in requests

        [Required]
        [MaxLength(100)]
        public string ProviderModelName { get; set; } = string.Empty; // Actual model name for the provider

        // Foreign Key to ProviderCredential
        public int ProviderCredentialId { get; set; }

        [ForeignKey("ProviderCredentialId")]
        public virtual ProviderCredential ProviderCredential { get; set; } = null!; // Navigation property

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The maximum number of tokens the model's context window can handle.
        /// Used for automatic context management to prevent exceeding model limits.
        /// </summary>
        public int? MaxContextTokens { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Optional: Add properties for model-specific configurations if needed
        // public int? MaxTokens { get; set; } 
        // public double? TemperatureDefault { get; set; }
    }
}
