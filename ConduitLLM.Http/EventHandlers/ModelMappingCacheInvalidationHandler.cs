using MassTransit;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles ModelMappingChanged events to refresh in-memory settings and invalidate discovery cache
    /// Critical for maintaining runtime configuration consistency
    /// </summary>
    public class ModelMappingCacheInvalidationHandler : IConsumer<ModelMappingChanged>
    {
        private readonly ISettingsRefreshService _settingsRefreshService;
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<ModelMappingCacheInvalidationHandler> _logger;

        public ModelMappingCacheInvalidationHandler(
            ISettingsRefreshService settingsRefreshService,
            IDiscoveryCacheService discoveryCacheService,
            ILogger<ModelMappingCacheInvalidationHandler> logger)
        {
            _settingsRefreshService = settingsRefreshService ?? throw new ArgumentNullException(nameof(settingsRefreshService));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events by refreshing model mappings from the database and invalidating discovery cache
        /// </summary>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;
            
            try
            {
                _logger.LogInformation(
                    "Processing ModelMappingChanged event: {ModelAlias} ({ChangeType})",
                    @event.ModelAlias,
                    @event.ChangeType);

                // Refresh all model mappings to ensure consistency
                await _settingsRefreshService.RefreshModelMappingsAsync();
                
                // Invalidate discovery cache - always use direct invalidation
                // Note: Batch invalidation doesn't support wildcard patterns, and we need to
                // invalidate all discovery cache entries (all, capability:chat, capability:vision, etc.)
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();
                
                _logger.LogInformation(
                    "Invalidated all discovery cache entries after {ChangeType} of {ModelAlias}",
                    @event.ChangeType,
                    @event.ModelAlias);
                
                _logger.LogInformation(
                    "Successfully refreshed model mappings and invalidated discovery cache after {ChangeType} of {ModelAlias}",
                    @event.ChangeType,
                    @event.ModelAlias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to refresh model mappings or invalidate cache after {ChangeType} of {ModelAlias}", 
                    @event.ChangeType,
                    @event.ModelAlias);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}