using System;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for creating a new provider credential
    /// </summary>
    public class CreateProviderCredentialDto
    {
        /// <summary>
        /// Provider type enum value
        /// </summary>
        [Required]
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// User-friendly name for this provider instance
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string? BaseUrl { get; set; }


        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Optional API key to create as the initial key for this provider
        /// </summary>
        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// Data transfer object for updating a provider credential
    /// </summary>
    public class UpdateProviderCredentialDto
    {
        /// <summary>
        /// Unique identifier for the provider credential
        /// </summary>
        [Required]
        public int Id { get; set; }

        /// <summary>
        /// User-friendly name for this provider instance
        /// </summary>
        [MaxLength(100)]
        public string? ProviderName { get; set; }

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string? BaseUrl { get; set; }


        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? Organization { get; set; }
    }

    /// <summary>
    /// Data transfer object for a simplified provider data representation
    /// </summary>
    public class ProviderInfoDto
    {
        /// <summary>
        /// Unique identifier for the provider
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Provider type enum value
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// User-friendly name for this provider instance
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
    }
}
