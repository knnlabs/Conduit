using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConduitLLM.Configuration.Entities
{
    /// <summary>
    /// Represents a key credential for an external LLM provider.
    /// Placeholder entity definition. This is not yet implemented.
    /// </summary>
    public class ProviderKeyCredential
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // This is associated with a ProviderCredential. 
        // It needs to map to the ProviderCredentialId in the ProviderCredential table
        [Required]
        public int ProviderCredentialId { get; set; }   
        public ProviderCredential ProviderCredential { get; set; } = null!;


        // Each key can be part of an account on that LLM Provider. This is not related to our own concept of accounts as this is solely for tracking the external account.
        // This is used for failover purposes
        [Range(0, 32)]
        public short ProviderAccountGroup { get; set; } = 0;
    
        // Made ApiKey nullable to allow for empty/cleared keys
        public string? ApiKey { get; set; }

        // Optional: Base URL if different from default
        public string? BaseUrl { get; set; }

        // Optional: Other provider-specific settings
        public string? ApiVersion { get; set; }
        
        // Optional: Organization or project ID (overrides provider default)
        public string? Organization { get; set; }
        
        // Optional: Human-readable name for this key
        public string? KeyName { get; set; }

        // Whether this is the primary key for the provider
        // There can be only one primary for the provider
        public bool IsPrimary { get; set; } = false;
        
        public bool IsEnabled { get; set; } = true;
        

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}