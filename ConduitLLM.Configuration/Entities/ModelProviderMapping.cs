using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Maps a generic model alias (e.g., "gpt-4-turbo") to a specific provider's model name 
    /// and associates it with provider credentials. This entity enables routing requests to 
    /// specific provider models regardless of the model name used in the request.
    /// </summary>
    public class ModelProviderMapping
    {
        /// <summary>
        /// Unique identifier for the model-provider mapping.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// User-friendly model alias used in client requests.
        /// This is the name that clients will use in their API calls (e.g., "gpt-4").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ModelAlias { get; set; } = string.Empty;

        /// <summary>
        /// The actual model identifier expected by the provider.
        /// This is the provider-specific model name (e.g., "gpt-4-turbo-preview", "claude-3-opus-20240229").
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProviderModelName { get; set; } = string.Empty;

        /// <summary>
        /// Foreign key to the provider credential entity.
        /// Links this mapping to the credential set used to authenticate with the provider.
        /// </summary>
        public int ProviderCredentialId { get; set; }

        /// <summary>
        /// Navigation property to the associated provider credential.
        /// Contains authentication details for connecting to the provider.
        /// </summary>
        [ForeignKey("ProviderCredentialId")]
        public virtual ProviderCredential ProviderCredential { get; set; } = null!;

        /// <summary>
        /// Indicates whether this mapping is currently active.
        /// When false, the router will not use this mapping for routing requests.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// The maximum number of tokens the model's context window can handle.
        /// Used for automatic context management to prevent exceeding model limits.
        /// </summary>
        public int? MaxContextTokens { get; set; }

        /// <summary>
        /// The UTC timestamp when this mapping was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// The UTC timestamp when this mapping was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Optional: Add properties for model-specific configurations if needed
        // public int? MaxTokens { get; set; } 
        // public double? TemperatureDefault { get; set; }
    }
}
