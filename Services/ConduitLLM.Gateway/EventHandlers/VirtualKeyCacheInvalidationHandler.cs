using MassTransit;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Gateway.EventHandlers
{
    /// <summary>
    /// Handles Virtual Key events for cache invalidation in the Gateway API
    /// Critical for maintaining cache consistency across all services
    /// </summary>
    public class VirtualKeyCacheInvalidationHandler :
        BatchInvalidationEventHandler<VirtualKeyUpdated>,
        IEventHandler<VirtualKeyCreated>,
        IEventHandler<VirtualKeyDeleted>,
        IEventHandler<SpendUpdated>
    {
        private readonly IBatchCacheInvalidationService _batchService;
        private readonly IVirtualKeyCache? _cache;
        private readonly ILogger<VirtualKeyCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyCacheInvalidationHandler
        /// </summary>
        /// <param name="batchService">Batch cache invalidation service</param>
        /// <param name="cache">Optional virtual key cache (null if Redis not configured)</param>
        /// <param name="logger">Logger instance</param>
        public VirtualKeyCacheInvalidationHandler(
            IBatchCacheInvalidationService batchService,
            IVirtualKeyCache? cache,
            ILogger<VirtualKeyCacheInvalidationHandler> logger)
            : base(batchService, logger)
        {
            _batchService = batchService ?? throw new ArgumentNullException(nameof(batchService));
            _cache = cache;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Extract invalidation requests from VirtualKeyUpdated event
        /// </summary>
        protected override IEnumerable<InvalidationRequest> ExtractInvalidationRequests(VirtualKeyUpdated @event)
        {
            yield return new InvalidationRequest
            {
                EntityType = "VirtualKey",
                EntityId = @event.KeyHash,
                Reason = $"Key updated: {string.Join(", ", @event.ChangedProperties)}",
                Priority = @event.DeterminePriority()
            };
        }

        /// <summary>
        /// Handles VirtualKeyCreated events by invalidating the cache to force a fresh load
        /// </summary>
        /// <param name="message">The virtual key created event</param>
        /// <param name="context">Message context containing the event</param>
        public async Task HandleAsync(VirtualKeyCreated message, IEventContext context)
        {
            var @event = message;

            try
            {
                // Use batch service for invalidation
                await _batchService.QueueInvalidationAsync(
                    @event.KeyHash,
                    @event,
                    CacheType.VirtualKey);
                    
                _logger.LogInformation(
                    "Queued cache invalidation for newly created key {KeyId} (name: {KeyName}, hash: {KeyHash})",
                    @event.KeyId, 
                    @event.KeyName,
                    @event.KeyHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to queue cache invalidation for newly created key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        // VirtualKeyUpdated is handled by the base class

        /// <summary>
        /// Handles VirtualKeyDeleted events by invalidating the cache
        /// </summary>
        /// <param name="message">The virtual key deleted event</param>
        /// <param name="context">Message context containing the event</param>
        public async Task HandleAsync(VirtualKeyDeleted message, IEventContext context)
        {
            var @event = message;

            try
            {
                // Use batch service for invalidation with critical priority
                await _batchService.QueueInvalidationAsync(
                    @event.KeyHash,
                    @event,
                    CacheType.VirtualKey);
                    
                _logger.LogInformation(
                    "Queued critical cache invalidation for deleted key {KeyId} (name: {KeyName})",
                    @event.KeyId, 
                    @event.KeyName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to queue cache invalidation for deleted key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles SpendUpdated events by invalidating the cache for the affected virtual key
        /// </summary>
        /// <param name="message">The spend updated event</param>
        /// <param name="context">Message context containing the event</param>
        public async Task HandleAsync(SpendUpdated message, IEventContext context)
        {
            var @event = message;

            try
            {
                // Use batch service for invalidation with high priority
                await _batchService.QueueInvalidationAsync(
                    @event.KeyHash,
                    @event,
                    CacheType.VirtualKey);
                    
                _logger.LogInformation(
                    "Queued high-priority cache invalidation after spend update for key {KeyId} - new total: {NewTotalSpend}",
                    @event.KeyId, 
                    @event.NewTotalSpend);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to queue cache invalidation after spend update for key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}