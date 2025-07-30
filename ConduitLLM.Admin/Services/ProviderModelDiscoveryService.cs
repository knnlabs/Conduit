using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Implementation of provider-specific model discovery using sister classes.
    /// </summary>
    public class ProviderModelDiscoveryService : IProviderModelDiscovery
    {
        private readonly ILogger<ProviderModelDiscoveryService> _logger;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderModelDiscoveryService"/> class.
        /// </summary>
        /// <param name="logger">The logger for diagnostics.</param>
        public ProviderModelDiscoveryService(ILogger<ProviderModelDiscoveryService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public async Task<List<DiscoveredModel>> DiscoverModelsAsync(
            string providerName, 
            HttpClient httpClient,
            string? apiKey, 
            CancellationToken cancellationToken = default)
        {
            var normalizedProviderName = providerName.ToLowerInvariant();
            
            try
            {
                switch (normalizedProviderName)
                {
                    case "openai":
                        _logger.LogDebug("Calling OpenAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var openAIModels = await ConduitLLM.Providers.OpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAIModelDiscovery.DiscoverAsync returned {Count} models", openAIModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in openAIModels)
                        {
                            model.Provider = providerName;
                        }
                        return openAIModels;
                        
                    case "groq":
                        _logger.LogDebug("Calling GroqModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var groqModels = await ConduitLLM.Providers.GroqModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("GroqModels.DiscoverAsync returned {Count} models", groqModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in groqModels)
                        {
                            model.Provider = providerName;
                        }
                        return groqModels;
                        
                    case "anthropic":
                        var anthropicModels = await ConduitLLM.Providers.AnthropicModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in anthropicModels)
                        {
                            model.Provider = providerName;
                        }
                        return anthropicModels;
                        
                    case "openrouter":
                        var openRouterModels = await ConduitLLM.Providers.OpenRouterModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in openRouterModels)
                        {
                            model.Provider = providerName;
                        }
                        return openRouterModels;
                    
                    case "cerebras":
                        _logger.LogDebug("Calling CerebrasModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var cerebrasModels = await ConduitLLM.Providers.CerebrasModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("CerebrasModels.DiscoverAsync returned {Count} models", cerebrasModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in cerebrasModels)
                        {
                            model.Provider = providerName;
                        }
                        return cerebrasModels;
                    
                    case "google":
                    case "gemini":
                        _logger.LogDebug("Calling GoogleModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var googleModels = await ConduitLLM.Providers.GoogleModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("GoogleModels.DiscoverAsync returned {Count} models", googleModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in googleModels)
                        {
                            model.Provider = providerName;
                        }
                        return googleModels;
                    
                    case "minimax":
                        _logger.LogDebug("Calling MiniMaxModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var miniMaxModels = await ConduitLLM.Providers.MiniMaxModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("MiniMaxModels.DiscoverAsync returned {Count} models", miniMaxModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in miniMaxModels)
                        {
                            model.Provider = providerName;
                        }
                        return miniMaxModels;
                    
                    case "replicate":
                        _logger.LogDebug("Calling ReplicateModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var replicateModels = await ConduitLLM.Providers.ReplicateModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("ReplicateModels.DiscoverAsync returned {Count} models", replicateModels.Count);
                        foreach (var model in replicateModels)
                        {
                            model.Provider = providerName;
                        }
                        return replicateModels;
                    
                    case "mistral":
                        _logger.LogDebug("Calling MistralModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var mistralModels = await ConduitLLM.Providers.MistralModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("MistralModels.DiscoverAsync returned {Count} models", mistralModels.Count);
                        foreach (var model in mistralModels)
                        {
                            model.Provider = providerName;
                        }
                        return mistralModels;
                    
                    case "cohere":
                        _logger.LogDebug("Calling CohereModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var cohereModels = await ConduitLLM.Providers.CohereModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("CohereModels.DiscoverAsync returned {Count} models", cohereModels.Count);
                        foreach (var model in cohereModels)
                        {
                            model.Provider = providerName;
                        }
                        return cohereModels;
                    
                    case "azureopenai":
                        _logger.LogDebug("Calling AzureOpenAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var azureModels = await ConduitLLM.Providers.AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("AzureOpenAIModelDiscovery.DiscoverAsync returned {Count} models", azureModels.Count);
                        foreach (var model in azureModels)
                        {
                            model.Provider = providerName;
                        }
                        return azureModels;
                    
                    case "bedrock":
                        _logger.LogDebug("Calling BedrockModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var bedrockModels = await ConduitLLM.Providers.BedrockModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("BedrockModelDiscovery.DiscoverAsync returned {Count} models", bedrockModels.Count);
                        foreach (var model in bedrockModels)
                        {
                            model.Provider = providerName;
                        }
                        return bedrockModels;
                    
                    case "vertexai":
                        _logger.LogDebug("Calling VertexAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var vertexModels = await ConduitLLM.Providers.VertexAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("VertexAIModelDiscovery.DiscoverAsync returned {Count} models", vertexModels.Count);
                        foreach (var model in vertexModels)
                        {
                            model.Provider = providerName;
                        }
                        return vertexModels;
                    
                    case "ollama":
                        _logger.LogDebug("Calling OllamaModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var ollamaModels = await ConduitLLM.Providers.OllamaModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OllamaModelDiscovery.DiscoverAsync returned {Count} models", ollamaModels.Count);
                        foreach (var model in ollamaModels)
                        {
                            model.Provider = providerName;
                        }
                        return ollamaModels;
                    
                    case "fireworks":
                        _logger.LogDebug("Calling FireworksModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var fireworksModels = await ConduitLLM.Providers.FireworksModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("FireworksModels.DiscoverAsync returned {Count} models", fireworksModels.Count);
                        foreach (var model in fireworksModels)
                        {
                            model.Provider = providerName;
                        }
                        return fireworksModels;
                    
                    default:
                        _logger.LogDebug("No provider-specific discovery available for {Provider}", providerName);
                        return new List<DiscoveredModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider {Provider}", providerName);
                return new List<DiscoveredModel>();
            }
        }
        
        /// <inheritdoc/>
        public bool SupportsDiscovery(string providerName)
        {
            var normalizedProviderName = providerName.ToLowerInvariant();
            
            // List of providers that have been migrated to sister classes
            var supportedProviders = new[]
            {
                "openai",
                "groq",
                "anthropic",
                "openrouter",
                "cerebras",
                "google",
                "gemini",
                "minimax",
                "replicate",
                "mistral",
                "cohere",
                "azureopenai",
                "bedrock",
                "vertexai",
                "ollama",
                "fireworks"
                // TODO: Add other providers as they are migrated
            };
            
            var supported = supportedProviders.Contains(normalizedProviderName);
            _logger.LogInformation("SupportsDiscovery called for provider '{Provider}' (normalized: '{Normalized}'): {Supported}", 
                providerName, normalizedProviderName, supported);
            
            return supported;
        }
    }
}