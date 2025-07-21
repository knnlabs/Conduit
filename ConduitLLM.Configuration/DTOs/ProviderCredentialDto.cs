using System;

namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for provider credentials
    /// </summary>
    public class ProviderCredentialDto
    {
        /// <summary>
        /// Unique identifier for the provider credential
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Name of the provider (e.g., "openai", "anthropic")
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string ApiBase { get; set; } = string.Empty;

        /// <summary>
        /// API key or other authentication token (always masked in responses)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? Organization { get; set; }

        /// <summary>
        /// Date when the credential was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the credential was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
