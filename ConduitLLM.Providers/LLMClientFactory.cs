using System;
using System.Linq;

using ConduitLLM.Configuration;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Interfaces; // Use the original interface location

using Microsoft.Extensions.Logging; // Added for ILoggerFactory
using Microsoft.Extensions.Options;

namespace ConduitLLM.Providers;

/// <summary>
/// Factory responsible for creating ILLMClient instances based on configuration.
/// </summary>
public class LLMClientFactory : ILLMClientFactory
{
    private readonly ConduitSettings _settings;
    private readonly ILoggerFactory _loggerFactory; // Added logger factory

    /// <summary>
    /// Initializes a new instance of the <see cref="LLMClientFactory"/> class.
    /// </summary>
    /// <param name="settingsOptions">The configuration settings.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public LLMClientFactory(IOptions<ConduitSettings> settingsOptions, ILoggerFactory loggerFactory)
    {
        _settings = settingsOptions.Value ?? throw new ArgumentNullException(nameof(settingsOptions), "Conduit settings cannot be null.");
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); // Store logger factory
    }

    /// <inheritdoc />
    public ILLMClient GetClient(string modelAlias)
    {
        if (string.IsNullOrWhiteSpace(modelAlias))
        {
            throw new ArgumentException("Model alias cannot be null or whitespace.", nameof(modelAlias));
        }

        // 1. Find the model mapping
        var mapping = _settings.ModelMappings?.FirstOrDefault(m =>
            string.Equals(m.ModelAlias, modelAlias, StringComparison.OrdinalIgnoreCase));

        if (mapping == null)
        {
            throw new ConfigurationException($"No model mapping found for alias '{modelAlias}'. Please check your Conduit configuration.");
        }

        // 2. Find the provider credentials using the ProviderName from the mapping
        var credentials = _settings.ProviderCredentials?.FirstOrDefault(p =>
            string.Equals(p.ProviderName, mapping.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{mapping.ProviderName}' (required by model alias '{modelAlias}'). Please check your Conduit configuration.");
        }

        // 3. Instantiate the appropriate client based on ProviderName
        return CreateClientForProvider(mapping.ProviderName, credentials, mapping.ProviderModelId);
    }

    /// <inheritdoc />
    public ILLMClient GetClientByProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be null or whitespace.", nameof(providerName));
        }

        // Find the provider credentials
        var credentials = _settings.ProviderCredentials?.FirstOrDefault(p =>
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (credentials == null)
        {
            throw new ConfigurationException($"No provider credentials found for provider '{providerName}'. Please check your Conduit configuration.");
        }

        // Use a default model ID for operations that don't require a specific model
        // This is mainly for operations like listing available models
        return CreateClientForProvider(providerName, credentials, "default-model-id");
    }

    private ILLMClient CreateClientForProvider(string providerName, ProviderCredentials credentials, string modelId)
    {
        switch (providerName.ToLowerInvariant())
        {
            case "openai":
                // Create and pass logger
                var openAiLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                return new OpenAIClient(credentials, modelId, openAiLogger);

            case "anthropic":
                 // Create and pass logger
                 var anthropicLogger = _loggerFactory.CreateLogger<AnthropicClient>();
                 return new AnthropicClient(credentials, modelId, anthropicLogger);

            // OpenAI-Compatible APIs
            case "openrouter":
                 var openRouterLogger = _loggerFactory.CreateLogger<OpenRouterClient>();
                 return new OpenRouterClient(credentials, openRouterLogger);

            // Other OpenAI-Compatible APIs (excluding OpenRouter now)
            case "fireworks": // Alias
            case "fireworksai":
            case "mistral":
            case "mistralai": // Alias
                 var compatibleLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                 // Credentials should contain the correct ApiBase (e.g., https://api.mistral.ai/) and ApiKey for the specific provider
                 return new OpenAIClient(credentials, modelId, compatibleLogger);

            case "google":
            case "gemini": // Alias
                 var geminiLogger = _loggerFactory.CreateLogger<GeminiClient>();
                 return new GeminiClient(credentials, modelId, geminiLogger);

            case "azure":
                 var azureLogger = _loggerFactory.CreateLogger<OpenAIClient>();
                 // Instantiate the modified OpenAIClient, passing "azure" as the provider name
                 return new OpenAIClient(credentials, modelId, azureLogger, providerName: "azure");

            case "cohere":
                 var cohereLogger = _loggerFactory.CreateLogger<CohereClient>();
                 return new CohereClient(credentials, modelId, cohereLogger);

            // Add cases for other providers as they are implemented

            default:
                throw new UnsupportedProviderException($"Provider '{providerName}' is not currently supported by ConduitLLM.");
        }
    }
}
