using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles provider credential events in the Core API
    /// Invalidates cached credentials and triggers capability rediscovery
    /// </summary>
    public class ProviderCredentialEventHandler : 
        IConsumer<ProviderCredentialUpdated>,
        IConsumer<ProviderCredentialDeleted>
    {
        private readonly IProviderDiscoveryService? _providerDiscoveryService;
        private readonly IProviderCredentialCache? _providerCredentialCache;
        private readonly ILogger<ProviderCredentialEventHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderCredentialEventHandler
        /// </summary>
        /// <param name="providerDiscoveryService">Optional provider discovery service</param>
        /// <param name="providerCredentialCache">Optional provider credential cache</param>
        /// <param name="logger">Logger instance</param>
        public ProviderCredentialEventHandler(
            IProviderDiscoveryService? providerDiscoveryService,
            IProviderCredentialCache? providerCredentialCache,
            ILogger<ProviderCredentialEventHandler> logger)
        {
            _providerDiscoveryService = providerDiscoveryService;
            _providerCredentialCache = providerCredentialCache;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderCredentialUpdated events
        /// Invalidates cached credentials and triggers capability rediscovery
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ProviderCredentialUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Provider credential updated: {ProviderName} (ID: {ProviderId}), enabled: {IsEnabled}, changed properties: {ChangedProperties}",
                    @event.ProviderName, @event.ProviderId, @event.IsEnabled, string.Join(", ", @event.ChangedProperties));

                // Invalidate provider credential cache if available
                if (_providerCredentialCache != null)
                {
                    await _providerCredentialCache.InvalidateProviderAsync(@event.ProviderId);
                    _logger.LogDebug("Provider credential cache invalidated for provider {ProviderId}", @event.ProviderId);
                }

                // If provider discovery service is available, refresh capabilities
                if (_providerDiscoveryService != null && @event.IsEnabled)
                {
                    try
                    {
                        _logger.LogDebug("Triggering capability rediscovery for provider {ProviderName} (ID: {ProviderId})", 
                            @event.ProviderName, @event.ProviderId);
                        
                        // Note: This will trigger ModelCapabilitiesDiscovered events
                        // which will be handled by other consumers
                        await _providerDiscoveryService.RefreshProviderCapabilitiesAsync(@event.ProviderName);
                        
                        _logger.LogInformation("Capability rediscovery completed for provider {ProviderName}", @event.ProviderName);
                    }
                    catch (Exception discoveryEx)
                    {
                        _logger.LogWarning(discoveryEx, 
                            "Failed to refresh capabilities for provider {ProviderName} - will continue without capability update", 
                            @event.ProviderName);
                        // Don't fail the entire event processing if capability discovery fails
                    }
                }
                else if (!@event.IsEnabled)
                {
                    _logger.LogInformation("Provider {ProviderName} was disabled - skipping capability rediscovery", @event.ProviderName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error handling provider credential update for {ProviderName} (ID: {ProviderId})", 
                    @event.ProviderName, @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderCredentialDeleted events
        /// Cleans up cached data for the deleted provider
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ProviderCredentialDeleted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Provider credential deleted: {ProviderName} (ID: {ProviderId})",
                    @event.ProviderName, @event.ProviderId);

                // Clean up provider-related caches if available
                if (_providerCredentialCache != null)
                {
                    await _providerCredentialCache.InvalidateProviderAsync(@event.ProviderId);
                    _logger.LogDebug("Provider credential cache invalidated for deleted provider {ProviderId}", @event.ProviderId);
                }

                _logger.LogDebug("Cache cleanup completed for deleted provider {ProviderName} (ID: {ProviderId})", 
                    @event.ProviderName, @event.ProviderId);
                    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error handling provider credential deletion for {ProviderName} (ID: {ProviderId})", 
                    @event.ProviderName, @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}