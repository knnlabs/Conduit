using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents an individual API key credential for an external LLM provider.
    /// Multiple key credentials can be associated with a single provider for load balancing,
    /// failover, and account-based organization.
    /// </summary>
    public class ProviderKeyCredential
    {
        /// <summary>
        /// Gets or sets the unique identifier for this provider key credential.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the ID of the parent provider credential.
        /// </summary>
        // This is associated with a ProviderCredential. 
        // It needs to map to the ProviderCredentialId in the ProviderCredential table
        [Required]
        public int ProviderCredentialId { get; set; }
        
        /// <summary>
        /// Gets or sets the parent provider credential that owns this key.
        /// </summary>
        public ProviderCredential ProviderCredential { get; set; } = null!;


        /// <summary>
        /// Gets or sets the provider account group identifier (0-32).
        /// This represents which external provider account this key belongs to.
        /// Keys with the same ProviderAccountGroup share quota limits and billing.
        /// This is used for intelligent failover - if one account hits rate limits,
        /// the system can switch to keys from a different account group.
        /// Note: This refers to external provider accounts, not Conduit user accounts.
        /// </summary>
        // Each key can be part of an account on that LLM Provider. This is not related to our own concept of accounts as this is solely for tracking the external account.
        // This is used for failover purposes
        [Range(0, 32)]
        public short ProviderAccountGroup { get; set; } = 0;
    
        /// <summary>
        /// Gets or sets the API key for this credential. Made nullable to allow for empty/cleared keys.
        /// </summary>
        // Made ApiKey nullable to allow for empty/cleared keys
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the base URL for this specific key. Overrides the provider default if specified.
        /// </summary>
        // Optional: Base URL if different from default
        public string? BaseUrl { get; set; }

        // Optional: Other provider-specific settings
        [Obsolete("This field is rarely used and will be removed in v2.0")]
        public string? ApiVersion { get; set; }
        
        /// <summary>
        /// Gets or sets the organization or project ID for this key. Overrides provider default if specified.
        /// </summary>
        // Optional: Organization or project ID (overrides provider default)
        public string? Organization { get; set; }
        
        /// <summary>
        /// Gets or sets a human-readable name for this key to help with identification and management.
        /// </summary>
        // Optional: Human-readable name for this key
        public string? KeyName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is the primary key for the provider.
        /// Only one key per provider can be marked as primary. The primary key is used as the default.
        /// </summary>
        // Whether this is the primary key for the provider
        // There can be only one primary for the provider
        public bool IsPrimary { get; set; } = false;
        
        /// <summary>
        /// Gets or sets a value indicating whether this key credential is enabled and available for use.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        

        /// <summary>
        /// Gets or sets the UTC timestamp when this key credential was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the UTC timestamp when this key credential was last updated.
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}