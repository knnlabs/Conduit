using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

using MassTransit;

namespace ConduitLLM.Gateway.Consumers
{
    /// <summary>
    /// Handles IpFilterChanged events for cache invalidation.
    /// Invalidates the Redis-based IP filter cache when filters are modified.
    /// </summary>
    public class IpFilterCacheInvalidationHandler : IEventHandler<IpFilterChanged>
    {
        private readonly IIpFilterCache? _ipFilterCache;
        private readonly ILogger<IpFilterCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the IpFilterCacheInvalidationHandler
        /// </summary>
        /// <param name="ipFilterCache">Optional IP filter cache</param>
        /// <param name="logger">Logger for diagnostics</param>
        public IpFilterCacheInvalidationHandler(
            IIpFilterCache? ipFilterCache,
            ILogger<IpFilterCacheInvalidationHandler> logger)
        {
            _ipFilterCache = ipFilterCache;
            _logger = logger;
        }

        /// <summary>
        /// Consumes IpFilterChanged events and logs them for monitoring
        /// </summary>
        /// <param name="message">The IP filter change event</param>
        /// <param name="context">The consume context containing the event</param>
        public async Task HandleAsync(IpFilterChanged message, IEventContext context)
        {
            var @event = message;

            _logger.LogInformation(
                "IpFilterChanged event received - FilterId: {FilterId}, IP: {IpAddressOrCidr}, ChangeType: {ChangeType}, FilterType: {FilterType}, IsEnabled: {IsEnabled}",
                @event.FilterId,
                @event.IpAddressOrCidr,
                @event.ChangeType,
                @event.FilterType,
                @event.IsEnabled);

            // Log warning for global filter changes
            if (@event.FilterType == "global")
            {
                _logger.LogWarning(
                    "Global IP filter changed - FilterId: {FilterId}, IP: {IpAddressOrCidr}. This affects all API access.",
                    @event.FilterId,
                    @event.IpAddressOrCidr);
            }

            if (!string.IsNullOrEmpty(@event.Description))
            {
                _logger.LogDebug(
                    "IP filter description: {Description}",
                    @event.Description);
            }

            if (@event.ChangedProperties?.Length > 0)
            {
                _logger.LogDebug(
                    "IP filter properties changed: {ChangedProperties}",
                    string.Join(", ", @event.ChangedProperties));
            }

            // Invalidate cache if available
            if (_ipFilterCache != null)
            {
                try
                {
                    // Clear all filters to ensure consistency across global and key-specific caches
                    await _ipFilterCache.ClearAllFiltersAsync();
                    _logger.LogInformation("IP filter cache cleared due to filter change event");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invalidating IP filter cache");
                }
            }
        }
    }
}