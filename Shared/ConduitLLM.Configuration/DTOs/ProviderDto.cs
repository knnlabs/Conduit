namespace ConduitLLM.Configuration.DTOs
{
    /// <summary>
    /// Data transfer object for providers
    /// </summary>
    public class ProviderDto
    {
        /// <summary>
        /// Unique identifier for the provider
        /// </summary>
        public required int Id { get; set; }

        /// <summary>
        /// Provider type enum value
        /// </summary>
        public required ProviderType ProviderType { get; set; }

        /// <summary>
        /// User-friendly name for this provider instance
        /// </summary>
        public required string ProviderName { get; set; }

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>Number of configured credentials for this provider.</summary>
        public required int KeyCount { get; set; }

        /// <summary>Whether provider-reported costs are authoritative.</summary>
        public required bool TrustProviderReportedCosts { get; set; }

        /// <summary>Markup applied to provider-reported costs.</summary>
        public required decimal ProviderCostMarkupMultiplier { get; set; }


        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public required bool IsEnabled { get; set; }


        /// <summary>
        /// Date when the provider was created
        /// </summary>
        public required DateTime CreatedAt { get; set; }

        /// <summary>
        /// Date when the provider was last updated
        /// </summary>
        public required DateTime UpdatedAt { get; set; }
    }
}
