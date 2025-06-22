using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles GlobalSettingChanged events for future cache invalidation
    /// Currently logs events for monitoring until cache implementation is added
    /// </summary>
    public class GlobalSettingCacheInvalidationHandler : IConsumer<GlobalSettingChanged>
    {
        private readonly ILogger<GlobalSettingCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the GlobalSettingCacheInvalidationHandler
        /// </summary>
        /// <param name="logger">Logger for diagnostics</param>
        public GlobalSettingCacheInvalidationHandler(
            ILogger<GlobalSettingCacheInvalidationHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Consumes GlobalSettingChanged events and logs them for monitoring
        /// </summary>
        /// <param name="context">The consume context containing the event</param>
        public async Task Consume(ConsumeContext<GlobalSettingChanged> context)
        {
            var @event = context.Message;

            _logger.LogInformation(
                "GlobalSettingChanged event received - SettingId: {SettingId}, Key: {SettingKey}, ChangeType: {ChangeType}",
                @event.SettingId,
                @event.SettingKey,
                @event.ChangeType);

            // If it's an authentication key change, log it with higher importance
            if (@event.SettingKey == "AuthenticationKey" || @event.SettingKey.StartsWith("Auth"))
            {
                _logger.LogWarning(
                    "Authentication setting changed - Key: {SettingKey}, ChangeType: {ChangeType}. Manual service restart may be required for immediate effect.",
                    @event.SettingKey,
                    @event.ChangeType);
            }

            if (@event.ChangedProperties?.Length > 0)
            {
                _logger.LogDebug(
                    "Global setting properties changed: {ChangedProperties}",
                    string.Join(", ", @event.ChangedProperties));
            }

            // TODO: Implement cache invalidation when IGlobalSettingCache is available
            // Future implementation will invalidate specific settings and authentication keys

            await Task.CompletedTask;
        }
    }
}