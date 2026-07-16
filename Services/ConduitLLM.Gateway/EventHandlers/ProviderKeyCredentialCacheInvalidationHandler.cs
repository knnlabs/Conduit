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
    /// NOTE: this handler is not currently registered on any endpoint (no handler /
    /// bridge registration exists) — a pre-existing gap kept as-is during the epic #909
    /// migration to avoid a behavior change. Register via
    /// <c>AddEventHandler&lt;TEvent, ProviderKeyCredentialCacheInvalidationHandler&gt;</c>
    /// + <c>AddEventBridge&lt;TEvent&gt;</c> to activate it.
    /// </remarks>
    public class ProviderKeyCredentialCacheInvalidationHandler :
        IEventHandler<ProviderKeyCredentialCreated>,
        IEventHandler<ProviderKeyCredentialUpdated>,
        IEventHandler<ProviderKeyCredentialDeleted>,
        IEventHandler<ProviderKeyCredentialPrimaryChanged>
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
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate cache after creating key {KeyId} for provider {ProviderId}",
                    @event.KeyId,
                    @event.ProviderId);
                throw; // Re-throw to trigger the endpoint retry policy
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialUpdated events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialUpdated @event, IEventContext context)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate cache after updating key {KeyId} for provider {ProviderId}",
                    @event.KeyId,
                    @event.ProviderId);
                throw; // Re-throw to trigger the endpoint retry policy
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialDeleted events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialDeleted @event, IEventContext context)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate cache after deleting key {KeyId} for provider {ProviderId}",
                    @event.KeyId,
                    @event.ProviderId);
                throw; // Re-throw to trigger the endpoint retry policy
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialPrimaryChanged events
        /// </summary>
        public async Task HandleAsync(ProviderKeyCredentialPrimaryChanged @event, IEventContext context)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to invalidate cache after changing primary key for provider {ProviderId}",
                    @event.ProviderId);
                throw; // Re-throw to trigger the endpoint retry policy
            }
        }
    }
}
