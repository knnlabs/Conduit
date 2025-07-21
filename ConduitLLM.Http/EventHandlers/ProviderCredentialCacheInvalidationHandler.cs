using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ProviderCredential events to refresh in-memory settings
    /// Critical for maintaining runtime configuration consistency
    /// </summary>
    public class ProviderCredentialCacheInvalidationHandler : 
        IConsumer<ProviderCredentialUpdated>,
        IConsumer<ProviderCredentialDeleted>
    {
        private readonly ISettingsRefreshService _settingsRefreshService;
        private readonly ILogger<ProviderCredentialCacheInvalidationHandler> _logger;

        public ProviderCredentialCacheInvalidationHandler(
            ISettingsRefreshService settingsRefreshService,
            ILogger<ProviderCredentialCacheInvalidationHandler> logger)
        {
            _settingsRefreshService = settingsRefreshService ?? throw new ArgumentNullException(nameof(settingsRefreshService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderCredentialUpdated events by refreshing provider credentials from the database
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderCredentialUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderCredentialUpdated event: {ProviderName} (ID: {ProviderId})",
                    @event.ProviderName,
                    @event.ProviderId);

                // Refresh all provider credentials to ensure consistency
                await _settingsRefreshService.RefreshProviderCredentialsAsync();
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials after update of {ProviderName}",
                    @event.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh provider credentials after update of {ProviderName}", 
                    @event.ProviderName);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderCredentialDeleted events by refreshing provider credentials from the database
        /// </summary>
        public async Task Consume(ConsumeContext<ProviderCredentialDeleted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ProviderCredentialDeleted event: {ProviderName} (ID: {ProviderId})",
                    @event.ProviderName,
                    @event.ProviderId);

                // Refresh all provider credentials to ensure consistency
                await _settingsRefreshService.RefreshProviderCredentialsAsync();
                
                _logger.LogInformation(
                    "Successfully refreshed provider credentials after deletion of {ProviderName}",
                    @event.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh provider credentials after deletion of {ProviderName}", 
                    @event.ProviderName);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}