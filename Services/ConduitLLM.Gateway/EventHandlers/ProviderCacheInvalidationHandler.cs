using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles Provider events to refresh in-memory settings and invalidate discovery cache.
    /// Critical for maintaining runtime configuration consistency.
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

        public async Task Consume(ConsumeContext<ProviderCreated> context)
        {
            var @event = context.Message;
            await RefreshAndInvalidateAsync(@event.ProviderId, "creation",
                invalidateDiscovery: true);
        }

        public async Task Consume(ConsumeContext<ProviderUpdated> context)
        {
            var @event = context.Message;
            var invalidateDiscovery = @event.ChangedProperties.Contains("IsEnabled") ||
                                     @event.ChangedProperties.Contains("IsActive");
            await RefreshAndInvalidateAsync(@event.ProviderId, "update",
                invalidateDiscovery: invalidateDiscovery);
        }

        public async Task Consume(ConsumeContext<ProviderDeleted> context)
        {
            var @event = context.Message;
            await RefreshAndInvalidateAsync(@event.ProviderId, "deletion",
                invalidateDiscovery: true);
        }

        private async Task RefreshAndInvalidateAsync(int providerId, string operation, bool invalidateDiscovery)
        {
            try
            {
                _logger.LogInformation(
                    "Processing provider {Operation} event: Provider ID {ProviderId}",
                    operation, providerId);

                await _settingsRefreshService.RefreshProvidersAsync();

                if (invalidateDiscovery)
                {
                    await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                }

                _logger.LogInformation(
                    "Successfully refreshed provider credentials after {Operation} of Provider ID {ProviderId}",
                    operation, providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to refresh provider credentials after {Operation} of Provider ID {ProviderId}",
                    operation, providerId);
                throw;
            }
        }
    }
}
