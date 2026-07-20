using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Consumers
{
    /// <summary>
    /// Handles <see cref="IpFilterChanged"/> events by invalidating the in-memory IP filter-rules
    /// cache on this replica. The event bus broadcasts to every replica, so a filter change takes
    /// effect immediately across the fleet instead of waiting for the 5-minute cache TTL.
    /// </summary>
    public class IpFilterCacheInvalidationHandler : IEventHandler<IpFilterChanged>
    {
        private readonly IIpFilterService _ipFilterService;
        private readonly ILogger<IpFilterCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the IpFilterCacheInvalidationHandler
        /// </summary>
        public IpFilterCacheInvalidationHandler(
            IIpFilterService ipFilterService,
            ILogger<IpFilterCacheInvalidationHandler> logger)
        {
            _ipFilterService = ipFilterService;
            _logger = logger;
        }

        /// <summary>
        /// Invalidates the live filter-rules cache when an IP filter (or its settings) changes.
        /// </summary>
        public Task HandleAsync(IpFilterChanged message, IEventContext context)
        {
            _logger.LogInformation(
                "IpFilterChanged received - FilterId: {FilterId}, ChangeType: {ChangeType}, FilterType: {FilterType}, IsEnabled: {IsEnabled}",
                message.FilterId, message.ChangeType, message.FilterType, message.IsEnabled);

            try
            {
                _ipFilterService.InvalidateCache();
                _logger.LogInformation("IP filter rules cache invalidated due to filter change event");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating IP filter rules cache");
            }

            return Task.CompletedTask;
        }
    }
}
