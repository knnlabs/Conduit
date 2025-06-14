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
        /// Name of the provider (e.g., "openai", "anthropic")
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string? ApiBase { get; set; }

        /// <summary>
        /// API key or other authentication token
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Optional model endpoint for providers with custom endpoints
        /// </summary>
        public string? ModelEndpoint { get; set; }

        /// <summary>
        /// Optional additional configuration for the provider
        /// </summary>
        public string? AdditionalConfig { get; set; }
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
        /// Base URL for the provider API
        /// </summary>
        public string? ApiBase { get; set; }

        /// <summary>
        /// API key or other authentication token
        /// </summary>
        /// <remarks>
        /// If null or empty, the existing API key will be preserved.
        /// To clear an API key, use the special value "[REMOVE]".
        /// </remarks>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Optional model endpoint for providers with custom endpoints
        /// </summary>
        public string? ModelEndpoint { get; set; }

        /// <summary>
        /// Optional additional configuration for the provider
        /// </summary>
        public string? AdditionalConfig { get; set; }
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
        /// Name of the provider
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;
    }
}
