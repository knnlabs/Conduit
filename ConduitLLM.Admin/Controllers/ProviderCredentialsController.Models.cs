using System;
using ConduitLLM.Configuration;

namespace ConduitLLM.Admin.Controllers
{
    /// <summary>
    /// Request model for creating a provider
    /// </summary>
    public class CreateProviderRequest
    {
        /// <summary>
        /// The type of provider to create
        /// </summary>
        public ProviderType ProviderType { get; set; }
        
        /// <summary>
        /// The name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
        
        /// <summary>
        /// The base URL for the provider (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Request model for updating a provider
    /// </summary>
    public class UpdateProviderRequest
    {
        /// <summary>
        /// The new name for the provider (optional)
        /// </summary>
        public string? ProviderName { get; set; }
        
        /// <summary>
        /// The new base URL for the provider (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Request model for testing a provider connection
    /// </summary>
    public class TestProviderRequest
    {
        /// <summary>
        /// The type of provider to test
        /// </summary>
        public ProviderType ProviderType { get; set; }
        
        /// <summary>
        /// The API key to test
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// The base URL to test (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// The organization to test (optional)
        /// </summary>
        public string? Organization { get; set; }
    }

    /// <summary>
    /// Event published when a provider is updated
    /// </summary>
    public class ProviderUpdated
    {
        /// <summary>
        /// The ID of the updated provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Whether the provider is enabled after the update
        /// </summary>
        public bool IsEnabled { get; set; }
        
        /// <summary>
        /// List of properties that were changed
        /// </summary>
        public string[] ChangedProperties { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Correlation ID for tracking the event
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event published when a provider is deleted
    /// </summary>
    public class ProviderDeleted
    {
        /// <summary>
        /// The ID of the deleted provider
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// Correlation ID for tracking the event
        /// </summary>
        public string CorrelationId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for creating a key credential
    /// </summary>
    public class CreateKeyRequest
    {
        /// <summary>
        /// The API key to create
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        
        /// <summary>
        /// The name for the key credential
        /// </summary>
        public string KeyName { get; set; } = string.Empty;
        
        /// <summary>
        /// The organization for the key (optional)
        /// </summary>
        public string? Organization { get; set; }
        
        /// <summary>
        /// The base URL for the key (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether this is the primary key for the provider
        /// </summary>
        public bool IsPrimary { get; set; }
        
        /// <summary>
        /// Whether the key is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// The provider account group (optional)
        /// </summary>
        public int? ProviderAccountGroup { get; set; }
    }

    /// <summary>
    /// Request model for updating a key credential
    /// </summary>
    public class UpdateKeyRequest
    {
        /// <summary>
        /// The new name for the key (optional)
        /// </summary>
        public string? KeyName { get; set; }
        
        /// <summary>
        /// The new API key (optional)
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// The new organization (optional)
        /// </summary>
        public string? Organization { get; set; }
        
        /// <summary>
        /// The new base URL (optional)
        /// </summary>
        public string? BaseUrl { get; set; }
        
        /// <summary>
        /// Whether this should be the primary key (optional)
        /// </summary>
        public bool? IsPrimary { get; set; }
        
        /// <summary>
        /// Whether the key is enabled (optional)
        /// </summary>
        public bool? IsEnabled { get; set; }
        
        /// <summary>
        /// The provider account group (optional)
        /// </summary>
        public int? ProviderAccountGroup { get; set; }
    }
}