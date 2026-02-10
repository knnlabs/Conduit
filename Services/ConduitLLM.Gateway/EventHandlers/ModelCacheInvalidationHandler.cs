using ConduitLLM.Core.Consumers;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ModelUpdated events to invalidate discovery cache
    /// Critical for ensuring updated model parameters are reflected in the discovery API
    /// </summary>
    public class ModelCacheInvalidationHandler : CacheInvalidationConsumerBase<ModelUpdated>
    {
        private readonly IDiscoveryCacheService _discoveryCacheService;

        public ModelCacheInvalidationHandler(
            IDiscoveryCacheService discoveryCacheService,
            ILogger<ModelCacheInvalidationHandler> logger)
            : base(logger)
        {
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
        }

        protected override Task InvalidateCacheAsync(ModelUpdated message)
            => _discoveryCacheService.InvalidateAllDiscoveryAsync();

        protected override void LogReceived(ModelUpdated message)
        {
            Logger.LogInformation(
                "Processing ModelUpdated event: {ModelName} (ID: {ModelId}, ChangeType: {ChangeType}, ParametersChanged: {ParametersChanged})",
                message.ModelName,
                message.ModelId,
                message.ChangeType,
                message.ParametersChanged);
        }

        protected override void LogSuccess(ModelUpdated message)
        {
            Logger.LogInformation(
                "Invalidated all discovery cache entries after {ChangeType} of model {ModelName} (ID: {ModelId})",
                message.ChangeType,
                message.ModelName,
                message.ModelId);

            if (message.ParametersChanged)
            {
                Logger.LogInformation(
                    "Model parameters were updated for {ModelName} - UI components will reflect new parameter definitions",
                    message.ModelName);
            }
        }

        protected override void LogFailure(ModelUpdated message, Exception ex)
            => Logger.LogError(ex,
                "Failed to invalidate discovery cache after {ChangeType} of model {ModelName} (ID: {ModelId})",
                message.ChangeType,
                message.ModelName,
                message.ModelId);
    }
}
