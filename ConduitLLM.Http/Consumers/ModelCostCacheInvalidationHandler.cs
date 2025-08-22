using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

using MassTransit;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles ModelCostChanged events for future cache invalidation
    /// Currently logs events for monitoring until cache implementation is added
    /// </summary>
    public class ModelCostCacheInvalidationHandler : IConsumer<ModelCostChanged>
    {
        private readonly IModelCostCache? _modelCostCache;
        private readonly ILogger<ModelCostCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCostCacheInvalidationHandler
        /// </summary>
        /// <param name="modelCostCache">Optional model cost cache</param>
        /// <param name="logger">Logger for diagnostics</param>
        public ModelCostCacheInvalidationHandler(
            IModelCostCache? modelCostCache,
            ILogger<ModelCostCacheInvalidationHandler> logger)
        {
            _modelCostCache = modelCostCache;
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

            // Invalidate cache if available
            if (_modelCostCache != null)
            {
                try
                {
                    // Since the ModelCostChanged event structure is not fully defined yet,
                    // we'll do a conservative approach and clear all model costs
                    await _modelCostCache.ClearAllModelCostsAsync();
                    _logger.LogInformation("Model cost cache cleared due to cost change event");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invalidating model cost cache");
                }
            }
        }
    }
}