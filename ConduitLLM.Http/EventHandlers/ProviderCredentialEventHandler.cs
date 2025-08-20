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

                // Provider discovery service removed - model capabilities are now managed through ModelProviderMapping
                _logger.LogDebug("Provider credential updated for provider ID {ProviderId}", @event.ProviderId);
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