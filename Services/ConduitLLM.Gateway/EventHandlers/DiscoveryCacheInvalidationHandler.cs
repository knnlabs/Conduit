using MassTransit;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles DiscoveryCacheInvalidationRequested events from Admin API
    /// Invalidates the discovery cache across all Gateway API instances
    /// </summary>
    public class DiscoveryCacheInvalidationHandler : IEventHandler<DiscoveryCacheInvalidationRequested>
    {
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<DiscoveryCacheInvalidationHandler> _logger;

        public DiscoveryCacheInvalidationHandler(
            IDiscoveryCacheService discoveryCacheService,
            ILogger<DiscoveryCacheInvalidationHandler> logger)
        {
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles manual discovery cache invalidation requests from Admin API
        /// </summary>
        public async Task HandleAsync(DiscoveryCacheInvalidationRequested message, IEventContext context)
        {
            var @event = message;

            try
            {
                _logger.LogInformation(
                    "Processing discovery cache invalidation request. Reason: {Reason}, Requested by: {RequestedBy}",
                    @event.Reason,
                    @event.RequestedBy);

                // Invalidate all discovery cache entries
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();

                _logger.LogInformation(
                    "Successfully invalidated all discovery cache entries. Reason: {Reason}",
                    @event.Reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate discovery cache. Reason: {Reason}",
                    @event.Reason);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}
