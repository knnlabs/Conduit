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

        [Required]
        [MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty; // e.g., "OpenAI", "Anthropic"

        [Required]
        public string ApiKey { get; set; } = string.Empty;

        // Optional: Base URL if different from default
        public string? BaseUrl { get; set; }

        // Optional: Other provider-specific settings
        public string? ApiVersion { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
