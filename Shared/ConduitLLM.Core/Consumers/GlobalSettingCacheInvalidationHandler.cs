using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Consumers
{
    /// <summary>
    /// Consumer that handles GlobalSettingChanged events to invalidate cached settings
    /// across all instances in a distributed deployment.
    ///
    /// This ensures cache consistency when settings are modified via the Admin API.
    /// Both Gateway API and Admin API register this consumer to keep their caches synchronized.
    /// </summary>
    public class GlobalSettingCacheInvalidationHandler : IEventHandler<GlobalSettingChanged>
    {
        private readonly IGlobalSettingsCacheService _cacheService;
        private readonly ILogger<GlobalSettingCacheInvalidationHandler> _logger;

        public GlobalSettingCacheInvalidationHandler(
            IGlobalSettingsCacheService cacheService,
            ILogger<GlobalSettingCacheInvalidationHandler> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task HandleAsync(GlobalSettingChanged message, IEventContext context)
        {
            _logger.LogInformation(
                "Received GlobalSettingChanged event for setting '{SettingKey}' (ID: {SettingId}, ChangeType: {ChangeType})",
                message.SettingKey,
                message.SettingId,
                message.ChangeType);

            await _cacheService.InvalidateSettingAsync(message.SettingKey);

            _logger.LogInformation(
                "Successfully invalidated cache for setting '{SettingKey}'",
                message.SettingKey);
        }
    }
}
