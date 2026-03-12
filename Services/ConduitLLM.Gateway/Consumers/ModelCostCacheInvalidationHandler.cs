using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

using MassTransit;

namespace ConduitLLM.Gateway.Consumers
{
    /// <summary>
    /// Handles ModelCostChanged events for cache invalidation.
    /// Invalidates both the model cost cache and pricing rules cache.
    /// </summary>
    public class ModelCostCacheInvalidationHandler : IConsumer<ModelCostChanged>
    {
        private readonly IModelCostCache? _modelCostCache;
        private readonly ICachedPricingRulesService? _pricingRulesCache;
        private readonly ILogger<ModelCostCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCostCacheInvalidationHandler
        /// </summary>
        /// <param name="modelCostCache">Optional model cost cache</param>
        /// <param name="pricingRulesCache">Optional pricing rules cache</param>
        /// <param name="logger">Logger for diagnostics</param>
        public ModelCostCacheInvalidationHandler(
            IModelCostCache? modelCostCache,
            ICachedPricingRulesService? pricingRulesCache,
            ILogger<ModelCostCacheInvalidationHandler> logger)
        {
            _modelCostCache = modelCostCache;
            _pricingRulesCache = pricingRulesCache;
            _logger = logger;
        }

        /// <summary>
        /// Consumes ModelCostChanged events and logs them for monitoring
        /// </summary>
        /// <param name="context">The consume context containing the event</param>
        public async Task Consume(ConsumeContext<ModelCostChanged> context)
        {
            var @event = context.Message;

            _logger.LogInformation(
                "ModelCostChanged event received - ModelCostId: {ModelCostId}, CostName: {CostName}, ChangeType: {ChangeType}",
                @event.ModelCostId,
                @event.CostName,
                @event.ChangeType);

            if (@event.ChangedProperties?.Length > 0)
            {
                _logger.LogDebug(
                    "Model cost properties changed: {ChangedProperties}",
                    string.Join(", ", @event.ChangedProperties));
            }

            // Log warning for cost changes that might affect billing
            if (@event.ChangeType == "Updated" && 
                (@event.ChangedProperties?.Contains("InputCost") == true || 
                 @event.ChangedProperties?.Contains("OutputCost") == true ||
                 @event.ChangedProperties?.Contains("Cost") == true))
            {
                _logger.LogWarning(
                    "Model pricing changed for cost '{CostName}'. This will affect cost calculations for new requests.",
                    @event.CostName);
            }

            // Invalidate model cost cache if available
            if (_modelCostCache != null)
            {
                try
                {
                    // Clear all model costs to ensure cache consistency
                    await _modelCostCache.ClearAllModelCostsAsync();
                    _logger.LogInformation("Model cost cache cleared due to cost change event");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invalidating model cost cache");
                }
            }

            // Invalidate pricing rules cache if available
            if (_pricingRulesCache != null && @event.ModelCostId > 0)
            {
                try
                {
                    await _pricingRulesCache.InvalidateCacheAsync(@event.ModelCostId);
                    _logger.LogInformation(
                        "Pricing rules cache invalidated for ModelCostId: {ModelCostId}",
                        @event.ModelCostId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invalidating pricing rules cache for ModelCostId: {ModelCostId}", @event.ModelCostId);
                }
            }
        }
    }
}