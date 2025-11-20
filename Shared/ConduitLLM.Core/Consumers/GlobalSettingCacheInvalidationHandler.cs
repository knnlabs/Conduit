using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Core.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Consumers
{
    /// <summary>
    /// Consumer that handles GlobalSettingChanged events to invalidate cached settings
    /// across all instances in a distributed deployment.
    ///
    /// This ensures cache consistency when settings are modified via the Admin API.
    /// Both Core API and Admin API register this consumer to keep their caches synchronized.
    /// </summary>
    public class GlobalSettingCacheInvalidationHandler : IConsumer<GlobalSettingChanged>
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

        public async Task Consume(ConsumeContext<GlobalSettingChanged> context)
        {
            var message = context.Message;

            _logger.LogInformation(
                "Received GlobalSettingChanged event for setting '{SettingKey}' (ID: {SettingId}, ChangeType: {ChangeType})",
                message.SettingKey,
                message.SettingId,
                message.ChangeType);

            try
            {
                // Invalidate the specific setting in the cache
                await _cacheService.InvalidateSettingAsync(message.SettingKey);

                _logger.LogInformation(
                    "Successfully invalidated cache for setting '{SettingKey}'",
                    message.SettingKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to invalidate cache for setting '{SettingKey}' (ID: {SettingId})",
                    message.SettingKey,
                    message.SettingId);

                // Rethrow to allow MassTransit retry policy to handle the failure
                throw;
            }
        }
    }
}
