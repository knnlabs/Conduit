using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles IpFilterChanged events for future cache invalidation
    /// Currently logs events for monitoring until cache implementation is added
    /// </summary>
    public class IpFilterCacheInvalidationHandler : IConsumer<IpFilterChanged>
    {
        private readonly ILogger<IpFilterCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the IpFilterCacheInvalidationHandler
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        public IpFilterCacheInvalidationHandler(
            ILogger<IpFilterCacheInvalidationHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Consumes IpFilterChanged events and logs them for monitoring
        /// </summary>
        /// <param name="context">The consume context containing the event</param>
        public async Task Consume(ConsumeContext<IpFilterChanged> context)
        {
            var @event = context.Message;

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

            // TODO: Implement cache invalidation when IIpFilterCache is available
            // Future implementation will invalidate key-specific or global filter caches

            await Task.CompletedTask;
        }
    }
}