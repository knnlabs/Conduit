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
        // Load configuration synchronously (already loaded in ExecuteAsync, this is just for client creation)
        var configuration = _configurationRepository.GetByIdAsync(functionConfigurationId).GetAwaiter().GetResult();
        if (configuration == null)
        {
            throw new InvalidOperationException($"Function configuration {functionConfigurationId} not found");
        }

        // Get credentials for this provider type
        var credentials = _credentialRepository.GetByProviderTypeAsync(configuration.ProviderType)
            .GetAwaiter().GetResult();

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
