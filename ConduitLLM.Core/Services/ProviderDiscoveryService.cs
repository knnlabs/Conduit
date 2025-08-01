using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Interfaces.Configuration;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Events;
using Microsoft.Extensions.Caching.Memory;
using MassTransit;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for discovering provider capabilities and available models.
    /// Enhanced with dynamic discovery providers for comprehensive metadata extraction.
    /// </summary>
    public class ProviderDiscoveryService : EventPublishingServiceBase, IProviderDiscoveryService
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly ConduitLLM.Configuration.IProviderService _credentialService;
        private readonly ConduitLLM.Configuration.IModelProviderMappingService _mappingService;
        private readonly ILogger<ProviderDiscoveryService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProviderModelDiscovery? _providerModelDiscovery;
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
            ["stable-video-diffusion"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                VideoGeneration = true,
                SupportedVideoResolutions = new List<string> { "576x1024", "1024x576" },
                MaxVideoDurationSeconds = 4
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
            },
            
            // Video Generation Models
            ["runway-gen"] = modelId => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                VideoGeneration = true,
                SupportedVideoResolutions = modelId.Contains("gen3") 
                    ? new List<string> { "1280x768", "768x1280", "1344x768", "768x1344" }
                    : new List<string> { "1280x720", "720x1280" },
                MaxVideoDurationSeconds = modelId.Contains("gen3") ? 10 : 4
            },
            ["pika"] = _ => new ConduitLLM.Core.Interfaces.ModelCapabilities
            {
                VideoGeneration = true,
                SupportedVideoResolutions = new List<string> { "1024x576", "576x1024", "1088x640", "640x1088" },
                MaxVideoDurationSeconds = 3
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
        /// <param name="httpClientFactory">HTTP client factory for making API calls.</param>
        /// <param name="publishEndpoint">Optional endpoint for publishing events.</param>
        /// <param name="providerModelDiscovery">Optional provider-specific model discovery service.</param>
        public ProviderDiscoveryService(
            ILLMClientFactory clientFactory,
            ConduitLLM.Configuration.IProviderService credentialService,
            ConduitLLM.Configuration.IModelProviderMappingService mappingService,
            ILogger<ProviderDiscoveryService> logger,
            IMemoryCache cache,
            IHttpClientFactory httpClientFactory,
            IPublishEndpoint? publishEndpoint = null,
            IProviderModelDiscovery? providerModelDiscovery = null)
            : base(publishEndpoint, logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _providerModelDiscovery = providerModelDiscovery;
            
            // Log event publishing configuration status
            LogEventPublishingConfiguration(nameof(ProviderDiscoveryService));
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, DiscoveredModel>> DiscoverModelsAsync(CancellationToken cancellationToken = default)
        {
            var allModels = new Dictionary<string, DiscoveredModel>();

            // Provider types that have model discovery functionality implemented
            var providerTypesThatSupportDiscovery = new HashSet<ProviderType> { 
                ProviderType.OpenAI, 
                ProviderType.Anthropic, 
                ProviderType.Gemini,
                ProviderType.MiniMax, 
                ProviderType.Replicate, 
                ProviderType.Mistral, 
                ProviderType.Cohere, 
                ProviderType.OpenRouter 
            };

            // BULK OPTIMIZATION: Load all providers and mappings in parallel to avoid N+1 queries
            var providersTask = _credentialService.GetAllProvidersAsync();
            var mappingsTask = _mappingService.GetAllMappingsAsync();
            
            await Task.WhenAll(providersTask, mappingsTask);
            
            var allProviders = await providersTask;
            var modelMappings = await mappingsTask;
            
            // Create lookup for credentials by provider ID
            var providerIdLookup = allProviders.ToDictionary(p => p.Id, p => p);

            // Iterate through each provider (not provider type)
            foreach (var provider in allProviders.Where(p => p.IsEnabled && providerTypesThatSupportDiscovery.Contains(p.ProviderType)))
            {
                try
                {
                    // Get the primary key for this provider
                    var primaryKey = await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id)
                        .ContinueWith(t => t.Result?.FirstOrDefault(k => k.IsPrimary && k.IsEnabled));

                    if (primaryKey == null)
                    {
                        // Fallback to any enabled key
                        var enabledKeys = await _credentialService.GetKeyCredentialsByProviderIdAsync(provider.Id);
                        primaryKey = enabledKeys?.FirstOrDefault(k => k.IsEnabled);
                    }
                    
                    if (primaryKey?.ApiKey == null)
                    {
                        _logger.LogWarning("No API key available for provider '{ProviderName}' (ID: {ProviderId})", 
                            provider.ProviderName, provider.Id);
                        continue;
                    }
                    
                    var providerModels = await DiscoverProviderModelsAsync(
                        provider, 
                        cancellationToken);

                    foreach (var model in providerModels)
                    {
                        allModels[model.Key] = model.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to discover models for provider '{ProviderName}' (ID: {ProviderId})", 
                        provider.ProviderName, provider.Id);
                }
            }

            // Add any configured model mappings that weren't discovered (using pre-loaded data)
            foreach (var mapping in modelMappings)
            {
                if (!allModels.ContainsKey(mapping.ModelAlias))
                {
                    // Look up the provider to get its type
                    string providerTypeName = "unknown";
                    if (mapping.ProviderId > 0 && providerIdLookup.TryGetValue(mapping.ProviderId, out var provider))
                    {
                        providerTypeName = provider.ProviderType.ToString();
                    }
                    
                    allModels[mapping.ModelAlias] = InferModelCapabilities(
                        mapping.ModelAlias, 
                        providerTypeName, 
                        mapping.ProviderModelId);
                }
            }

            return allModels;
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, DiscoveredModel>> DiscoverProviderModelsAsync(
            Provider Provider, 
            CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{Provider.Id}_{Provider.ProviderType}";
            
            // Check cache first
            if (_cache.TryGetValue<Dictionary<string, DiscoveredModel>>(cacheKey, out var cachedModels))
            {
                _logger.LogDebug("Using cached capabilities for provider '{ProviderName}' (ID: {ProviderId})", 
                    Provider.ProviderName, Provider.Id);
                return cachedModels!;
            }

            var models = new Dictionary<string, DiscoveredModel>();

            _logger.LogInformation("Starting discovery for provider '{ProviderName}' (ID: {ProviderId}, Type: {ProviderType}). Provider discovery service available: {Available}", 
                Provider.ProviderName, Provider.Id, Provider.ProviderType, _providerModelDiscovery != null);

            // Try provider-specific discovery if available
            if (_providerModelDiscovery != null && _providerModelDiscovery.SupportsDiscovery(Provider.ProviderType))
            {
                try
                {
                    _logger.LogDebug("Using provider-specific discovery for '{ProviderName}' (Type: {ProviderType})", 
                        Provider.ProviderName, Provider.ProviderType);
                    var discoveredModels = await _providerModelDiscovery.DiscoverModelsAsync(
                        Provider,
                        _httpClientFactory.CreateClient("DiscoveryProviders"),
                        null, // API key will be retrieved from credential
                        cancellationToken);
                    
                    _logger.LogDebug("Provider-specific discovery returned {Count} models", discoveredModels.Count);
                    
                    foreach (var model in discoveredModels)
                    {
                        models[model.ModelId] = model;
                    }
                    
                    if (models.Count > 0)
                    {
                        _logger.LogInformation("Provider-specific discovery found {Count} models for provider '{ProviderName}'", 
                            models.Count, Provider.ProviderName);
                        // Continue to cache and event publishing logic below
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Provider-specific discovery failed for provider '{ProviderName}' (Type: {ProviderType})", 
                        Provider.ProviderName, Provider.ProviderType);
                    // Continue to other discovery methods
                }
            }

            // Legacy discovery removed - rely only on provider-specific discovery

            // Cache the results
            _cache.Set(cacheKey, models, _cacheExpiration);

            // Publish ModelCapabilitiesDiscovered event to eliminate redundant discovery calls
            if (models.Count > 0)
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

                await PublishEventAsync(
                    new ModelCapabilitiesDiscovered
                    {
                        ProviderId = Provider.Id,
                        ModelCapabilities = modelCapabilities,
                        DiscoveredAt = DateTime.UtcNow,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    $"model discovery for provider {Provider.ProviderName}",
                    new { ProviderName = Provider.ProviderName, ProviderId = Provider.Id, ModelCount = models.Count });
            }
            else
            {
                _logger.LogDebug("No models discovered for provider '{ProviderName}' - skipping ModelCapabilitiesDiscovered event", Provider.ProviderName);
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
        public async Task RefreshProviderCapabilitiesAsync(int providerId, CancellationToken cancellationToken = default)
        {
            if (providerId <= 0)
            {
                throw new ArgumentException("Provider ID must be greater than zero", nameof(providerId));
            }

            _logger.LogInformation("Refreshing capabilities cache for provider ID: {ProviderId}", providerId);
            
            try
            {
                await RefreshProviderCapabilitiesByIdAsync(providerId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh capabilities for provider ID {ProviderId}", providerId);
                throw;
            }
        }
        
        /// <summary>
        /// Refreshes provider capabilities by provider ID
        /// </summary>
        private async Task RefreshProviderCapabilitiesByIdAsync(int providerId, CancellationToken cancellationToken)
        {
            var provider = await _credentialService.GetProviderByIdAsync(providerId);
            if (provider == null)
            {
                _logger.LogWarning("No provider found with ID {ProviderId}", providerId);
                return;
            }
            
            // Clear cached entries for this specific provider instance
            var cacheKey = $"{CacheKeyPrefix}{provider.Id}_{provider.ProviderType}";
            _cache.Remove(cacheKey);
            
            // Re-discover models for this specific provider
            var discoveredModels = await DiscoverProviderModelsAsync(provider, cancellationToken);
            
            _logger.LogInformation("Successfully refreshed capabilities for provider '{ProviderName}' (ID: {ProviderId}): {ModelCount} models discovered", 
                provider.ProviderName, provider.Id, discoveredModels.Count);
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

        // Legacy method no longer needed - all providers use provider-specific discovery


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

    }
}