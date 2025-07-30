using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents credentials for an external LLM provider.
    /// This is the main entity for managing provider configurations and serves as the parent
    /// for multiple API keys through the ProviderKeyCredentials collection.
    /// </summary>
    public class ProviderCredential
    {
        /// <summary>
        /// Gets or sets the unique identifier for this provider credential.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the provider type enum. This is the preferred way to identify providers.
        /// </summary>
        [Required]
        public ProviderType ProviderType { get; set; } = ProviderType.OpenAI;

        /// <summary>
        /// Gets or sets the user-friendly name for this provider instance.
        /// For example: "Production OpenAI", "Dev Azure OpenAI", "Nick's Ollama Server"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the base URL for the provider API. Optional - uses provider default if not specified.
        /// </summary>
        // Optional: Base URL if different from default
        public string? BaseUrl { get; set; }


        /// <summary>
        /// Gets or sets a value indicating whether this provider credential is enabled and available for use.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the UTC timestamp when this provider credential was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the UTC timestamp when this provider credential was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the collection of API keys associated with this provider.
        /// This enables multi-key support for load balancing and failover scenarios.
        /// </summary>
        // Navigation property for the one-to-many relationship
        public ICollection<ProviderKeyCredential> ProviderKeyCredentials { get; set; } = new List<ProviderKeyCredential>();
    }
}
