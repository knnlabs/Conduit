using ConduitLLM.Configuration.Events;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles ProviderKeyCredential events to invalidate cache
    /// Critical for maintaining cache consistency when keys change
    /// </summary>
    /// <remarks>
    /// Registered via <c>CacheInvalidationMessagingExtensions</c> (handler + bridge per
    /// event type).
    /// </remarks>
    public class ProviderKeyCredentialCacheInvalidationHandler :
        IEventHandler<ProviderKeyCredentialCreated>,
        IEventHandler<ProviderKeyCredentialUpdated>,
        IEventHandler<ProviderKeyCredentialDeleted>,
        IEventHandler<ProviderKeyCredentialPrimaryChanged>,
        IEventHandler<ProviderKeyDisabledEvent>,
        IEventHandler<ProviderKeyReenabledEvent>
    {
        private readonly IProviderCache _cache;
        private readonly ILogger<ProviderKeyCredentialCacheInvalidationHandler> _logger;

        public ProviderKeyCredentialCacheInvalidationHandler(
            IProviderCache cache,
            ILogger<ProviderKeyCredentialCacheInvalidationHandler> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderKeyCredentialCreated events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialCreated @event, IEventContext context)
        {
            _logger.LogInformation(
                "Processing ProviderKeyCredentialCreated event: Key {KeyId} for Provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);

            // Invalidate the provider's cache entry
            await _cache.InvalidateProviderAsync(@event.ProviderId);

            _logger.LogInformation(
                "Successfully invalidated cache after creating key {KeyId} for provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);
        }

        /// <summary>
        /// Handles ProviderKeyCredentialUpdated events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialUpdated @event, IEventContext context)
        {
            _logger.LogInformation(
                "Processing ProviderKeyCredentialUpdated event: Key {KeyId} for Provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);

            // Invalidate the provider's cache entry
            await _cache.InvalidateProviderAsync(@event.ProviderId);

            _logger.LogInformation(
                "Successfully invalidated cache after updating key {KeyId} for provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);
        }

        /// <summary>
        /// Handles ProviderKeyCredentialDeleted events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialDeleted @event, IEventContext context)
        {
            _logger.LogInformation(
                "Processing ProviderKeyCredentialDeleted event: Key {KeyId} for Provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);

            // Invalidate the provider's cache entry
            await _cache.InvalidateProviderAsync(@event.ProviderId);

            _logger.LogInformation(
                "Successfully invalidated cache after deleting key {KeyId} for provider {ProviderId}",
                @event.KeyId,
                @event.ProviderId);
        }

        /// <summary>
        /// Handles ProviderKeyCredentialPrimaryChanged events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialPrimaryChanged @event, IEventContext context)
        {
            _logger.LogInformation(
                "Processing ProviderKeyCredentialPrimaryChanged event: Provider {ProviderId}, Old Primary {OldKeyId}, New Primary {NewKeyId}",
                @event.ProviderId,
                @event.OldPrimaryKeyId,
                @event.NewPrimaryKeyId);

            // Invalidate the provider's cache entry
            await _cache.InvalidateProviderAsync(@event.ProviderId);

            _logger.LogInformation(
                "Successfully invalidated cache after changing primary key for provider {ProviderId}",
                @event.ProviderId);
        }

        /// <summary>
        /// Handles automatic and manual key disables.
        /// </summary>
        public Task HandleAsync(ProviderKeyDisabledEvent @event, IEventContext context) =>
            InvalidateKeyStatusAsync(@event.ProviderId, @event.KeyId, "disabled");

        /// <summary>
        /// Handles automatic and manual key re-enables.
        /// </summary>
        public Task HandleAsync(ProviderKeyReenabledEvent @event, IEventContext context) =>
            InvalidateKeyStatusAsync(@event.ProviderId, @event.KeyId, "re-enabled");

        private async Task InvalidateKeyStatusAsync(int providerId, int keyId, string status)
        {
            _logger.LogInformation(
                "Invalidating provider {ProviderId} cache because key {KeyId} was {Status}",
                providerId,
                keyId,
                status);

            // The cache entry contains both provider enabled state and all key states,
            // so this also covers provider-level changes when the last key is disabled.
            await _cache.InvalidateProviderAsync(providerId);
        }
    }
}
