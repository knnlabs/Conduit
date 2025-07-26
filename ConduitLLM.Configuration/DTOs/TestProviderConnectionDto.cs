namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for testing provider connection
    /// </summary>
    public class TestProviderConnectionDto
    {
        /// <summary>
        /// Provider type enum value
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// API key for authentication
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Optional organization ID for providers that support it
        /// </summary>
        public string? OrganizationId { get; set; }

        /// <summary>
        /// Additional provider-specific configuration
        /// </summary>
        public ProviderSettings? AdditionalConfig { get; set; }
    }
}