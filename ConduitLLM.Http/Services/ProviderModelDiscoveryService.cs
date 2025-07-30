using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
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
            ProviderCredential providerCredential, 
            HttpClient httpClient,
            string? apiKey = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting model discovery for provider '{ProviderName}' (ID: {ProviderId}, Type: {ProviderType})", 
                    providerCredential.ProviderName, providerCredential.Id, providerCredential.ProviderType);
                
                // If no API key provided, try to get it from the credential's keys
                if (string.IsNullOrEmpty(apiKey) && providerCredential.ProviderKeyCredentials?.Any() == true)
                {
                    var primaryKey = providerCredential.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ?? 
                        providerCredential.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsEnabled);
                    
                    if (primaryKey != null)
                    {
                        apiKey = primaryKey.ApiKey;
                        _logger.LogDebug("Using API key from provider credential");
                    }
                }
                
                // If provider has a custom base URL, configure the HTTP client
                if (!string.IsNullOrEmpty(providerCredential.BaseUrl))
                {
                    _logger.LogDebug("Provider has custom base URL: {BaseUrl}", providerCredential.BaseUrl);
                    // Note: The individual discovery methods should handle custom base URLs
                }
                
                switch (providerCredential.ProviderType)
                {
                    case ProviderType.OpenAI:
                        var openAIModels = await ConduitLLM.Providers.OpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        // Ensure provider field is set correctly
                        foreach (var model in openAIModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return openAIModels;
                        
                    case ProviderType.Groq:
                        var groqModels = await ConduitLLM.Providers.GroqModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        // Ensure provider field is set correctly
                        foreach (var model in groqModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return groqModels;
                        
                    case ProviderType.Anthropic:
                        var anthropicModels = await ConduitLLM.Providers.AnthropicModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in anthropicModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return anthropicModels;
                        
                    case ProviderType.OpenRouter:
                        var openRouterModels = await ConduitLLM.Providers.OpenRouterModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in openRouterModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return openRouterModels;
                    
                    case ProviderType.Cerebras:
                        var cerebrasModels = await ConduitLLM.Providers.CerebrasModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in cerebrasModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return cerebrasModels;
                    
                    case ProviderType.Gemini:
                        var googleModels = await ConduitLLM.Providers.GoogleModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in googleModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return googleModels;
                    
                    case ProviderType.MiniMax:
                        var miniMaxModels = await ConduitLLM.Providers.MiniMaxModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in miniMaxModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return miniMaxModels;
                    
                    case ProviderType.Replicate:
                        var replicateModels = await ConduitLLM.Providers.ReplicateModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in replicateModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return replicateModels;
                    
                    case ProviderType.Mistral:
                        var mistralModels = await ConduitLLM.Providers.MistralModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in mistralModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return mistralModels;
                    
                    case ProviderType.Cohere:
                        var cohereModels = await ConduitLLM.Providers.CohereModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in cohereModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return cohereModels;
                    
                    case ProviderType.AzureOpenAI:
                        var azureModels = await ConduitLLM.Providers.AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in azureModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return azureModels;
                    
                    case ProviderType.Bedrock:
                        var bedrockModels = await ConduitLLM.Providers.BedrockModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in bedrockModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return bedrockModels;
                    
                    case ProviderType.VertexAI:
                        var vertexModels = await ConduitLLM.Providers.VertexAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in vertexModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return vertexModels;
                    
                    case ProviderType.Ollama:
                        var ollamaModels = await ConduitLLM.Providers.OllamaModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in ollamaModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return ollamaModels;
                    
                    case ProviderType.Fireworks:
                        var fireworksModels = await ConduitLLM.Providers.FireworksModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in fireworksModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return fireworksModels;
                    
                    case ProviderType.HuggingFace:
                        var huggingFaceModels = await ConduitLLM.Providers.HuggingFaceModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in huggingFaceModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return huggingFaceModels;
                    
                    case ProviderType.SageMaker:
                        var sageMakerModels = await ConduitLLM.Providers.SageMakerModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in sageMakerModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return sageMakerModels;
                    
                    case ProviderType.OpenAICompatible:
                        var openAICompatibleModels = await ConduitLLM.Providers.OpenAICompatibleModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in openAICompatibleModels)
                        {
                            model.Provider = providerCredential.ProviderName;
                        }
                        return openAICompatibleModels;
                    
                    default:
                        _logger.LogDebug("No provider-specific discovery available for {ProviderType}", providerCredential.ProviderType);
                        return new List<DiscoveredModel>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider '{ProviderName}' (Type: {ProviderType})", 
                    providerCredential.ProviderName, providerCredential.ProviderType);
                return new List<DiscoveredModel>();
            }
        }
        
        /// <inheritdoc/>
        public bool SupportsDiscovery(ProviderType providerType)
        {
            // Check if this provider type has discovery support
            var supportedTypes = new[]
            {
                ProviderType.OpenAI,
                ProviderType.Groq,
                ProviderType.Anthropic,
                ProviderType.OpenRouter,
                ProviderType.Cerebras,
                ProviderType.Gemini,
                ProviderType.MiniMax,
                ProviderType.Replicate,
                ProviderType.Mistral,
                ProviderType.Cohere,
                ProviderType.AzureOpenAI,
                ProviderType.Bedrock,
                ProviderType.VertexAI,
                ProviderType.Ollama,
                ProviderType.Fireworks,
                ProviderType.HuggingFace,
                ProviderType.SageMaker,
                ProviderType.OpenAICompatible
            };
            
            var supported = supportedTypes.Contains(providerType);
            _logger.LogInformation("SupportsDiscovery called for provider type {ProviderType}: {Supported}", 
                providerType, supported);
            return supported;
        }
    }
}