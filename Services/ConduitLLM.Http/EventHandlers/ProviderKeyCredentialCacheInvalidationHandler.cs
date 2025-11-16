using MassTransit;
using ConduitLLM.Configuration.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ProviderKeyCredential events to invalidate cache
    /// Critical for maintaining cache consistency when keys change
    /// </summary>
    public class ProviderKeyCredentialCacheInvalidationHandler : 
        IConsumer<ProviderKeyCredentialCreated>,
        IConsumer<ProviderKeyCredentialUpdated>,
        IConsumer<ProviderKeyCredentialDeleted>,
        IConsumer<ProviderKeyCredentialPrimaryChanged>
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
        public async Task Consume(ConsumeContext<ProviderKeyCredentialCreated> context)
        {
            var @event = context.Message;
            
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
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialUpdated events
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderKeyCredentialUpdated> context)
        {
            var @event = context.Message;
            
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
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialDeleted events
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderKeyCredentialDeleted> context)
        {
            var @event = context.Message;
            
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
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderKeyCredentialPrimaryChanged events
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderKeyCredentialPrimaryChanged> context)
        {
            var @event = context.Message;
            
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
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}