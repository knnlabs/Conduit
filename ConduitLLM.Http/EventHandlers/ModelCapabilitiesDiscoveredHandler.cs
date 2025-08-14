using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelCapabilitiesDiscovered events to update in-memory caches and notify connected clients.
    /// Uses service locator pattern for optional cross-service dependencies.
    /// </summary>
    public class ModelCapabilitiesDiscoveredHandler : IConsumer<ModelCapabilitiesDiscovered>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ModelCapabilitiesDiscoveredHandler> _logger;
        private const string CacheKeyPrefix = "provider_capabilities_";

        public ModelCapabilitiesDiscoveredHandler(
            IServiceProvider serviceProvider,
            IMemoryCache cache,
            ILogger<ModelCapabilitiesDiscoveredHandler> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Processing ModelCapabilitiesDiscovered event for provider ID {ProviderId} with {ModelCount} models", 
                message.ProviderId, message.ModelCapabilities.Count);

            try
            {
                // Update in-memory cache with discovered capabilities
                var cacheKey = $"{CacheKeyPrefix}provider_{message.ProviderId}";
                
                // Cache the capabilities for 24 hours
                _cache.Set(cacheKey, message.ModelCapabilities, TimeSpan.FromHours(24));
                
                _logger.LogInformation("Updated capability cache for provider ID {ProviderId} with {ModelCount} models", 
                    message.ProviderId, message.ModelCapabilities.Count);
                
                // Log capability changes for monitoring
                foreach (var (modelId, capabilities) in message.ModelCapabilities)
                {
                    _logger.LogDebug("Model {ModelId} capabilities: ImageGen={ImageGen}, Vision={Vision}, Chat={Chat}, FunctionCalling={FunctionCalling}",
                        modelId,
                        capabilities.SupportsImageGeneration,
                        capabilities.SupportsVision,
                        capabilities.AdditionalCapabilities.ContainsKey("chat") ? capabilities.AdditionalCapabilities["chat"] : false,
                        capabilities.SupportsFunctionCalling);
                }
                
                // Future: Update model mappings if service is available
                // var modelMappingService = _serviceProvider.GetService<IModelMappingService>();
                // if (modelMappingService != null)
                // {
                //     try
                //     {
                //         await modelMappingService.UpdateProviderModelsAsync(message.ProviderName, message.ModelCapabilities);
                //         _logger.LogDebug("Model mappings updated for provider {ProviderName}", message.ProviderName);
                //     }
                //     catch (Exception ex)
                //     {
                //         _logger.LogWarning(ex, "Failed to update model mappings - continuing without update");
                //     }
                // }
                
                // Future: Notify WebUI via SignalR
                // var hubContext = _serviceProvider.GetService<IHubContext<ModelCapabilityHub>>();
                // if (hubContext != null)
                // {
                //     await hubContext.Clients.All.SendAsync("ModelCapabilitiesUpdated", new
                //     {
                //         Provider = message.ProviderName,
                //         Models = message.ModelCapabilities.Keys,
                //         Timestamp = message.DiscoveredAt
                //     });
                // }
                
                // Track metrics for monitoring
                if (message.ModelCapabilities.Count() > 0)
                {
                    _logger.LogInformation("Provider ID {ProviderId} discovery metrics: {ImageGenModels} image generation, {VisionModels} vision, {ChatModels} chat models",
                        message.ProviderId,
                        message.ModelCapabilities.Count(m => m.Value.SupportsImageGeneration),
                        message.ModelCapabilities.Count(m => m.Value.SupportsVision),
                        message.ModelCapabilities.Count(m => m.Value.AdditionalCapabilities.ContainsKey("chat") && 
                            (bool)m.Value.AdditionalCapabilities["chat"]));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ModelCapabilitiesDiscovered event for provider ID {ProviderId}", 
                    message.ProviderId);
                throw; // Let MassTransit handle retry
            }

            await Task.CompletedTask;
        }
    }
}