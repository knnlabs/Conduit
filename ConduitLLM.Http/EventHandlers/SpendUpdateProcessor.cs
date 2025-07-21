using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration.Repositories;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Processes spend update requests in ordered fashion per virtual key
    /// Eliminates race conditions and dual update paths
    /// Uses service locator pattern to handle cross-service dependencies gracefully
    /// </summary>
    public class SpendUpdateProcessor : IConsumer<SpendUpdateRequested>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<SpendUpdateProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the SpendUpdateProcessor
        /// </summary>
        /// <param name="serviceProvider">Service provider for resolving optional dependencies</param>
        /// <param name="publishEndpoint">MassTransit publish endpoint for publishing events</param>
        /// <param name="logger">Logger instance</param>
        public SpendUpdateProcessor(
            IServiceProvider serviceProvider,
            IPublishEndpoint publishEndpoint,
            ILogger<SpendUpdateProcessor> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes spend update requests in ordered fashion
        /// This replaces the dual update paths (individual + batch) with single ordered processing
        /// </summary>
        /// <param name="context">Message context containing the spend update request</param>
        public async Task Consume(ConsumeContext<SpendUpdateRequested> context)
        {
            var request = context.Message;
            
            if (request.Amount <= 0)
            {
                _logger.LogDebug("Spend update request for key {KeyId} has zero or negative amount {Amount} - skipping", 
                    request.KeyId, request.Amount);
                return;
            }

            // Get virtual key repository from service provider
            var virtualKeyRepository = _serviceProvider.GetService<IVirtualKeyRepository>();
            
            if (virtualKeyRepository == null)
            {
                _logger.LogWarning(
                    "Virtual key repository not available - cannot process spend update for key {KeyId}. " +
                    "This is expected in Core API context where repository is not registered.",
                    request.KeyId);
                
                // Still publish the event so other services can react
                // This allows the Admin API or other services to handle the update
                await _publishEndpoint.Publish(new SpendUpdateDeferred
                {
                    KeyId = request.KeyId,
                    Amount = request.Amount,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId,
                    Reason = "Repository not available in current context"
                });
                
                return;
            }

            try
            {
                _logger.LogDebug("Processing spend update request for key {KeyId}: amount {Amount}, requestId {RequestId}",
                    request.KeyId, request.Amount, request.RequestId);

                // Get current virtual key state
                var virtualKey = await virtualKeyRepository.GetByIdAsync(request.KeyId);
                if (virtualKey == null)
                {
                    _logger.LogWarning("Spend update request for non-existent virtual key {KeyId} - ignoring", request.KeyId);
                    return;
                }

                // Calculate new spend total
                var previousSpend = virtualKey.CurrentSpend;
                var newSpend = previousSpend + request.Amount;
                
                // Update the virtual key spend
                virtualKey.CurrentSpend = newSpend;
                virtualKey.UpdatedAt = DateTime.UtcNow;
                
                var success = await virtualKeyRepository.UpdateAsync(virtualKey);
                
                if (success)
                {
                    // Publish SpendUpdated event for cache invalidation and audit
                    await _publishEndpoint.Publish(new SpendUpdated
                    {
                        KeyId = request.KeyId,
                        KeyHash = virtualKey.KeyHash,
                        Amount = request.Amount,
                        NewTotalSpend = newSpend,
                        RequestId = request.RequestId,
                        CorrelationId = request.CorrelationId
                    });

                    _logger.LogInformation(
                        "Spend updated for virtual key {KeyId}: {PreviousSpend} + {Amount} = {NewSpend} (requestId: {RequestId})",
                        request.KeyId, previousSpend, request.Amount, newSpend, request.RequestId);
                }
                else
                {
                    _logger.LogError("Failed to update spend for virtual key {KeyId} - database update returned false", request.KeyId);
                    throw new InvalidOperationException($"Failed to update spend for virtual key {request.KeyId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error processing spend update for virtual key {KeyId}, amount {Amount}, requestId {RequestId}", 
                    request.KeyId, request.Amount, request.RequestId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}