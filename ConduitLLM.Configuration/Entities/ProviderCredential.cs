using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents credentials for an external LLM provider.
    /// Placeholder entity definition.
    /// </summary>
    public class ProviderCredential
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // TODO: Use an ID instead of a name. Something like ProviderId
        // DEPRECATED: In the SDKs we'll use the ProviderType as a substitute for the name
        [Required]
        [MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty; // e.g., "OpenAI", "Anthropic"

        // TODO: Add a ProviderType enum that is based on the Provider class that is used
        // e.g. OpenAIProvider, AnthropicProvider, etc.
        public ProviderType ProviderType { get; set; } = ProviderType.OpenAI;

        // Made ApiKey nullable to allow for empty/cleared keys
        // TODO: Replace this with a ProviderKeyCredential class that will support multiple keys
        // DEPRECATED: This will be replaced by the ProviderKeyCredentials collection
        public string? ApiKey { get; set; }

        // Optional: Base URL if different from default
        public string? BaseUrl { get; set; }

        // Optional: Other provider-specific settings
        public string? ApiVersion { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property for the one-to-many relationship
        public ICollection<ProviderKeyCredential> ProviderKeyCredentials { get; set; } = new List<ProviderKeyCredential>();
    }
}
