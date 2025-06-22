using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Events;
using Microsoft.Extensions.Caching.Memory;
using MassTransit;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for discovering provider capabilities and available models.
    /// Enhanced with dynamic discovery providers for comprehensive metadata extraction.
    /// </summary>
    public class ProviderDiscoveryService : IProviderDiscoveryService
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ConduitLLM.Configuration.IProviderCredentialService _credentialService;
        private readonly ConduitLLM.Configuration.IModelProviderMappingService _mappingService;
        private readonly ILogger<ProviderDiscoveryService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IPublishEndpoint? _publishEndpoint;
        private readonly IEnumerable<IModelDiscoveryProvider> _discoveryProviders;
        private const string CacheKeyPrefix = "provider_capabilities_";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

        // Known model patterns and their capabilities
        private static readonly Dictionary<string, Func<string, ConduitLLM.Core.Interfaces.ModelCapabilities>> KnownModelPatterns = new()
        {
            // OpenAI - Order matters, more specific patterns first
            ["gpt-4-vision"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                FunctionCalling = true,
                ToolUse = true,
                JsonMode = true,
                MaxTokens = 128000,
                MaxOutputTokens = 4096
            },
            ["gpt-4o"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                FunctionCalling = true,
                ToolUse = true,
                JsonMode = true,
                MaxTokens = 128000,
                MaxOutputTokens = 4096
            },
            ["gpt-4-turbo"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                FunctionCalling = true,
                ToolUse = true,
                JsonMode = true,
                MaxTokens = 128000,
                MaxOutputTokens = 4096
            },
            ["gpt-4"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = modelId.Contains("vision") || modelId.Contains("gpt-4-turbo") || modelId.Contains("gpt-4o"),
                FunctionCalling = true,
                ToolUse = true,
                JsonMode = true,
                MaxTokens = modelId.Contains("32k") ? 32768 : (modelId.Contains("turbo") ? 128000 : 8192),
                MaxOutputTokens = 4096
            },
            ["gpt-3.5"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                FunctionCalling = true,
                ToolUse = true,
                JsonMode = true,
                MaxTokens = 16385,
                MaxOutputTokens = 4096
            },
            ["dall-e"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                ImageGeneration = true,
                SupportedImageSizes = modelId.Contains("3") 
                    ? new List<string> { "1024x1024", "1792x1024", "1024x1792" }
                    : new List<string> { "256x256", "512x512", "1024x1024" }
            },
            ["text-embedding"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Embeddings = true
            },

            // Anthropic Claude
            ["claude"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = modelId.Contains("vision") || modelId.Contains("claude-3"),
                ToolUse = true,
                JsonMode = false,
                MaxTokens = modelId.Contains("200k") ? 200000 : 100000,
                MaxOutputTokens = 4096
            },

            // MiniMax
            ["abab"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                MaxTokens = 245760,
                MaxOutputTokens = 8192
            },
            ["image-01"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                ImageGeneration = true,
                SupportedImageSizes = new List<string> 
                { 
                    "1:1", "16:9", "9:16", "4:3", "3:4", 
                    "2.35:1", "1:2.35", "21:9", "9:21" 
                }
            },
            ["video-01"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                VideoGeneration = true,
                SupportedVideoResolutions = new List<string> 
                { 
                    "720x480", "1280x720", "1920x1080", "720x1280", "1080x1920" 
                },
                MaxVideoDurationSeconds = 6
            },

            // Google
            ["gemini"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                Vision = true,
                VideoUnderstanding = modelId.Contains("pro"),
                FunctionCalling = false,  // Gemini models use a different function calling approach
                ToolUse = false,
                MaxTokens = modelId.Contains("1.5") ? 1048576 : 32768,
                MaxOutputTokens = 8192
            },

            // Replicate
            ["flux"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                ImageGeneration = true,
                SupportedImageSizes = new List<string> { "Any custom size" }
            },
            ["stable-diffusion"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                ImageGeneration = true,
                SupportedImageSizes = new List<string> { "512x512", "768x768", "1024x1024" }
            },
            
            // Mistral
            ["mistral"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                FunctionCalling = false,
                ToolUse = false,
                JsonMode = false
            },
            
            // Meta Llama
            ["llama"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                Chat = true,
                ChatStream = true,
                FunctionCalling = false,
                ToolUse = false,
                JsonMode = false
            }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderDiscoveryService"/> class.
        /// </summary>
        /// <param name="clientFactory">Factory for creating LLM clients.</param>
        /// <param name="credentialService">Service for managing provider credentials.</param>
        /// <param name="mappingService">Service for managing model provider mappings.</param>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="cache">Memory cache for caching discovery results.</param>
        /// <param name="discoveryProviders">Collection of provider-specific discovery implementations.</param>
        /// <param name="publishEndpoint">Optional endpoint for publishing events.</param>
        public ProviderDiscoveryService(
            ILLMClientFactory clientFactory,
            ConduitLLM.Configuration.IProviderCredentialService credentialService,
            ConduitLLM.Configuration.IModelProviderMappingService mappingService,
            ILogger<ProviderDiscoveryService> logger,
            IMemoryCache cache,
            IEnumerable<IModelDiscoveryProvider> discoveryProviders,
            IPublishEndpoint? publishEndpoint = null)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _discoveryProviders = discoveryProviders ?? throw new ArgumentNullException(nameof(discoveryProviders));
            _publishEndpoint = publishEndpoint; // Optional - can be null if MassTransit not configured
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, DiscoveredModel>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            var allModels = new Dictionary<string, DiscoveredModel>();

            // Get all known providers from the system
            var knownProviders = new[] { "openai", "anthropic", "google", "minimax", "replicate", "mistral", "cohere", "openrouter" };

            // BULK OPTIMIZATION: Load all credentials and mappings in parallel to avoid N+1 queries
            var credentialsTask = _credentialService.GetAllCredentialsAsync();
            var mappingsTask = _mappingService.GetAllMappingsAsync();
            
            await Task.WhenAll(credentialsTask, mappingsTask);
            
            var allCredentials = await credentialsTask;
            var modelMappings = await mappingsTask;
            
            var credentialLookup = allCredentials.ToDictionary(c => c.ProviderName.ToLowerInvariant(), c => c);

            foreach (var providerName in knownProviders)
            {
                try
                {
                    if (credentialLookup.TryGetValue(providerName.ToLowerInvariant(), out var credentials) 
                        && credentials.IsEnabled)
                    {
                        var providerModels = await DiscoverProviderModelsAsync(
                            providerName, 
                            credentials.ApiKey, 
                            cancellationToken);

                        foreach (var model in providerModels)
                        {
                            allModels[model.Key] = model.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to discover models for provider {Provider}", providerName);
                }
            }

            // Add any configured model mappings that weren't discovered (using pre-loaded data)
            foreach (var mapping in modelMappings)
            {
                if (!allModels.ContainsKey(mapping.ModelAlias))
                {
                    allModels[mapping.ModelAlias] = InferModelCapabilities(
                        mapping.ModelAlias, 
                        mapping.ProviderName, 
                        mapping.ProviderModelId);
                }
            }

            return allModels;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, DiscoveredModel>> DiscoverProviderModelsAsync(
            string providerName, 
            string? apiKey = null, 
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{providerName}";
            
            // Check cache first
            if (_cache.TryGetValue<Dictionary<string, DiscoveredModel>>(cacheKey, out var cachedModels))
            {
                _logger.LogDebug("Using cached capabilities for provider {Provider}", providerName);
                return cachedModels!;
            }

            var models = new Dictionary<string, DiscoveredModel>();

            // First try enhanced discovery providers
            var discoveryProvider = _discoveryProviders.FirstOrDefault(p => 
                string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));
            
            if (discoveryProvider != null && discoveryProvider.SupportsDiscovery)
            {
                try
                {
                    _logger.LogDebug("Using enhanced discovery provider for {Provider}", providerName);
                    var modelMetadataList = await discoveryProvider.DiscoverModelsAsync(cancellationToken);
                    
                    foreach (var metadata in modelMetadataList)
                    {
                        var discoveredModel = ConvertMetadataToDiscoveredModel(metadata);
                        models[metadata.ModelId] = discoveredModel;
                    }

                    _logger.LogInformation("Enhanced discovery found {Count} models from provider {Provider}", 
                        models.Count, providerName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Enhanced discovery failed for provider {Provider}, falling back to legacy methods", providerName);
                    // Continue to fallback methods below
                }
            }
            
            // If enhanced discovery didn't work or found no models, try legacy client discovery
            if (models.Count == 0)
            {
                try
                {
                    // Get client for provider
                    var client = _clientFactory.GetClientByProvider(providerName);

                    // Try to list models from the provider
                    try
                    {
                        var modelList = await client.ListModelsAsync(apiKey, cancellationToken);
                        
                        foreach (var modelId in modelList)
                        {
                            var discoveredModel = InferModelCapabilities(modelId, providerName, modelId);
                            models[modelId] = discoveredModel;
                        }

                        _logger.LogInformation("Legacy discovery found {Count} models from provider {Provider}", 
                            models.Count, providerName);
                    }
                    catch (NotSupportedException)
                    {
                        _logger.LogDebug("Provider {Provider} does not support listing models", providerName);
                        // Fall back to known models for this provider
                        AddKnownModelsForProvider(providerName, models);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create client for provider {Provider}", providerName);
                    // Fall back to known models for this provider
                    AddKnownModelsForProvider(providerName, models);
                }
            }

            // Cache the results
            _cache.Set(cacheKey, models, _cacheExpiration);

            // Publish ModelCapabilitiesDiscovered event to eliminate redundant discovery calls
            if (_publishEndpoint != null && models.Count > 0)
            {
                try
                {
                    // Convert DiscoveredModel to ModelCapabilities for the event
                    var modelCapabilities = models.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new ConduitLLM.Core.Events.ModelCapabilities
                        {
                            SupportsImageGeneration = kvp.Value.Capabilities.ImageGeneration,
                            SupportsVision = kvp.Value.Capabilities.Vision,
                            SupportsEmbeddings = kvp.Value.Capabilities.Embeddings,
                            SupportsAudioTranscription = false, // Not supported in current interface
                            SupportsTextToSpeech = false, // Not supported in current interface
                            SupportsRealtimeAudio = false, // Not supported in current interface
                            SupportsFunctionCalling = kvp.Value.Capabilities.FunctionCalling,
                            AdditionalCapabilities = new Dictionary<string, object>
                            {
                                ["chat"] = kvp.Value.Capabilities.Chat,
                                ["chatStream"] = kvp.Value.Capabilities.ChatStream,
                                ["toolUse"] = kvp.Value.Capabilities.ToolUse,
                                ["jsonMode"] = kvp.Value.Capabilities.JsonMode,
                                ["maxTokens"] = kvp.Value.Capabilities.MaxTokens ?? 0,
                                ["maxOutputTokens"] = kvp.Value.Capabilities.MaxOutputTokens ?? 0
                            }
                        });

                    // Get provider ID from credentials
                    var providerId = await GetProviderIdAsync(providerName);

                    await _publishEndpoint.Publish(new ModelCapabilitiesDiscovered
                    {
                        ProviderId = providerId,
                        ProviderName = providerName,
                        ModelCapabilities = modelCapabilities,
                        DiscoveredAt = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid().ToString()
                    });

                    _logger.LogDebug("Published ModelCapabilitiesDiscovered event for provider {ProviderName} with {ModelCount} models",
                        providerName, models.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish ModelCapabilitiesDiscovered event for provider {ProviderName} - discovery succeeded but event not sent",
                        providerName);
                    // Don't fail the operation if event publishing fails
                }
            }
            else if (models.Count == 0)
            {
                _logger.LogDebug("No models discovered for provider {ProviderName} - skipping ModelCapabilitiesDiscovered event", providerName);
            }
            else
            {
                _logger.LogDebug("Event publishing not configured - skipping ModelCapabilitiesDiscovered event for provider {ProviderName}", providerName);
            }

            return models;
        }

        /// <inheritdoc/>
        public async Task<bool> TestModelCapabilityAsync(
            string modelName, 
            ModelCapability capability, 
            CancellationToken cancellationToken = default)
        {
            // First check if we have cached information
            var allModels = await DiscoverModelsAsync(cancellationToken);
            
            if (allModels.TryGetValue(modelName, out var model))
            {
                return capability switch
                {
                    ModelCapability.Chat => model.Capabilities.Chat,
                    ModelCapability.ChatStream => model.Capabilities.ChatStream,
                    ModelCapability.Embeddings => model.Capabilities.Embeddings,
                    ModelCapability.ImageGeneration => model.Capabilities.ImageGeneration,
                    ModelCapability.Vision => model.Capabilities.Vision,
                    ModelCapability.VideoGeneration => model.Capabilities.VideoGeneration,
                    ModelCapability.VideoUnderstanding => model.Capabilities.VideoUnderstanding,
                    ModelCapability.FunctionCalling => model.Capabilities.FunctionCalling,
                    ModelCapability.ToolUse => model.Capabilities.ToolUse,
                    ModelCapability.JsonMode => model.Capabilities.JsonMode,
                    _ => false
                };
            }

            // If not found, try to infer from model name
            var inferredModel = InferModelCapabilities(modelName, "unknown", modelName);
            return TestCapability(inferredModel.Capabilities, capability);
        }

        /// <inheritdoc/>
        public async Task RefreshCapabilitiesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Refreshing provider capabilities cache");
            
            // Clear all cached entries for known providers
            var knownProviders = new[] { "openai", "anthropic", "google", "minimax", "replicate", "mistral", "cohere" };
            foreach (var providerName in knownProviders)
            {
                var cacheKey = $"{CacheKeyPrefix}{providerName}";
                _cache.Remove(cacheKey);
            }

            // Re-discover all models
            await DiscoverModelsAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task RefreshProviderCapabilitiesAsync(string providerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty", nameof(providerName));
            }

            _logger.LogInformation("Refreshing capabilities cache for provider: {ProviderName}", providerName);
            
            // Clear cached entries for the specific provider
            var cacheKey = $"{CacheKeyPrefix}{providerName.ToLowerInvariant()}";
            _cache.Remove(cacheKey);
            
            try
            {
                // Re-discover models for this specific provider
                var discoveredModels = await DiscoverProviderModelsAsync(providerName, null, cancellationToken);
                
                _logger.LogInformation("Successfully refreshed capabilities for provider {ProviderName}: {ModelCount} models discovered", 
                    providerName, discoveredModels.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh capabilities for provider {ProviderName}", providerName);
                // Don't throw - this is called from event handlers and shouldn't fail the entire event processing
            }
        }

        private DiscoveredModel InferModelCapabilities(string modelAlias, string provider, string modelId)
        {
            var capabilities = new ConduitLLM.Core.Interfaces.ModelCapabilities();
            var lowerModelId = modelId.ToLowerInvariant();

            // Special handling for OpenRouter models (format: provider/model-name)
            if (provider.ToLowerInvariant() == "openrouter" && modelId.Contains("/"))
            {
                var parts = modelId.Split('/', 2);
                if (parts.Length == 2)
                {
                    var underlyingProvider = parts[0].ToLowerInvariant();
                    var underlyingModel = parts[1].ToLowerInvariant();
                    
                    // Use the underlying model name for pattern matching
                    lowerModelId = underlyingModel;
                    
                    // Also check if the full model ID matches any patterns
                    var fullModelId = modelId.ToLowerInvariant();
                    foreach (var pattern in KnownModelPatterns)
                    {
                        if (fullModelId.Contains(pattern.Key) || lowerModelId.Contains(pattern.Key))
                        {
                            capabilities = pattern.Value(lowerModelId);
                            break;
                        }
                    }
                    
                    // If no pattern matched, use defaults based on the underlying provider
                    if (!HasAnyCapability(capabilities))
                    {
                        capabilities = GetProviderDefaults(underlyingProvider);
                    }
                }
            }
            else
            {
                // Standard pattern matching for non-OpenRouter providers
                foreach (var pattern in KnownModelPatterns)
                {
                    if (lowerModelId.Contains(pattern.Key))
                    {
                        capabilities = pattern.Value(lowerModelId);
                        break;
                    }
                }
            }

            // If no pattern matched, use provider-specific defaults
            if (!HasAnyCapability(capabilities))
            {
                capabilities = GetProviderDefaults(provider);
            }

            return new DiscoveredModel
            {
                ModelId = modelAlias,
                Provider = provider,
                DisplayName = modelAlias,
                Capabilities = capabilities,
                LastVerified = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["original_model_id"] = modelId,
                    ["inferred"] = true
                }
            };
        }

        private bool HasAnyCapability(ConduitLLM.Core.Interfaces.ModelCapabilities capabilities)
        {
            return capabilities.Chat || capabilities.Embeddings || 
                   capabilities.ImageGeneration || capabilities.VideoGeneration;
        }

        private ConduitLLM.Core.Interfaces.ModelCapabilities GetProviderDefaults(string provider)
        {
            return provider.ToLowerInvariant() switch
            {
                "openai" => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true },
                "anthropic" => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true, ToolUse = true },
                "google" => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true, Vision = true },
                "minimax" => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true, Vision = true },
                "replicate" => new ConduitLLM.Core.Interfaces.ModelCapabilities { ImageGeneration = true },
                "openrouter" => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true },
                _ => new ConduitLLM.Core.Interfaces.ModelCapabilities { Chat = true, ChatStream = true }
            };
        }

        private void AddKnownModelsForProvider(string provider, Dictionary<string, DiscoveredModel> models)
        {
            var knownModels = provider.ToLowerInvariant() switch
            {
                "openai" => new[] { "gpt-4-turbo-preview", "gpt-4", "gpt-3.5-turbo", "dall-e-3", "dall-e-2" },
                "anthropic" => new[] { "claude-3-opus-20240229", "claude-3-sonnet-20240229", "claude-3-haiku-20240307" },
                "google" => new[] { "gemini-1.5-pro", "gemini-1.5-flash", "gemini-pro" },
                "minimax" => new[] { "abab6.5-chat", "abab6.5s-chat", "abab5.5-chat", "image-01", "video-01" },
                "openrouter" => new[] { 
                    "anthropic/claude-3-opus", "anthropic/claude-3-sonnet", "anthropic/claude-3-haiku",
                    "openai/gpt-4-turbo", "openai/gpt-4", "openai/gpt-3.5-turbo",
                    "google/gemini-pro", "google/gemini-pro-vision",
                    "meta-llama/llama-3-70b-instruct", "mistralai/mistral-large"
                },
                _ => Array.Empty<string>()
            };

            foreach (var modelId in knownModels)
            {
                models[modelId] = InferModelCapabilities(modelId, provider, modelId);
            }
        }

        /// <summary>
        /// Converts enhanced ModelMetadata from discovery providers to DiscoveredModel format.
        /// This preserves all the rich metadata while maintaining compatibility with existing interfaces.
        /// </summary>
        /// <param name="metadata">Enhanced model metadata from discovery provider.</param>
        /// <returns>DiscoveredModel compatible with existing code.</returns>
        private DiscoveredModel ConvertMetadataToDiscoveredModel(ModelMetadata metadata)
        {
            var discoveredModel = new DiscoveredModel
            {
                ModelId = metadata.ModelId,
                Provider = metadata.Provider,
                DisplayName = metadata.DisplayName,
                Capabilities = metadata.Capabilities,
                LastVerified = metadata.LastUpdated,
                Metadata = new Dictionary<string, object>(metadata.AdditionalMetadata)
            };

            // Add enhanced metadata to the metadata dictionary
            if (metadata.MaxContextTokens.HasValue)
            {
                discoveredModel.Metadata["max_context_tokens"] = metadata.MaxContextTokens.Value;
            }
            
            if (metadata.MaxOutputTokens.HasValue)
            {
                discoveredModel.Metadata["max_output_tokens"] = metadata.MaxOutputTokens.Value;
            }
            
            if (metadata.InputTokenCost.HasValue)
            {
                discoveredModel.Metadata["input_token_cost"] = metadata.InputTokenCost.Value;
            }
            
            if (metadata.OutputTokenCost.HasValue)
            {
                discoveredModel.Metadata["output_token_cost"] = metadata.OutputTokenCost.Value;
            }
            
            if (metadata.ImageCostPerImage.HasValue)
            {
                discoveredModel.Metadata["image_cost_per_image"] = metadata.ImageCostPerImage.Value;
            }
            
            if (metadata.SupportedImageSizes.Count > 0)
            {
                discoveredModel.Metadata["supported_image_sizes"] = metadata.SupportedImageSizes;
            }
            
            if (metadata.SupportedVideoResolutions.Count > 0)
            {
                discoveredModel.Metadata["supported_video_resolutions"] = metadata.SupportedVideoResolutions;
            }
            
            if (metadata.MaxVideoDurationSeconds.HasValue)
            {
                discoveredModel.Metadata["max_video_duration_seconds"] = metadata.MaxVideoDurationSeconds.Value;
            }
            
            // Add discovery source information
            discoveredModel.Metadata["discovery_source"] = metadata.Source.ToString();
            
            if (metadata.Warnings.Count > 0)
            {
                discoveredModel.Metadata["warnings"] = metadata.Warnings;
            }
            
            // Mark as enhanced discovery
            discoveredModel.Metadata["enhanced_discovery"] = true;
            discoveredModel.Metadata["last_updated"] = metadata.LastUpdated;

            return discoveredModel;
        }

        private bool TestCapability(ConduitLLM.Core.Interfaces.ModelCapabilities capabilities, ModelCapability capability)
        {
            return capability switch
            {
                ModelCapability.Chat => capabilities.Chat,
                ModelCapability.ChatStream => capabilities.ChatStream,
                ModelCapability.Embeddings => capabilities.Embeddings,
                ModelCapability.ImageGeneration => capabilities.ImageGeneration,
                ModelCapability.Vision => capabilities.Vision,
                ModelCapability.VideoGeneration => capabilities.VideoGeneration,
                ModelCapability.VideoUnderstanding => capabilities.VideoUnderstanding,
                ModelCapability.FunctionCalling => capabilities.FunctionCalling,
                ModelCapability.ToolUse => capabilities.ToolUse,
                ModelCapability.JsonMode => capabilities.JsonMode,
                _ => false
            };
        }

        /// <summary>
        /// Gets the provider ID from the credential service for a given provider name
        /// </summary>
        /// <param name="providerName">The provider name to look up</param>
        /// <returns>The provider ID, or 0 if not found</returns>
        private async Task<int> GetProviderIdAsync(string providerName)
        {
            try
            {
                // Get provider credential to find the ID
                var credential = await _credentialService.GetCredentialByProviderNameAsync(providerName);
                if (credential != null)
                {
                    return credential.Id;
                }
                
                _logger.LogDebug("No provider credential found for provider name: {ProviderName}", providerName);
                return 0; // Return 0 if provider not found
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting provider ID for provider name: {ProviderName}", providerName);
                return 0; // Return 0 on error to avoid failing event publishing
            }
        }
    }
}