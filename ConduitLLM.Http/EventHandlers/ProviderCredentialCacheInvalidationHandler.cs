using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles Provider events to refresh in-memory settings and invalidate discovery cache
    /// Critical for maintaining runtime configuration consistency
    /// </summary>
    public class ProviderCacheInvalidationHandler : 
        IConsumer<ProviderCreated>,
        IConsumer<ProviderUpdated>,
        IConsumer<ProviderDeleted>
    {
        private readonly ISettingsRefreshService _settingsRefreshService;
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<ProviderCacheInvalidationHandler> _logger;

        public ProviderCacheInvalidationHandler(
            ISettingsRefreshService settingsRefreshService,
            IDiscoveryCacheService discoveryCacheService,
            ILogger<ProviderCacheInvalidationHandler> logger)
        {
            _settingsRefreshService = settingsRefreshService ?? throw new ArgumentNullException(nameof(settingsRefreshService));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderCreated events by refreshing provider credentials and invalidating discovery cache
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderCreated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderCreated event: Provider ID {ProviderId} ({ProviderName})",
                    @event.ProviderId,
                    @event.ProviderName);

                // Refresh all provider credentials to ensure consistency
                await _settingsRefreshService.RefreshProvidersAsync();
                
                // Invalidate discovery cache as new provider affects available models
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials and invalidated discovery cache after creation of Provider ID {ProviderId}",
                    @event.ProviderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh provider credentials after creation of Provider ID {ProviderId}", 
                    @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderUpdated events by refreshing provider credentials and invalidating discovery cache
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderUpdated event: Provider ID {ProviderId}",
                    @event.ProviderId);

                // Refresh all provider credentials to ensure consistency
                await _settingsRefreshService.RefreshProvidersAsync();
                
                // Invalidate discovery cache if provider enabled status changed
                if (@event.ChangedProperties.Contains("IsEnabled") || 
                    @event.ChangedProperties.Contains("IsActive"))
                {
                    await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                    _logger.LogInformation(
                        "Invalidated discovery cache after status change of Provider ID {ProviderId}",
                        @event.ProviderId);
                }
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials after update of Provider ID {ProviderId}",
                    @event.ProviderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh provider credentials after update of Provider ID {ProviderId}", 
                    @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderDeleted events by refreshing provider credentials from the database
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderDeleted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderDeleted event: Provider ID {ProviderId}",
                    @event.ProviderId);

                // Refresh all provider credentials to ensure consistency
                await _settingsRefreshService.RefreshProvidersAsync();
                
                // Invalidate discovery cache as provider deletion affects available models
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials and invalidated discovery cache after deletion of Provider ID {ProviderId}",
                    @event.ProviderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh provider credentials after deletion of Provider ID {ProviderId}", 
                    @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}