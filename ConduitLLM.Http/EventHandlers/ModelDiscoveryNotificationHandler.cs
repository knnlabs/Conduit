using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Services;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelCapabilitiesDiscovered events and sends real-time notifications via SignalR
    /// </summary>
    public class ModelDiscoveryNotificationHandler : IConsumer<ModelCapabilitiesDiscovered>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<ModelDiscoveryHub> _hubContext;
        private readonly ILogger<ModelDiscoveryNotificationHandler> _logger;
        private readonly IMemoryCache _cache;
        private readonly IModelCostService _modelCostService;
        private const string CacheKeyPrefix = "previous_model_capabilities_";
        private const string PricingCacheKeyPrefix = "previous_model_pricing_";

        public ModelDiscoveryNotificationHandler(
            IServiceProvider serviceProvider,
            IHubContext<ModelDiscoveryHub> hubContext,
            ILogger<ModelDiscoveryNotificationHandler> logger,
            IMemoryCache cache,
            IModelCostService modelCostService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _modelCostService = modelCostService ?? throw new ArgumentNullException(nameof(modelCostService));
        }

        public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
        {
            var message = context.Message;
            _logger.LogInformation(
                "Processing model discovery notification for provider ID {ProviderId} with {ModelCount} models",
                message.ProviderId, message.ModelCapabilities.Count);

            try
            {
                // Check for new models
                var newModels = await CheckForNewModelsAsync(message);
                if (newModels.Any())
                {
                    await NotifyNewModelsDiscoveredAsync(message.ProviderId, newModels, message);
                }

                // Check for capability changes
                var capabilityChanges = await CheckForCapabilityChangesAsync(message);
                foreach (var change in capabilityChanges)
                {
                    await NotifyCapabilityChangedAsync(message.ProviderId, change);
                }

                // Check for pricing updates
                var pricingUpdates = await CheckForPricingUpdatesAsync(message);
                foreach (var update in pricingUpdates)
                {
                    await NotifyPricingUpdatedAsync(message.ProviderId, update);
                }

                // Update cache with current state for next comparison
                await UpdateCacheAsync(message);

                _logger.LogInformation(
                    "Successfully processed model discovery notification for provider ID {ProviderId}",
                    message.ProviderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error processing model discovery notification for provider ID {ProviderId}",
                    message.ProviderId);
                throw;
            }
        }

        private Task<List<DiscoveredModelInfo>> CheckForNewModelsAsync(ModelCapabilitiesDiscovered message)
        {
            var cacheKey = $"{CacheKeyPrefix}provider_{message.ProviderId}";
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
            var cacheKey = $"{CacheKeyPrefix}provider_{message.ProviderId}";
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
                            ProviderId = message.ProviderId,
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
                        var pricingCacheKey = $"{PricingCacheKeyPrefix}provider_{message.ProviderId}_{modelId}";
                        var previousCost = _cache.Get<decimal?>(pricingCacheKey);
                        
                        if (previousCost.HasValue && previousCost.Value != currentCost.InputCostPerMillionTokens)
                        {
                            updates.Add(new ModelPricingUpdate
                            {
                                ModelId = modelId,
                                ProviderId = message.ProviderId,
                                PreviousCost = previousCost.Value,
                                NewCost = currentCost.InputCostPerMillionTokens,
                                CostDetails = currentCost
                            });
                        }
                        
                        // Update cache
                        _cache.Set(pricingCacheKey, currentCost.InputCostPerMillionTokens, TimeSpan.FromDays(7));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Failed to check pricing for model {Model} from provider ID {ProviderId}",
                        modelId, message.ProviderId);
                }
            }

            return updates;
        }

        private async Task NotifyNewModelsDiscoveredAsync(int providerId, List<DiscoveredModelInfo> newModels, ModelCapabilitiesDiscovered message)
        {
            var notification = new NewModelsDiscoveredNotification
            {
                ProviderId = message.ProviderId,
                NewModels = newModels,
                TotalModelCount = message.ModelCapabilities.Count,
                DiscoveredAt = message.DiscoveredAt
            };

            // Try to get optional services
            var severityClassifier = _serviceProvider.GetService<INotificationSeverityClassifier>();
            var batcher = _serviceProvider.GetService<IModelDiscoveryNotificationBatcher>();

            // Determine severity based on provider and model capabilities
            var severity = NotificationSeverity.Low;
            if (severityClassifier != null && newModels.Any())
            {
                severity = newModels.Max(m => severityClassifier.ClassifyNewModel(providerId.ToString(), m));
            }

            if (batcher != null)
            {
                // Queue for provider-specific subscribers
                var providerGroup = $"provider-{providerId}";
                await batcher.QueueNotificationAsync(providerGroup, notification, severity);

                // Queue for global subscribers 
                await batcher.QueueNotificationAsync("model-discovery-all", notification, severity);

                _logger.LogInformation(
                    "Queued new models notification for provider ID {ProviderId}: {Count} new models with severity {Severity}",
                    providerId, newModels.Count, severity);
            }
            else
            {
                _logger.LogDebug(
                    "Model discovery notification batcher not available - skipping batched notifications for {Count} new models from provider ID {ProviderId}",
                    newModels.Count, providerId);
            }
        }

        private async Task NotifyCapabilityChangedAsync(int providerId, ModelCapabilityChange change)
        {
            var notification = new ModelCapabilitiesChangedNotification
            {
                ProviderId = change.ProviderId,
                ModelId = change.ModelId,
                PreviousCapabilities = ConvertToCapabilityInfo(change.PreviousCapabilities),
                NewCapabilities = ConvertToCapabilityInfo(change.NewCapabilities),
                Changes = change.Changes,
                ChangedAt = DateTime.UtcNow
            };

            // Try to get optional services
            var severityClassifier = _serviceProvider.GetService<INotificationSeverityClassifier>();
            var batcher = _serviceProvider.GetService<IModelDiscoveryNotificationBatcher>();

            // Determine severity based on the type of changes
            var severity = NotificationSeverity.Low;
            if (severityClassifier != null)
            {
                severity = severityClassifier.ClassifyCapabilityChange(providerId.ToString(), change.ModelId, change.Changes);
            }

            if (batcher != null)
            {
                // Queue for provider-specific subscribers
                var providerGroup = $"provider-{providerId}";
                await batcher.QueueNotificationAsync(providerGroup, notification, severity);

                // Queue for global subscribers
                await batcher.QueueNotificationAsync("model-discovery-all", notification, severity);

                _logger.LogInformation(
                    "Queued capability change notification for model {Model} from provider ID {ProviderId} with severity {Severity}",
                    change.ModelId, providerId, severity);
            }
            else
            {
                _logger.LogDebug(
                    "Model discovery notification batcher not available - skipping capability change notifications for {Model} from provider ID {ProviderId}",
                    change.ModelId, providerId);
            }
        }

        private async Task NotifyPricingUpdatedAsync(int providerId, ModelPricingUpdate update)
        {
            var percentageChange = ((update.NewCost - update.PreviousCost) / update.PreviousCost) * 100;
            
            var notification = new ModelPricingUpdatedNotification
            {
                ProviderId = update.ProviderId,
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

            // Try to get optional services
            var severityClassifier = _serviceProvider.GetService<INotificationSeverityClassifier>();
            var batcher = _serviceProvider.GetService<IModelDiscoveryNotificationBatcher>();

            // Determine severity based on price change magnitude
            var severity = NotificationSeverity.Low;
            if (severityClassifier != null)
            {
                severity = severityClassifier.ClassifyPriceChange(providerId.ToString(), update.ModelId, percentageChange);
            }

            if (batcher != null)
            {
                // Queue for provider-specific subscribers
                var providerGroup = $"provider-{providerId}";
                await batcher.QueueNotificationAsync(providerGroup, notification, severity);

                // Queue for global subscribers
                await batcher.QueueNotificationAsync("model-discovery-all", notification, severity);

                _logger.LogInformation(
                    "Queued pricing update notification for model {Model} from provider ID {ProviderId}: {Change:F2}% change with severity {Severity}",
                    update.ModelId, providerId, percentageChange, severity);
            }
            else
            {
                _logger.LogDebug(
                    "Model discovery notification batcher not available - skipping pricing update notifications for {Model} from provider ID {ProviderId}",
                    update.ModelId, providerId);
            }
        }

        private Task UpdateCacheAsync(ModelCapabilitiesDiscovered message)
        {
            var cacheKey = $"{CacheKeyPrefix}provider_{message.ProviderId}";
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
            public int ProviderId { get; set; }
            public Core.Events.ModelCapabilities PreviousCapabilities { get; set; } = new();
            public Core.Events.ModelCapabilities NewCapabilities { get; set; } = new();
            public List<string> Changes { get; set; } = new();
        }

        private class ModelPricingUpdate
        {
            public string ModelId { get; set; } = string.Empty;
            public int ProviderId { get; set; }
            public decimal PreviousCost { get; set; }
            public decimal NewCost { get; set; }
            public dynamic? CostDetails { get; set; }
        }
    }
}