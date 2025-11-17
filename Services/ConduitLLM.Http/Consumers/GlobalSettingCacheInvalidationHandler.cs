using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

using MassTransit;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Handles GlobalSettingChanged events for future cache invalidation
    /// Currently logs events for monitoring until cache implementation is added
    /// </summary>
    public class GlobalSettingCacheInvalidationHandler : IConsumer<GlobalSettingChanged>
    {
        private readonly IGlobalSettingCache? _globalSettingCache;
        private readonly ILogger<GlobalSettingCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the GlobalSettingCacheInvalidationHandler
        /// </summary>
        /// <param name="globalSettingCache">Optional global setting cache</param>
        /// <param name="logger">Logger for diagnostics</param>
        public GlobalSettingCacheInvalidationHandler(
            IGlobalSettingCache? globalSettingCache,
            ILogger<GlobalSettingCacheInvalidationHandler> logger)
        {
            _globalSettingCache = globalSettingCache;
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

            // Invalidate cache if available
            if (_globalSettingCache != null)
            {
                try
                {
                    // Handle different change types
                    switch (@event.ChangeType)
                    {
                        case "Created":
                        case "Updated":
                            await _globalSettingCache.InvalidateSettingAsync(@event.SettingKey);
                            _logger.LogDebug("Global setting cache invalidated for key: {SettingKey}", @event.SettingKey);
                            break;
                            
                        case "Deleted":
                            await _globalSettingCache.InvalidateSettingAsync(@event.SettingKey);
                            _logger.LogDebug("Global setting cache invalidated for deleted key: {SettingKey}", @event.SettingKey);
                            break;
                            
                        case "BulkUpdate":
                            // For bulk updates, clear all settings to ensure consistency
                            await _globalSettingCache.ClearAllSettingsAsync();
                            _logger.LogWarning("All global setting cache entries cleared due to bulk update");
                            break;
                    }
                    
                    // If it's an auth-related setting, invalidate all auth settings
                    if (@event.SettingKey.StartsWith("Auth", StringComparison.OrdinalIgnoreCase))
                    {
                        await _globalSettingCache.InvalidateAuthenticationSettingsAsync();
                        _logger.LogWarning("All authentication-related cache entries invalidated due to auth setting change");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invalidating global setting cache for key: {SettingKey}", @event.SettingKey);
                }
            }
        }
    }
}