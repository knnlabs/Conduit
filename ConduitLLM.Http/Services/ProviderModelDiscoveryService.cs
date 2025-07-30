using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
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
                        var openAIModels = await ConduitLLM.Providers.OpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        // Ensure provider field is set correctly
                        foreach (var model in openAIModels)
                        {
                            model.Provider = providerName;
                        }
                        return openAIModels;
                        
                    case "groq":
                        var groqModels = await ConduitLLM.Providers.GroqModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
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
                        var cerebrasModels = await ConduitLLM.Providers.CerebrasModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in cerebrasModels)
                        {
                            model.Provider = providerName;
                        }
                        return cerebrasModels;
                    
                    case "google":
                    case "gemini":
                        var googleModels = await ConduitLLM.Providers.GoogleModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in googleModels)
                        {
                            model.Provider = providerName;
                        }
                        return googleModels;
                    
                    case "minimax":
                        var miniMaxModels = await ConduitLLM.Providers.MiniMaxModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in miniMaxModels)
                        {
                            model.Provider = providerName;
                        }
                        return miniMaxModels;
                    
                    case "replicate":
                        var replicateModels = await ConduitLLM.Providers.ReplicateModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in replicateModels)
                        {
                            model.Provider = providerName;
                        }
                        return replicateModels;
                    
                    case "mistral":
                        var mistralModels = await ConduitLLM.Providers.MistralModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in mistralModels)
                        {
                            model.Provider = providerName;
                        }
                        return mistralModels;
                    
                    case "cohere":
                        var cohereModels = await ConduitLLM.Providers.CohereModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in cohereModels)
                        {
                            model.Provider = providerName;
                        }
                        return cohereModels;
                    
                    case "azureopenai":
                        var azureModels = await ConduitLLM.Providers.AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in azureModels)
                        {
                            model.Provider = providerName;
                        }
                        return azureModels;
                    
                    case "bedrock":
                        var bedrockModels = await ConduitLLM.Providers.BedrockModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in bedrockModels)
                        {
                            model.Provider = providerName;
                        }
                        return bedrockModels;
                    
                    case "vertexai":
                        var vertexModels = await ConduitLLM.Providers.VertexAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in vertexModels)
                        {
                            model.Provider = providerName;
                        }
                        return vertexModels;
                    
                    case "ollama":
                        var ollamaModels = await ConduitLLM.Providers.OllamaModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in ollamaModels)
                        {
                            model.Provider = providerName;
                        }
                        return ollamaModels;
                    
                    case "fireworks":
                        var fireworksModels = await ConduitLLM.Providers.FireworksModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in fireworksModels)
                        {
                            model.Provider = providerName;
                        }
                        return fireworksModels;
                    
                    case "huggingface":
                        var huggingFaceModels = await ConduitLLM.Providers.HuggingFaceModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in huggingFaceModels)
                        {
                            model.Provider = providerName;
                        }
                        return huggingFaceModels;
                    
                    case "sagemaker":
                        var sageMakerModels = await ConduitLLM.Providers.SageMakerModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in sageMakerModels)
                        {
                            model.Provider = providerName;
                        }
                        return sageMakerModels;
                    
                    case "openaicompatible":
                        var openAICompatibleModels = await ConduitLLM.Providers.OpenAICompatibleModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in openAICompatibleModels)
                        {
                            model.Provider = providerName;
                        }
                        return openAICompatibleModels;
                    
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
                "fireworks",
                "huggingface",
                "sagemaker",
                "openaicompatible"
            };
            
            return supportedProviders.Contains(normalizedProviderName);
        }
    }
}