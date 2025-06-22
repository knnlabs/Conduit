using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelCapabilitiesDiscovered events to update in-memory caches and notify connected clients.
    /// </summary>
    public class ModelCapabilitiesDiscoveredHandler : IConsumer<ModelCapabilitiesDiscovered>
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ModelCapabilitiesDiscoveredHandler> _logger;
        private const string CacheKeyPrefix = "provider_capabilities_";

        public ModelCapabilitiesDiscoveredHandler(
            IMemoryCache cache,
            ILogger<ModelCapabilitiesDiscoveredHandler> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ModelCapabilitiesDiscovered> context)
        {
            var message = context.Message;
            
            _logger.LogInformation("Processing ModelCapabilitiesDiscovered event for provider {ProviderName} with {ModelCount} models", 
                message.ProviderName, message.ModelCapabilities.Count);

            try
            {
                // Update in-memory cache with discovered capabilities
                var cacheKey = $"{CacheKeyPrefix}{message.ProviderName.ToLowerInvariant()}";
                
                // Cache the capabilities for 24 hours
                _cache.Set(cacheKey, message.ModelCapabilities, TimeSpan.FromHours(24));
                
                _logger.LogInformation("Updated capability cache for provider {ProviderName} with {ModelCount} models", 
                    message.ProviderName, message.ModelCapabilities.Count);
                
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
                
                // Future: Notify WebUI via SignalR
                // await _hubContext.Clients.All.SendAsync("ModelCapabilitiesUpdated", new
                // {
                //     Provider = message.ProviderName,
                //     Models = message.ModelCapabilities.Keys,
                //     Timestamp = message.DiscoveredAt
                // });
                
                // Track metrics for monitoring
                if (message.ModelCapabilities.Count > 0)
                {
                    _logger.LogInformation("Provider {ProviderName} discovery metrics: {ImageGenModels} image generation, {VisionModels} vision, {ChatModels} chat models",
                        message.ProviderName,
                        message.ModelCapabilities.Count(m => m.Value.SupportsImageGeneration),
                        message.ModelCapabilities.Count(m => m.Value.SupportsVision),
                        message.ModelCapabilities.Count(m => m.Value.AdditionalCapabilities.ContainsKey("chat") && 
                            (bool)m.Value.AdditionalCapabilities["chat"]));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ModelCapabilitiesDiscovered event for provider {ProviderName}", 
                    message.ProviderName);
                throw; // Let MassTransit handle retry
            }

            await Task.CompletedTask;
        }
    }
}