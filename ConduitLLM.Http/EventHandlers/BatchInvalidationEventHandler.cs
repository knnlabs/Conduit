using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Base class for event handlers that support batch cache invalidation
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event to handle</typeparam>
    public abstract class BatchInvalidationEventHandler<TEvent> : IConsumer<TEvent> 
        where TEvent : class
    {
        private readonly IBatchCacheInvalidationService _batchService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the BatchInvalidationEventHandler
        /// </summary>
        protected BatchInvalidationEventHandler(
            IBatchCacheInvalidationService batchService,
            ILogger logger)
        {
            _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Consumes the event and queues invalidation requests
        /// </summary>
        public async Task Consume(ConsumeContext<TEvent> context)
        {
            try
            {
                var requests = ExtractInvalidationRequests(context.Message);
                
                if (requests.Count() > 0)
                {
                    // Group by cache type for efficient processing
                    var groupedRequests = requests.GroupBy(r => GetCacheType(r));
                    
                    foreach (var group in groupedRequests)
                    {
                        var cacheType = group.Key;
                        var keys = group.Select(r => r.EntityId).ToArray();
                        
                        // Queue bulk invalidation for this cache type
                        if (context.Message is DomainEvent domainEvent)
                        {
                            await _batchService.QueueBulkInvalidationAsync(
                                keys, 
                                domainEvent, 
                                cacheType);
                        }
                    }
                    
                    _logger.LogDebug(
                        "Enqueued {Count} cache invalidation requests from {EventType}",
                        requests.Count(), 
                        typeof(TEvent).Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to process cache invalidation for {EventType}",
                    typeof(TEvent).Name);
                throw; // Let MassTransit handle retry
            }
        }

        /// <summary>
        /// Extract invalidation requests from the event
        /// </summary>
        /// <param name="event">The domain event</param>
        /// <returns>Collection of invalidation requests</returns>
        protected abstract IEnumerable<InvalidationRequest> ExtractInvalidationRequests(TEvent @event);

        /// <summary>
        /// Get the cache type from an invalidation request
        /// </summary>
        private CacheType GetCacheType(InvalidationRequest request)
        {
            return request.EntityType switch
            {
                "VirtualKey" => CacheType.VirtualKey,
                "ModelCost" => CacheType.ModelCost,
                "Provider" => CacheType.Provider,
                "ModelMapping" => CacheType.ModelMapping,
                "GlobalSetting" => CacheType.GlobalSetting,
                "IpFilter" => CacheType.IpFilter,
                _ => throw new ArgumentException($"Unknown entity type: {request.EntityType}")
            };
        }
    }

    /// <summary>
    /// Extension methods for determining invalidation priority
    /// </summary>
    public static class InvalidationPriorityExtensions
    {
        /// <summary>
        /// Determine the priority of a domain event for cache invalidation
        /// </summary>
        public static InvalidationPriority DeterminePriority(this DomainEvent @event)
        {
            return @event switch
            {
                // Critical - Security/billing related
                VirtualKeyDeleted => InvalidationPriority.Critical,
                SpendThresholdExceeded => InvalidationPriority.Critical,
                
                // High - Affects active operations
                VirtualKeyUpdated e when e.ChangedProperties.Contains("IsEnabled") => InvalidationPriority.High,
                VirtualKeyUpdated e when e.ChangedProperties.Contains("MaxBudget") => InvalidationPriority.High,
                SpendUpdated => InvalidationPriority.High,
                
                // Normal - Regular updates
                ModelCostChanged => InvalidationPriority.Normal,
                VirtualKeyCreated => InvalidationPriority.Normal,
                VirtualKeyUpdated => InvalidationPriority.Normal,
                
                // Default
                _ => InvalidationPriority.Normal
            };
        }
    }
}