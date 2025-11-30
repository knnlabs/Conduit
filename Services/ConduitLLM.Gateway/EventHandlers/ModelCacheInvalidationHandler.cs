using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ModelUpdated events to invalidate discovery cache
    /// Critical for ensuring updated model parameters are reflected in the discovery API
    /// </summary>
    public class ModelCacheInvalidationHandler : IConsumer<ModelUpdated>
    {
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<ModelCacheInvalidationHandler> _logger;

        public ModelCacheInvalidationHandler(
            IDiscoveryCacheService discoveryCacheService,
            ILogger<ModelCacheInvalidationHandler> logger)
        {
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelUpdated events by invalidating discovery cache
        /// </summary>
        public async Task Consume(ConsumeContext<ModelUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ModelUpdated event: {ModelName} (ID: {ModelId}, ChangeType: {ChangeType}, ParametersChanged: {ParametersChanged})",
                    @event.ModelName,
                    @event.ModelId,
                    @event.ChangeType,
                    @event.ParametersChanged);

                // Invalidate all discovery cache entries
                // This ensures that any capability-filtered queries get fresh data
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                
                _logger.LogInformation(
                    "Invalidated all discovery cache entries after {ChangeType} of model {ModelName} (ID: {ModelId})",
                    @event.ChangeType,
                    @event.ModelName,
                    @event.ModelId);
                
                // Log specific parameter changes for debugging
                if (@event.ParametersChanged)
                {
                    _logger.LogInformation(
                        "Model parameters were updated for {ModelName} - UI components will reflect new parameter definitions",
                        @event.ModelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate discovery cache after {ChangeType} of model {ModelName} (ID: {ModelId})", 
                    @event.ChangeType,
                    @event.ModelName,
                    @event.ModelId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}