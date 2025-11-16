using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using MassTransit;

namespace ConduitLLM.Http.Consumers
{
    /// <summary>
    /// Consumes ModelMappingChanged events to invalidate cached model provider mappings.
    /// Ensures cache consistency across distributed deployments when model mappings are modified.
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
    /// - Invalidates by model alias (most common lookup)
    /// - Invalidates by mapping ID
    /// - Invalidates the "all mappings" cache
    ///
    /// This ensures that all API endpoints using cached mappings will get fresh data
    /// on the next request after a configuration change.
    /// </remarks>
    public class ModelMappingCacheInvalidationConsumer : IConsumer<ModelMappingChanged>
    {
        private readonly ICacheManager _cacheManager;
        private readonly ILogger<ModelMappingCacheInvalidationConsumer> _logger;

        // Cache configuration - must match CachedModelProviderMappingService
        private const CacheRegion Region = CacheRegion.ModelMetadata;
        private const string ByAliasKeyPattern = "model:mapping:{0}";
        private const string ByIdKeyPattern = "model:mapping:id:{0}";
        private const string AllMappingsKey = "model:mapping:all";

        public ModelMappingCacheInvalidationConsumer(
            ICacheManager cacheManager,
            ILogger<ModelMappingCacheInvalidationConsumer> logger)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles ModelMappingChanged events and invalidates the corresponding cache entries.
        /// </summary>
        public async Task Consume(ConsumeContext<ModelMappingChanged> context)
        {
            var @event = context.Message;

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
                    "Invalidated {Count} cache entries for model mapping change: " +
                    "MappingId={MappingId}, ModelAlias={ModelAlias}, ChangeType={ChangeType}, CorrelationId={CorrelationId}",
                    removed,
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType,
                    @event.CorrelationId);
            }
            catch (Exception ex)
            {
                // Log error but don't throw - cache invalidation failures shouldn't break the event flow
                _logger.LogError(ex,
                    "Failed to invalidate cache for model mapping change: " +
                    "MappingId={MappingId}, ModelAlias={ModelAlias}, ChangeType={ChangeType}, CorrelationId={CorrelationId}",
                    @event.MappingId,
                    @event.ModelAlias,
                    @event.ChangeType,
                    @event.CorrelationId);
            }
        }
    }
}
