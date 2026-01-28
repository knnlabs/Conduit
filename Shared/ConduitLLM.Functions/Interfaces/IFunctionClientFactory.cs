using ConduitLLM.Functions.Enums;

namespace ConduitLLM.Functions.Interfaces;

/// <summary>
/// Factory interface for creating function provider clients.
/// </summary>
public interface IFunctionClientFactory
{
    /// <summary>
    /// Gets a function client for the specified provider type.
    /// </summary>
    /// <param name="providerType">The function provider type.</param>
    /// <param name="functionConfigurationId">The function configuration ID.</param>
    /// <returns>A function client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid or provider is unsupported.</exception>
    IFunctionClient GetClient(FunctionProviderType providerType, int functionConfigurationId);

    /// <summary>
    /// Gets a function client for the specified provider type asynchronously.
    /// </summary>
    /// <param name="providerType">The function provider type.</param>
    /// <param name="functionConfigurationId">The function configuration ID.</param>
    /// <returns>A function client instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid or provider is unsupported.</exception>
    Task<IFunctionClient> GetClientAsync(FunctionProviderType providerType, int functionConfigurationId);
}
