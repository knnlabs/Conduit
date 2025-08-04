using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles Provider events to refresh in-memory settings
    /// Critical for maintaining runtime configuration consistency
    /// </summary>
    public class ProviderCacheInvalidationHandler : 
        IConsumer<ProviderUpdated>,
        IConsumer<ProviderDeleted>
    {
        private readonly ISettingsRefreshService _settingsRefreshService;
        private readonly ILogger<ProviderCacheInvalidationHandler> _logger;

        public ProviderCacheInvalidationHandler(
            ISettingsRefreshService settingsRefreshService,
            ILogger<ProviderCacheInvalidationHandler> logger)
        {
            _settingsRefreshService = settingsRefreshService ?? throw new ArgumentNullException(nameof(settingsRefreshService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderUpdated events by refreshing provider credentials from the database
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
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials after deletion of Provider ID {ProviderId}",
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