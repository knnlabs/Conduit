using ConduitLLM.Core.Exceptions; // Need this for exception documentation

namespace ConduitLLM.Core.Interfaces; // Restored original namespace

/// <summary>
/// Defines the contract for a factory that creates instances of ILLMClient based on configuration.
/// </summary>
public interface ILLMClientFactory
{
    /// <summary>
    /// Gets an appropriate ILLMClient instance for the specified model alias based on the loaded configuration.
    /// </summary>
    /// <param name="modelAlias">The model alias specified in the request (e.g., "gpt-4-turbo").</param>
    /// <returns>An instance of ILLMClient capable of handling the request for the specified model.</returns>
    /// <exception cref="ConfigurationException">Thrown if the configuration for the model alias or its provider is invalid or missing.</exception>
    /// <exception cref="UnsupportedProviderException">Thrown if the provider specified in the configuration is not supported by this factory.</exception>
    ILLMClient GetClient(string modelAlias);

    
    /// <summary>
    /// Gets an ILLMClient instance for the specified provider ID directly.
    /// </summary>
    /// <param name="providerId">The ID of the provider.</param>
    /// <returns>An instance of ILLMClient for the specified provider.</returns>
    /// <exception cref="ConfigurationException">Thrown if the configuration for the provider is invalid or missing.</exception>
    /// <exception cref="UnsupportedProviderException">Thrown if the specified provider is not supported by this factory.</exception>
    ILLMClient GetClientByProviderId(int providerId);

    /// <summary>
    /// Gets provider metadata for the specified provider type without requiring credentials.
    /// </summary>
    /// <param name="providerType">The provider type to get metadata for.</param>
    /// <returns>Provider metadata if the provider implements IProviderMetadata, null otherwise.</returns>
    IProviderMetadata? GetProviderMetadata(ConduitLLM.Configuration.ProviderType providerType);

    /// <summary>
    /// Gets an ILLMClient instance for the specified provider type directly.
    /// This method looks up the provider by its enum type rather than database ID.
    /// </summary>
    /// <param name="providerType">The provider type enum value.</param>
    /// <returns>An instance of ILLMClient for the specified provider type.</returns>
    /// <exception cref="ConfigurationException">Thrown if the configuration for the provider is invalid or missing.</exception>
    /// <exception cref="UnsupportedProviderException">Thrown if the specified provider type is not supported by this factory.</exception>
    ILLMClient GetClientByProviderType(ConduitLLM.Configuration.ProviderType providerType);
}
