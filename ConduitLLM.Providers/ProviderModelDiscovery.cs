using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

// Use alias to avoid ambiguity
using CoreModelCapabilities = ConduitLLM.Core.Interfaces.ModelCapabilities;

namespace ConduitLLM.Providers
{
    /// <summary>
    /// Implementation of provider-specific model discovery.
    /// </summary>
    public class ProviderModelDiscovery : IProviderModelDiscovery
    {
        private readonly ILogger<ProviderModelDiscovery> _logger;
        private readonly ILLMClientFactory _clientFactory;
        private readonly IProviderService _providerService;

        public ProviderModelDiscovery(
            ILogger<ProviderModelDiscovery> logger,
            ILLMClientFactory clientFactory,
            IProviderService providerService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _providerService = providerService ?? throw new ArgumentNullException(nameof(providerService));
        }

        /// <inheritdoc/>
        public async Task<List<DiscoveredModel>> DiscoverModelsAsync(
            Provider provider,
            HttpClient httpClient,
            string? apiKey = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Discovering models for provider {ProviderId} of type {ProviderType}", 
                provider.Id, provider.ProviderType);

            try
            {
                // Get the primary key credential for this provider
                var keyCredentials = await _providerService.GetKeyCredentialsByProviderIdAsync(provider.Id);
                var primaryKey = keyCredentials.FirstOrDefault(k => k.IsPrimary && k.IsEnabled) 
                    ?? keyCredentials.FirstOrDefault(k => k.IsEnabled);

                if (primaryKey == null)
                {
                    _logger.LogWarning("No enabled API key found for provider {ProviderId}", provider.Id);
                    return new List<DiscoveredModel>();
                }

                // Get the client for this provider
                var client = _clientFactory.GetClientByProviderId(provider.Id);

                // Get the models from the client
                var modelList = await client.ListModelsAsync(
                    apiKey ?? primaryKey.ApiKey,
                    cancellationToken);

                // Convert to DiscoveredModel format
                var discoveredModels = new List<DiscoveredModel>();
                
                foreach (var modelId in modelList)
                {
                    var discoveredModel = new DiscoveredModel
                    {
                        ModelId = modelId,
                        Provider = provider.ProviderName ?? provider.ProviderType.ToString(),
                        DisplayName = modelId,
                        Capabilities = GetCapabilitiesForModel(provider.ProviderType, modelId),
                        LastVerified = DateTime.UtcNow
                    };
                    
                    discoveredModels.Add(discoveredModel);
                }

                _logger.LogInformation("Discovered {Count} models for provider {ProviderId}", 
                    discoveredModels.Count(), provider.Id);

                return discoveredModels;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error discovering models for provider {ProviderId}", provider.Id);
                return new List<DiscoveredModel>();
            }
        }

        /// <inheritdoc/>
        public bool SupportsDiscovery(ProviderType providerType)
        {
            // We support discovery for all provider types
            return true;
        }

        private CoreModelCapabilities GetCapabilitiesForModel(ProviderType providerType, string modelId)
        {
            var capabilities = new CoreModelCapabilities();
            var modelIdLower = modelId.ToLowerInvariant();

            // Set basic chat capabilities for most models
            capabilities.Chat = true;
            capabilities.ChatStream = true;

            // Provider-specific capabilities
            switch (providerType)
            {
                case ProviderType.SambaNova:
                    capabilities.FunctionCalling = true;
                    capabilities.JsonMode = true;
                    capabilities.Vision = modelIdLower.Contains("maverick"); // Llama-4-Maverick is multimodal
                    
                    // Set context lengths based on model
                    if (modelIdLower.Contains("deepseek-r1-distill") || modelIdLower.Contains("meta-llama-3.3-70b") || modelIdLower.Contains("maverick"))
                    {
                        capabilities.MaxTokens = 131072; // 128k context
                        capabilities.MaxOutputTokens = 16384;
                    }
                    else if (modelIdLower.Contains("deepseek"))
                    {
                        capabilities.MaxTokens = 32768; // 32k context
                        capabilities.MaxOutputTokens = 8192;
                    }
                    else if (modelIdLower.Contains("meta-llama-3.1-8b") || modelIdLower.Contains("swallow"))
                    {
                        capabilities.MaxTokens = 16384; // 16k context
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("qwen"))
                    {
                        capabilities.MaxTokens = 8192; // 8k context
                        capabilities.MaxOutputTokens = 4096;
                    }
                    else if (modelIdLower.Contains("e5-mistral"))
                    {
                        capabilities.MaxTokens = 4096; // 4k context
                        capabilities.MaxOutputTokens = 2048;
                    }
                    break;

                case ProviderType.MiniMax:
                    capabilities.Vision = modelIdLower.Contains("abab");
                    if (modelIdLower.Contains("video"))
                    {
                        capabilities.VideoGeneration = true;
                        capabilities.Chat = false;
                        capabilities.ChatStream = false;
                    }
                    else if (modelIdLower.Contains("image"))
                    {
                        capabilities.ImageGeneration = true;
                        capabilities.Chat = false;
                        capabilities.ChatStream = false;
                    }
                    break;

                case ProviderType.OpenAI:
                    capabilities.FunctionCalling = true;
                    capabilities.JsonMode = true;
                    capabilities.Vision = modelIdLower.Contains("vision") || modelIdLower.Contains("gpt-4o");
                    capabilities.ImageGeneration = modelIdLower.Contains("dall-e");
                    capabilities.Embeddings = modelIdLower.Contains("embedding");
                    break;

                case ProviderType.Cerebras:
                    capabilities.FunctionCalling = false;
                    capabilities.JsonMode = false;
                    capabilities.MaxTokens = 128000;
                    capabilities.MaxOutputTokens = 8192;
                    break;

                default:
                    // Basic defaults for other providers
                    capabilities.MaxTokens = 4096;
                    capabilities.MaxOutputTokens = 2048;
                    break;
            }

            return capabilities;
        }

    }
}
