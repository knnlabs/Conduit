using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles ModelCostChanged events for future cache invalidation
    /// Currently logs events for monitoring until cache implementation is added
    /// </summary>
    public class ModelCostCacheInvalidationHandler : IConsumer<ModelCostChanged>
    {
        private readonly ILogger<ModelCostCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the ModelCostCacheInvalidationHandler
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        public ModelCostCacheInvalidationHandler(
            ILogger<ModelCostCacheInvalidationHandler> logger)
        {
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
                "ModelCostChanged event received - ModelCostId: {ModelCostId}, ModelIdPattern: {ModelIdPattern}, ChangeType: {ChangeType}",
                @event.ModelCostId,
                @event.ModelIdPattern,
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
                    "Model pricing changed for pattern {ModelIdPattern}. This will affect cost calculations for new requests.",
                    @event.ModelIdPattern);
            }

            // TODO: Implement cache invalidation when IModelCostCache is available
            // Future implementation will invalidate model cost and provider model caches

            await Task.CompletedTask;
        }
    }
}