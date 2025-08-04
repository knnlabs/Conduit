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
            Provider Provider, 
            HttpClient httpClient,
            string? apiKey = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting model discovery for provider '{ProviderName}' (ID: {ProviderId}, Type: {ProviderType})", 
                    Provider.ProviderName, Provider.Id, Provider.ProviderType);
                
                // If no API key provided, try to get it from the credential's keys
                if (string.IsNullOrEmpty(apiKey) && Provider.ProviderKeyCredentials?.Any() == true)
                {
                    var primaryKey = Provider.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsPrimary && k.IsEnabled) ?? 
                        Provider.ProviderKeyCredentials
                        .FirstOrDefault(k => k.IsEnabled);
                    
                    if (primaryKey != null)
                    {
                        apiKey = primaryKey.ApiKey;
                        _logger.LogDebug("Using API key from provider credential");
                    }
                }
                
                // If provider has a custom base URL, configure the HTTP client
                if (!string.IsNullOrEmpty(Provider.BaseUrl))
                {
                    _logger.LogDebug("Provider has custom base URL: {BaseUrl}", Provider.BaseUrl);
                    // Note: The individual discovery methods should handle custom base URLs
                }
                
                switch (Provider.ProviderType)
                {
                    case ProviderType.OpenAI:
                        _logger.LogDebug("Calling OpenAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var openAIModels = await ConduitLLM.Providers.OpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAIModelDiscovery.DiscoverAsync returned {Count} models", openAIModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in openAIModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return openAIModels;
                        
                    case ProviderType.Groq:
                        _logger.LogDebug("Calling GroqModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var groqModels = await ConduitLLM.Providers.GroqModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("GroqModels.DiscoverAsync returned {Count} models", groqModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in groqModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return groqModels;
                        
                    case ProviderType.Anthropic:
                        var anthropicModels = await ConduitLLM.Providers.AnthropicModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in anthropicModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return anthropicModels;
                        
                    case ProviderType.OpenRouter:
                        var openRouterModels = await ConduitLLM.Providers.OpenRouterModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        foreach (var model in openRouterModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return openRouterModels;
                    
                    case ProviderType.Cerebras:
                        _logger.LogDebug("Calling CerebrasModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var cerebrasModels = await ConduitLLM.Providers.CerebrasModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("CerebrasModels.DiscoverAsync returned {Count} models", cerebrasModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in cerebrasModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return cerebrasModels;
                    
                    case ProviderType.Gemini:
                        _logger.LogDebug("Calling GoogleModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var googleModels = await ConduitLLM.Providers.GoogleModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("GoogleModels.DiscoverAsync returned {Count} models", googleModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in googleModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return googleModels;
                    
                    case ProviderType.MiniMax:
                        _logger.LogDebug("Calling MiniMaxModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var miniMaxModels = await ConduitLLM.Providers.MiniMaxModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("MiniMaxModels.DiscoverAsync returned {Count} models", miniMaxModels.Count);
                        // Ensure provider field is set correctly
                        foreach (var model in miniMaxModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return miniMaxModels;
                    
                    case ProviderType.Replicate:
                        _logger.LogDebug("Calling ReplicateModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var replicateModels = await ConduitLLM.Providers.ReplicateModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("ReplicateModels.DiscoverAsync returned {Count} models", replicateModels.Count);
                        foreach (var model in replicateModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return replicateModels;
                    
                    case ProviderType.Mistral:
                        _logger.LogDebug("Calling MistralModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var mistralModels = await ConduitLLM.Providers.MistralModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("MistralModels.DiscoverAsync returned {Count} models", mistralModels.Count);
                        foreach (var model in mistralModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return mistralModels;
                    
                    case ProviderType.Cohere:
                        _logger.LogDebug("Calling CohereModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var cohereModels = await ConduitLLM.Providers.CohereModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("CohereModels.DiscoverAsync returned {Count} models", cohereModels.Count);
                        foreach (var model in cohereModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return cohereModels;
                    
                    case ProviderType.AzureOpenAI:
                        _logger.LogDebug("Calling AzureOpenAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var azureModels = await ConduitLLM.Providers.AzureOpenAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("AzureOpenAIModelDiscovery.DiscoverAsync returned {Count} models", azureModels.Count);
                        foreach (var model in azureModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return azureModels;
                    
                    case ProviderType.Bedrock:
                        _logger.LogDebug("Calling BedrockModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var bedrockModels = await ConduitLLM.Providers.BedrockModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("BedrockModelDiscovery.DiscoverAsync returned {Count} models", bedrockModels.Count);
                        foreach (var model in bedrockModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return bedrockModels;
                    
                    case ProviderType.VertexAI:
                        _logger.LogDebug("Calling VertexAIModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var vertexModels = await ConduitLLM.Providers.VertexAIModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("VertexAIModelDiscovery.DiscoverAsync returned {Count} models", vertexModels.Count);
                        foreach (var model in vertexModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return vertexModels;
                    
                    case ProviderType.Ollama:
                        _logger.LogDebug("Calling OllamaModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var ollamaModels = await ConduitLLM.Providers.OllamaModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OllamaModelDiscovery.DiscoverAsync returned {Count} models", ollamaModels.Count);
                        foreach (var model in ollamaModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return ollamaModels;
                    
                    case ProviderType.Fireworks:
                        _logger.LogDebug("Calling FireworksModels.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var fireworksModels = await ConduitLLM.Providers.FireworksModels.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("FireworksModels.DiscoverAsync returned {Count} models", fireworksModels.Count);
                        foreach (var model in fireworksModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return fireworksModels;
                    
                    case ProviderType.HuggingFace:
                        _logger.LogDebug("Calling HuggingFaceModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var huggingFaceModels = await ConduitLLM.Providers.HuggingFaceModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("HuggingFaceModelDiscovery.DiscoverAsync returned {Count} models", huggingFaceModels.Count);
                        foreach (var model in huggingFaceModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return huggingFaceModels;
                    
                    case ProviderType.SageMaker:
                        _logger.LogDebug("Calling SageMakerModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var sageMakerModels = await ConduitLLM.Providers.SageMakerModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("SageMakerModelDiscovery.DiscoverAsync returned {Count} models", sageMakerModels.Count);
                        foreach (var model in sageMakerModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return sageMakerModels;
                    
                    case ProviderType.OpenAICompatible:
                        _logger.LogDebug("Calling OpenAICompatibleModelDiscovery.DiscoverAsync with API key: {HasKey}", !string.IsNullOrEmpty(apiKey));
                        var openAICompatibleModels = await ConduitLLM.Providers.OpenAICompatibleModelDiscovery.DiscoverAsync(httpClient, apiKey, cancellationToken);
                        _logger.LogDebug("OpenAICompatibleModelDiscovery.DiscoverAsync returned {Count} models", openAICompatibleModels.Count);
                        foreach (var model in openAICompatibleModels)
                        {
                            model.Provider = Provider.ProviderName;
                        }
                        return openAICompatibleModels;
                    
                    default:
                        _logger.LogDebug("No provider-specific discovery available for {ProviderType}", Provider.ProviderType);
                        return new List<DiscoveredModel>();
                }
            }
            catch (NotSupportedException)
            {
                // Rethrow NotSupportedException so it can be handled by the controller
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider '{ProviderName}' (Type: {ProviderType})", 
                    Provider.ProviderName, Provider.ProviderType);
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