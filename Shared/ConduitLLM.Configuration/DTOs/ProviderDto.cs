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
        public int Id { get; set; }

        /// <summary>
        /// Provider type enum value
        /// </summary>
        public ProviderType ProviderType { get; set; }

        /// <summary>
        /// User-friendly name for this provider instance
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Base URL for the provider API
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;


        /// <summary>
        /// Whether this provider is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;


        /// <summary>
        /// Date when the provider was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when the provider was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}