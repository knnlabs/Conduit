using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ModelUpdated events to invalidate discovery cache
    /// Critical for ensuring updated model parameters are reflected in the discovery API
    /// </summary>
    public class ModelCacheInvalidationHandler : IEventHandler<ModelUpdated>
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
        public async Task HandleAsync(ModelUpdated message, IEventContext context)
        {
            _logger.LogInformation(
                "Processing ModelUpdated event: {ModelName} (ID: {ModelId}, ChangeType: {ChangeType}, ParametersChanged: {ParametersChanged})",
                message.ModelName,
                message.ModelId,
                message.ChangeType,
                message.ParametersChanged);

            try
            {
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();

                _logger.LogInformation(
                    "Invalidated all discovery cache entries after {ChangeType} of model {ModelName} (ID: {ModelId})",
                    message.ChangeType,
                    message.ModelName,
                    message.ModelId);

                if (message.ParametersChanged)
                {
                    _logger.LogInformation(
                        "Model parameters were updated for {ModelName} - UI components will reflect new parameter definitions",
                        message.ModelName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate discovery cache after {ChangeType} of model {ModelName} (ID: {ModelId})",
                    message.ChangeType,
                    message.ModelName,
                    message.ModelId);
                throw; // Re-throw to trigger transport retry logic
            }
        }
    }
}
