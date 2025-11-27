using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using MassTransit;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Consumes ModelMappingChanged events to invalidate cached model provider mappings
    /// and discovery cache. Ensures cache consistency across distributed deployments
    /// when model mappings are modified.
    /// </summary>
    /// <remarks>
    /// This consumer listens to all model mapping changes (Create, Update, Delete) and
    /// invalidates the corresponding cache entries to prevent stale data.
    ///
    /// Event Sources:
    /// - AdminModelProviderMappingService.AddMappingAsync
    /// - AdminModelProviderMappingService.UpdateMappingAsync
    /// - AdminModelProviderMappingService.DeleteMappingAsync
    /// - AdminModelProviderMappingService.CreateBulkMappingsAsync
    ///
    /// Cache Invalidation Strategy:
    /// - Model Mapping Cache (CacheRegion.ModelMetadata):
    ///   - Invalidates by model alias (most common lookup)
    ///   - Invalidates by mapping ID
    ///   - Invalidates the "all mappings" cache
    /// - Discovery Cache (CacheRegion.ModelDiscovery):
    ///   - Invalidates all discovery entries since model availability may have changed
    ///
    /// This ensures that all API endpoints using cached mappings will get fresh data
    /// on the next request after a configuration change.
    /// </remarks>
    public class ModelMappingCacheInvalidationConsumer : IConsumer<ModelMappingChanged>
    {
        private readonly ICacheManager _cacheManager;
        private readonly IDiscoveryCacheService _discoveryCacheService;
        private readonly ILogger<ModelMappingCacheInvalidationConsumer> _logger;

        // Cache configuration - must match CachedModelProviderMappingService
        private const CacheRegion Region = CacheRegion.ModelMetadata;
        private const string ByAliasKeyPattern = "model:mapping:{0}";
        private const string ByIdKeyPattern = "model:mapping:id:{0}";
        private const string AllMappingsKey = "model:mapping:all";

        public ModelMappingCacheInvalidationConsumer(
            ICacheManager cacheManager,
            IDiscoveryCacheService discoveryCacheService,
            ILogger<ModelMappingCacheInvalidationConsumer> logger)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _discoveryCacheService = discoveryCacheService ?? throw new ArgumentNullException(nameof(discoveryCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events and invalidates the corresponding cache entries.
        /// </summary>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;

            _logger.LogInformation(
                "Processing ModelMappingChanged event: MappingId={MappingId}, ModelAlias={ModelAlias}, ChangeType={ChangeType}",
                @event.MappingId,
                @event.ModelAlias,
                @event.ChangeType);

            // Invalidate model mapping cache
            await InvalidateModelMappingCacheAsync(@event);

            // Invalidate discovery cache - model availability may have changed
            await InvalidateDiscoveryCacheAsync(@event);
        }

        private async Task InvalidateModelMappingCacheAsync(ModelMappingChanged @event)
        {
            try
            {
                var keysToRemove = new List<string>();

                // Always invalidate the ID-based key
                keysToRemove.Add(string.Format(ByIdKeyPattern, @event.MappingId));

                // Invalidate alias-based key (primary lookup path for most operations)
                if (!string.IsNullOrEmpty(@event.ModelAlias))
                {
                    keysToRemove.Add(string.Format(ByAliasKeyPattern, @event.ModelAlias));
                }

                // Invalidate the "all mappings" cache
                keysToRemove.Add(AllMappingsKey);

                // Perform cache invalidation
                var removed = await _cacheManager.RemoveManyAsync(keysToRemove, Region);

                _logger.LogInformation(
                    "Invalidated {Count} model mapping cache entries for {ChangeType} of {ModelAlias} (MappingId={MappingId})",
                    removed,
                    @event.ChangeType,
                    @event.ModelAlias,
                    @event.MappingId);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - cache invalidation failures shouldn't break the event flow
                _logger.LogError(ex,
                    "Failed to invalidate model mapping cache: MappingId={MappingId}, ModelAlias={ModelAlias}, ChangeType={ChangeType}",
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType);
            }
        }

        private async Task InvalidateDiscoveryCacheAsync(ModelMappingChanged @event)
        {
            try
            {
                // Invalidate all discovery cache entries since model availability may have changed
                // This covers all capability-filtered queries (chat, vision, image_generation, etc.)
                await _discoveryCacheService.InvalidateAllDiscoveryAsync();

                _logger.LogInformation(
                    "Invalidated discovery cache after {ChangeType} of {ModelAlias}",
                    @event.ChangeType,
                    @event.ModelAlias);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - cache invalidation failures shouldn't break the event flow
                _logger.LogError(ex,
                    "Failed to invalidate discovery cache: MappingId={MappingId}, ModelAlias={ModelAlias}, ChangeType={ChangeType}",
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType);
            }
        }
    }
}
