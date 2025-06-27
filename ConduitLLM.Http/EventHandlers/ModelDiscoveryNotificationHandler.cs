using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelCapabilitiesDiscovered events and sends real-time notifications via SignalR
    /// </summary>
    public class ModelDiscoveryNotificationHandler : IConsumer<ModelCapabilitiesDiscovered>
    {
        private readonly IHubContext<ModelDiscoveryHub> _hubContext;
        private readonly ILogger<ModelDiscoveryNotificationHandler> _logger;
        private readonly IMemoryCache _cache;
        private readonly IModelCostService _modelCostService;
        private const string CacheKeyPrefix = "previous_model_capabilities_";
        private const string PricingCacheKeyPrefix = "previous_model_pricing_";

        public ModelDiscoveryNotificationHandler(
            IHubContext<ModelDiscoveryHub> hubContext,
            ILogger<ModelDiscoveryNotificationHandler> logger,
            IMemoryCache cache,
            IModelCostService modelCostService)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
        }

        public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
        {
            var message = context.Message;
            _logger.LogInformation(
                "Processing model discovery notification for provider {Provider} with {ModelCount} models",
                message.ProviderName, message.ModelCapabilities.Count);

            try
            {
                // Check for new models
                var newModels = await CheckForNewModelsAsync(message);
                if (newModels.Any())
                {
                    await NotifyNewModelsDiscoveredAsync(message.ProviderName, newModels, message);
                }

                // Check for capability changes
                var capabilityChanges = await CheckForCapabilityChangesAsync(message);
                foreach (var change in capabilityChanges)
                {
                    await NotifyCapabilityChangedAsync(message.ProviderName, change);
                }

                // Check for pricing updates
                var pricingUpdates = await CheckForPricingUpdatesAsync(message);
                foreach (var update in pricingUpdates)
                {
                    await NotifyPricingUpdatedAsync(message.ProviderName, update);
                }

                // Update cache with current state for next comparison
                await UpdateCacheAsync(message);

                _logger.LogInformation(
                    "Successfully processed model discovery notification for provider {Provider}",
                    message.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing model discovery notification for provider {Provider}",
                    message.ProviderName);
                throw;
            }
        }

        private Task<List<DiscoveredModelInfo>> CheckForNewModelsAsync(ModelCapabilitiesDiscovered message)
        {
            var cacheKey = $"{CacheKeyPrefix}{message.ProviderName}";
            var previousModels = _cache.Get<Dictionary<string, Core.Events.ModelCapabilities>>(cacheKey);
            
            if (previousModels == null)
            {
                // First time discovery - all models are new
                return Task.FromResult(message.ModelCapabilities.Select(kvp => ConvertToDiscoveredModelInfo(kvp.Key, kvp.Value)).ToList());
            }

            var newModels = new List<DiscoveredModelInfo>();
            foreach (var kvp in message.ModelCapabilities)
            {
                if (!previousModels.ContainsKey(kvp.Key))
                {
                    newModels.Add(ConvertToDiscoveredModelInfo(kvp.Key, kvp.Value));
                }
            }

            return Task.FromResult(newModels);
        }

        private Task<List<ModelCapabilityChange>> CheckForCapabilityChangesAsync(ModelCapabilitiesDiscovered message)
        {
            var cacheKey = $"{CacheKeyPrefix}{message.ProviderName}";
            var previousModels = _cache.Get<Dictionary<string, Core.Events.ModelCapabilities>>(cacheKey);
            
            if (previousModels == null)
            {
                return Task.FromResult(new List<ModelCapabilityChange>());
            }

            var changes = new List<ModelCapabilityChange>();
            foreach (var kvp in message.ModelCapabilities)
            {
                if (previousModels.TryGetValue(kvp.Key, out var previousCapabilities))
                {
                    var changeList = CompareCapabilities(previousCapabilities, kvp.Value);
                    if (changeList.Any())
                    {
                        changes.Add(new ModelCapabilityChange
                        {
                            ModelId = kvp.Key,
                            PreviousCapabilities = previousCapabilities,
                            NewCapabilities = kvp.Value,
                            Changes = changeList
                        });
                    }
                }
            }

            return Task.FromResult(changes);
        }

        private async Task<List<ModelPricingUpdate>> CheckForPricingUpdatesAsync(ModelCapabilitiesDiscovered message)
        {
            var updates = new List<ModelPricingUpdate>();
            
            // Get current pricing for all models
            foreach (var modelId in message.ModelCapabilities.Keys)
            {
                try
                {
                    var currentCost = await _modelCostService.GetCostForModelAsync(modelId);
                    if (currentCost != null)
                    {
                        var pricingCacheKey = $"{PricingCacheKeyPrefix}{message.ProviderName}_{modelId}";
                        var previousCost = _cache.Get<decimal?>(pricingCacheKey);
                        
                        if (previousCost.HasValue && previousCost.Value != currentCost.InputTokenCost)
                        {
                            updates.Add(new ModelPricingUpdate
                            {
                                ModelId = modelId,
                                PreviousCost = previousCost.Value,
                                NewCost = currentCost.InputTokenCost,
                                CostDetails = currentCost
                            });
                        }
                        
                        // Update cache
                        _cache.Set(pricingCacheKey, currentCost.InputTokenCost, TimeSpan.FromDays(7));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to check pricing for model {Model} from provider {Provider}",
                        modelId, message.ProviderName);
                }
            }

            return updates;
        }

        private async Task NotifyNewModelsDiscoveredAsync(string provider, List<DiscoveredModelInfo> newModels, ModelCapabilitiesDiscovered message)
        {
            var notification = new NewModelsDiscoveredNotification
            {
                Provider = provider,
                NewModels = newModels,
                TotalModelCount = message.ModelCapabilities.Count,
                DiscoveredAt = message.DiscoveredAt
            };

            // Notify provider-specific subscribers
            await _hubContext.Clients.Group($"provider-{provider.ToLowerInvariant()}")
                .SendAsync("NewModelsDiscovered", notification);

            // Notify global subscribers
            await _hubContext.Clients.Group("model-discovery-all")
                .SendAsync("NewModelsDiscovered", notification);

            _logger.LogInformation(
                "Sent new models notification for provider {Provider}: {Count} new models",
                provider, newModels.Count);
        }

        private async Task NotifyCapabilityChangedAsync(string provider, ModelCapabilityChange change)
        {
            var notification = new ModelCapabilitiesChangedNotification
            {
                Provider = provider,
                ModelId = change.ModelId,
                PreviousCapabilities = ConvertToCapabilityInfo(change.PreviousCapabilities),
                NewCapabilities = ConvertToCapabilityInfo(change.NewCapabilities),
                Changes = change.Changes,
                ChangedAt = DateTime.UtcNow
            };

            // Notify provider-specific subscribers
            await _hubContext.Clients.Group($"provider-{provider.ToLowerInvariant()}")
                .SendAsync("ModelCapabilitiesChanged", notification);

            // Notify global subscribers
            await _hubContext.Clients.Group("model-discovery-all")
                .SendAsync("ModelCapabilitiesChanged", notification);

            _logger.LogInformation(
                "Sent capability change notification for model {Model} from provider {Provider}",
                change.ModelId, provider);
        }

        private async Task NotifyPricingUpdatedAsync(string provider, ModelPricingUpdate update)
        {
            var percentageChange = ((update.NewCost - update.PreviousCost) / update.PreviousCost) * 100;
            
            var notification = new ModelPricingUpdatedNotification
            {
                Provider = provider,
                ModelId = update.ModelId,
                PreviousPricing = new ModelPricingInfo
                {
                    InputTokenCost = update.PreviousCost,
                    Currency = "USD"
                },
                NewPricing = new ModelPricingInfo
                {
                    InputTokenCost = update.NewCost,
                    OutputTokenCost = update.CostDetails?.OutputTokenCost,
                    Currency = "USD",
                    EffectiveDate = DateTime.UtcNow
                },
                PercentageChange = percentageChange,
                UpdatedAt = DateTime.UtcNow
            };

            // Notify provider-specific subscribers
            await _hubContext.Clients.Group($"provider-{provider.ToLowerInvariant()}")
                .SendAsync("ModelPricingUpdated", notification);

            // Notify global subscribers
            await _hubContext.Clients.Group("model-discovery-all")
                .SendAsync("ModelPricingUpdated", notification);

            _logger.LogInformation(
                "Sent pricing update notification for model {Model} from provider {Provider}: {Change:F2}% change",
                update.ModelId, provider, percentageChange);
        }

        private Task UpdateCacheAsync(ModelCapabilitiesDiscovered message)
        {
            var cacheKey = $"{CacheKeyPrefix}{message.ProviderName}";
            _cache.Set(cacheKey, message.ModelCapabilities, TimeSpan.FromDays(7));
            return Task.CompletedTask;
        }

        private DiscoveredModelInfo ConvertToDiscoveredModelInfo(string modelId, Core.Events.ModelCapabilities capabilities)
        {
            return new DiscoveredModelInfo
            {
                ModelId = modelId,
                DisplayName = modelId,
                Capabilities = ConvertToCapabilityInfo(capabilities),
                ReleaseDate = DateTime.UtcNow
            };
        }

        private ModelCapabilityInfo ConvertToCapabilityInfo(Core.Events.ModelCapabilities capabilities)
        {
            // Map from the event's ModelCapabilities to the DTO's ModelCapabilityInfo
            return new ModelCapabilityInfo
            {
                Chat = true, // Default, as the event model doesn't have all fields
                ImageGeneration = capabilities.SupportsImageGeneration,
                Vision = capabilities.SupportsVision,
                Embeddings = capabilities.SupportsEmbeddings,
                VideoGeneration = capabilities.SupportsVideoGeneration,
                AudioTranscription = capabilities.SupportsAudioTranscription,
                TextToSpeech = capabilities.SupportsTextToSpeech,
                FunctionCalling = capabilities.SupportsFunctionCalling,
                AdditionalCapabilities = capabilities.AdditionalCapabilities
            };
        }

        private List<string> CompareCapabilities(Core.Events.ModelCapabilities previous, Core.Events.ModelCapabilities current)
        {
            var changes = new List<string>();

            if (previous.SupportsImageGeneration != current.SupportsImageGeneration)
                changes.Add($"Image generation: {previous.SupportsImageGeneration} → {current.SupportsImageGeneration}");
            
            if (previous.SupportsVision != current.SupportsVision)
                changes.Add($"Vision: {previous.SupportsVision} → {current.SupportsVision}");
            
            if (previous.SupportsEmbeddings != current.SupportsEmbeddings)
                changes.Add($"Embeddings: {previous.SupportsEmbeddings} → {current.SupportsEmbeddings}");
            
            if (previous.SupportsVideoGeneration != current.SupportsVideoGeneration)
                changes.Add($"Video generation: {previous.SupportsVideoGeneration} → {current.SupportsVideoGeneration}");
            
            if (previous.SupportsAudioTranscription != current.SupportsAudioTranscription)
                changes.Add($"Audio transcription: {previous.SupportsAudioTranscription} → {current.SupportsAudioTranscription}");
            
            if (previous.SupportsTextToSpeech != current.SupportsTextToSpeech)
                changes.Add($"Text to speech: {previous.SupportsTextToSpeech} → {current.SupportsTextToSpeech}");
            
            if (previous.SupportsFunctionCalling != current.SupportsFunctionCalling)
                changes.Add($"Function calling: {previous.SupportsFunctionCalling} → {current.SupportsFunctionCalling}");

            return changes;
        }

        private class ModelCapabilityChange
        {
            public string ModelId { get; set; } = string.Empty;
            public Core.Events.ModelCapabilities PreviousCapabilities { get; set; } = new();
            public Core.Events.ModelCapabilities NewCapabilities { get; set; } = new();
            public List<string> Changes { get; set; } = new();
        }

        private class ModelPricingUpdate
        {
            public string ModelId { get; set; } = string.Empty;
            public decimal PreviousCost { get; set; }
            public decimal NewCost { get; set; }
            public dynamic? CostDetails { get; set; }
        }
    }
}