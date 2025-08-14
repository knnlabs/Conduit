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
    public class ProviderEventHandler : 
        IConsumer<ProviderUpdated>,
        IConsumer<ProviderDeleted>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ProviderEventHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the ProviderEventHandler
        /// </summary>
        /// <param name="serviceScopeFactory">Service scope factory for resolving optional dependencies</param>
        /// <param name="logger">Logger instance</param>
        public ProviderEventHandler(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ProviderEventHandler> logger)
        {
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ProviderUpdated events
        /// Invalidates cached credentials and triggers capability rediscovery
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ProviderUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Provider credential updated: Provider ID {ProviderId}, enabled: {IsEnabled}, changed properties: {ChangedProperties}",
                    @event.ProviderId, @event.IsEnabled, string.Join(", ", @event.ChangedProperties));

                // Create a scope to get services
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Try to get provider credential cache from service provider
                var ProviderCache = scope.ServiceProvider.GetService<IProviderCache>();
                
                // Invalidate provider credential cache if available
                if (ProviderCache != null)
                {
                    await ProviderCache.InvalidateProviderAsync(@event.ProviderId);
                    _logger.LogDebug("Provider credential cache invalidated for provider {ProviderId}", @event.ProviderId);
                }

                // Try to get provider discovery service from service provider
                var providerDiscoveryService = scope.ServiceProvider.GetService<IProviderDiscoveryService>();
                
                // If provider discovery service is available, refresh capabilities
                if (providerDiscoveryService != null && @event.IsEnabled)
                {
                    try
                    {
                        _logger.LogDebug("Triggering capability rediscovery for provider ID {ProviderId}", 
                            @event.ProviderId);
                        
                        // Note: This will trigger ModelCapabilitiesDiscovered events
                        // which will be handled by other consumers
                        // Pass the provider ID for more accurate refresh
                        await providerDiscoveryService.RefreshProviderCapabilitiesAsync(@event.ProviderId);
                        
                        _logger.LogInformation("Capability rediscovery completed for provider ID {ProviderId}", @event.ProviderId);
                    }
                    catch (Exception discoveryEx)
                    {
                        _logger.LogWarning(discoveryEx, 
                            "Failed to refresh capabilities for provider ID {ProviderId} - will continue without capability update", 
                            @event.ProviderId);
                        // Don't fail the entire event processing if capability discovery fails
                    }
                }
                else if (providerDiscoveryService == null)
                {
                    _logger.LogDebug("Provider discovery service not available in Core API - skipping capability rediscovery");
                }
                else if (!@event.IsEnabled)
                {
                    _logger.LogInformation("Provider ID {ProviderId} was disabled - skipping capability rediscovery", @event.ProviderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error handling provider credential update for provider ID {ProviderId}", 
                    @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles ProviderDeleted events
        /// Cleans up cached data for the deleted provider
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<ProviderDeleted> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Provider credential deleted: Provider ID {ProviderId}",
                    @event.ProviderId);

                // Create a scope to get services
                using var scope = _serviceScopeFactory.CreateScope();
                
                // Try to get provider credential cache from service provider
                var ProviderCache = scope.ServiceProvider.GetService<IProviderCache>();
                
                // Clean up provider-related caches if available
                if (ProviderCache != null)
                {
                    await ProviderCache.InvalidateProviderAsync(@event.ProviderId);
                    _logger.LogDebug("Provider credential cache invalidated for deleted provider {ProviderId}", @event.ProviderId);
                }

                _logger.LogDebug("Cache cleanup completed for deleted provider ID {ProviderId}", 
                    @event.ProviderId);
                    
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error handling provider credential deletion for provider ID {ProviderId}", 
                    @event.ProviderId);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}