using ConduitLLM.Functions.Enums;
using ConduitLLM.Functions.Interfaces;
using ConduitLLM.Functions.Providers.Exa;
using ConduitLLM.Functions.Providers.Tavily;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Functions.Services;

/// <summary>
/// Factory for creating function provider clients.
/// </summary>
/// <remarks>
/// This factory creates provider-specific client instances based on configuration.
/// Similar to LLMClientFactory, but for function providers.
/// </remarks>
public class FunctionClientFactory : IFunctionClientFactory
{
    private readonly IFunctionConfigurationRepository _configurationRepository;
    private readonly IFunctionCredentialRepository _credentialRepository;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public FunctionClientFactory(
        IFunctionConfigurationRepository configurationRepository,
        IFunctionCredentialRepository credentialRepository,
        ILoggerFactory loggerFactory,
        IHttpClientFactory? httpClientFactory = null)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _credentialRepository = credentialRepository ?? throw new ArgumentNullException(nameof(credentialRepository));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public IFunctionClient GetClient(FunctionProviderType providerType, int functionConfigurationId)
    {
        // Prefer using GetClientAsync for non-blocking operation
        return GetClientAsync(providerType, functionConfigurationId).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets a function client asynchronously.
    /// </summary>
    /// <param name="providerType">The type of function provider.</param>
    /// <param name="functionConfigurationId">The function configuration ID.</param>
    /// <returns>The function client for the specified provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration or credentials are not found.</exception>
    /// <exception cref="NotSupportedException">Thrown when the provider type is not supported.</exception>
    public async Task<IFunctionClient> GetClientAsync(FunctionProviderType providerType, int functionConfigurationId)
    {
        var configuration = await _configurationRepository.GetByIdAsync(functionConfigurationId);
        if (configuration == null)
        {
            throw new InvalidOperationException($"Function configuration {functionConfigurationId} not found");
        }

        // Get credentials for this provider type
        var credentials = await _credentialRepository.GetByProviderTypeAsync(configuration.ProviderType);

        var credential = credentials.FirstOrDefault(c => c.IsEnabled);
        if (credential == null)
        {
            throw new InvalidOperationException(
                $"No enabled credentials found for provider type {configuration.ProviderType}");
        }

        // Create provider-specific client
        return providerType switch
        {
            FunctionProviderType.Exa => CreateExaClient(configuration, credential),
            FunctionProviderType.Tavily => CreateTavilyClient(configuration, credential),
            _ => throw new NotSupportedException($"Function provider type '{providerType}' is not supported")
        };
    }

    /// <summary>
    /// Creates an Exa client instance.
    /// </summary>
    private ExaClient CreateExaClient(
        ConduitLLM.Functions.Entities.FunctionConfiguration configuration,
        ConduitLLM.Functions.Entities.FunctionCredential credential)
    {
        var logger = _loggerFactory.CreateLogger<ExaClient>();

        return new ExaClient(
            configuration,
            credential,
            _httpClientFactory,
            logger);
    }

    /// <summary>
    /// Creates a Tavily client instance.
    /// </summary>
    private TavilyClient CreateTavilyClient(
        ConduitLLM.Functions.Entities.FunctionConfiguration configuration,
        ConduitLLM.Functions.Entities.FunctionCredential credential)
    {
        var logger = _loggerFactory.CreateLogger<TavilyClient>();

        return new TavilyClient(
            configuration,
            credential,
            _httpClientFactory,
            logger);
    }
}
