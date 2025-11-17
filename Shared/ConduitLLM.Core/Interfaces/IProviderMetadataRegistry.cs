using ConduitLLM.Configuration;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Registry for managing provider metadata across the application.
    /// Provides a single source of truth for provider capabilities and configuration.
    /// </summary>
    public interface IProviderMetadataRegistry
    {
        /// <summary>
        /// Gets metadata for a specific provider type.
        /// </summary>
        /// <param name="providerType">The provider type to retrieve metadata for</param>
        /// <returns>The provider metadata</returns>
        /// <exception cref="ProviderNotFoundException">Thrown when the provider type is not registered</exception>
        IProviderMetadata GetMetadata(ProviderType providerType);

        /// <summary>
        /// Tries to get metadata for a specific provider type.
        /// </summary>
        /// <param name="providerType">The provider type to retrieve metadata for</param>
        /// <param name="metadata">The provider metadata if found</param>
        /// <returns>True if the provider was found, false otherwise</returns>
        bool TryGetMetadata(ProviderType providerType, out IProviderMetadata? metadata);

        /// <summary>
        /// Gets metadata for all registered providers.
        /// </summary>
        /// <returns>An enumerable of all provider metadata</returns>
        IEnumerable<IProviderMetadata> GetAllMetadata();

        /// <summary>
        /// Checks if a provider type is registered.
        /// </summary>
        /// <param name="providerType">The provider type to check</param>
        /// <returns>True if the provider is registered, false otherwise</returns>
        bool IsRegistered(ProviderType providerType);

        /// <summary>
        /// Gets providers that support a specific feature.
        /// </summary>
        /// <param name="featurePredicate">Predicate to test provider features</param>
        /// <returns>Providers that match the feature predicate</returns>
        IEnumerable<IProviderMetadata> GetProvidersByFeature(Func<Models.FeatureSupport, bool> featurePredicate);

        /// <summary>
        /// Gets diagnostic information about the registry.
        /// Useful for debugging and health checks.
        /// </summary>
        /// <returns>Diagnostic information including registered providers and their capabilities</returns>
        ProviderRegistryDiagnostics GetDiagnostics();
    }

    /// <summary>
    /// Contains diagnostic information about the provider registry.
    /// </summary>
    public class ProviderRegistryDiagnostics
    {
        /// <summary>
        /// Gets or sets the total number of registered providers.
        /// </summary>
        public int TotalProviders { get; set; }

        /// <summary>
        /// Gets or sets the list of registered provider types.
        /// </summary>
        public List<string> RegisteredProviders { get; set; } = new();

        /// <summary>
        /// Gets or sets providers grouped by capabilities.
        /// </summary>
        public Dictionary<string, List<string>> ProvidersByCapability { get; set; } = new();

        /// <summary>
        /// Gets or sets any registration errors encountered.
        /// </summary>
        public List<string> RegistrationErrors { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp when diagnostics were generated.
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Exception thrown when a provider is not found in the registry.
    /// </summary>
    public class ProviderNotFoundException : Exception
    {
        /// <summary>
        /// Gets the provider type that was not found.
        /// </summary>
        public ProviderType ProviderType { get; }

        /// <summary>
        /// Initializes a new instance of the ProviderNotFoundException class.
        /// </summary>
        public ProviderNotFoundException(ProviderType providerType)
            : base($"Provider '{providerType}' is not registered in the provider registry.")
        {
            ProviderType = providerType;
        }

        /// <summary>
        /// Initializes a new instance of the ProviderNotFoundException class with a custom message.
        /// </summary>
        public ProviderNotFoundException(ProviderType providerType, string message)
            : base(message)
        {
            ProviderType = providerType;
        }

        /// <summary>
        /// Initializes a new instance of the ProviderNotFoundException class with an inner exception.
        /// </summary>
        public ProviderNotFoundException(ProviderType providerType, string message, Exception innerException)
            : base(message, innerException)
        {
            ProviderType = providerType;
        }
    }
}