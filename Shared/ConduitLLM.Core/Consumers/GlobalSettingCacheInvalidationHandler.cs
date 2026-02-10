using ConduitLLM.Configuration.Interfaces;
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
    public class GlobalSettingCacheInvalidationHandler : CacheInvalidationConsumerBase<GlobalSettingChanged>
    {
        private readonly IGlobalSettingsCacheService _cacheService;

        public GlobalSettingCacheInvalidationHandler(
            IGlobalSettingsCacheService cacheService,
            ILogger<GlobalSettingCacheInvalidationHandler> logger)
            : base(logger)
        {
            _cacheService = cacheService;
        }

        protected override Task InvalidateCacheAsync(GlobalSettingChanged message)
            => _cacheService.InvalidateSettingAsync(message.SettingKey);

        protected override void LogReceived(GlobalSettingChanged message)
            => Logger.LogInformation(
                "Received GlobalSettingChanged event for setting '{SettingKey}' (ID: {SettingId}, ChangeType: {ChangeType})",
                message.SettingKey,
                message.SettingId,
                message.ChangeType);

        protected override void LogSuccess(GlobalSettingChanged message)
            => Logger.LogInformation(
                "Successfully invalidated cache for setting '{SettingKey}'",
                message.SettingKey);

        protected override void LogFailure(GlobalSettingChanged message, Exception ex)
            => Logger.LogError(
                ex,
                "Failed to invalidate cache for setting '{SettingKey}' (ID: {SettingId})",
                message.SettingKey,
                message.SettingId);
    }
}
