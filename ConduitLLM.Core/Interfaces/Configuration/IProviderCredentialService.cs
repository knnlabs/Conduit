namespace ConduitLLM.Core.Interfaces.Configuration
{
    /// <summary>
    /// Service interface for retrieving provider credentials.
    /// </summary>
    public interface IProviderCredentialService
    {
        /// <summary>
        /// Retrieves credentials for a specific provider.
        /// </summary>
        /// <param name="providerName">The name of the provider.</param>
        /// <returns>Provider credentials if found, otherwise null.</returns>
        [Obsolete("Use GetCredentialByIdAsync instead. Provider names are error-prone and will be deprecated.")]
        Task<ProviderCredentials?> GetCredentialByProviderNameAsync(string providerName);
        
        /// <summary>
        /// Retrieves credentials for a specific provider by ID.
        /// </summary>
        /// <param name="providerId">The ID of the provider.</param>
        /// <returns>Provider credentials if found, otherwise null.</returns>
        Task<ProviderCredentials?> GetCredentialByIdAsync(int providerId);
    }

    /// <summary>
    /// Represents credentials for an LLM provider.
    /// </summary>
    public class ProviderCredentials
    {
        /// <summary>
        /// The unique identifier of the provider.
        /// </summary>
        public int ProviderId { get; set; }
        
        /// <summary>
        /// The name of the provider.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// The API key for the provider.
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Optional base URL if different from default.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Optional API version.
        /// </summary>
        public string? ApiVersion { get; set; }

        /// <summary>
        /// Whether the credential is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
