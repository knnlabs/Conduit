using MassTransit;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.EventHandlers
{
    /// <summary>
    /// Handles Virtual Key events for cache invalidation in the Core API
    /// Critical for maintaining cache consistency across all services
    /// </summary>
    public class VirtualKeyCacheInvalidationHandler : 
        IConsumer<VirtualKeyCreated>,
        IConsumer<VirtualKeyUpdated>,
        IConsumer<VirtualKeyDeleted>,
        IConsumer<SpendUpdated>
    {
        private readonly IVirtualKeyCache? _cache;
        private readonly ILogger<VirtualKeyCacheInvalidationHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the VirtualKeyCacheInvalidationHandler
        /// </summary>
        /// <param name="cache">Optional virtual key cache (null if Redis not configured)</param>
        /// <param name="logger">Logger instance</param>
        public VirtualKeyCacheInvalidationHandler(
            IVirtualKeyCache? cache,
            ILogger<VirtualKeyCacheInvalidationHandler> logger)
        {
            _cache = cache;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles VirtualKeyCreated events by invalidating the cache to force a fresh load
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<VirtualKeyCreated> context)
        {
            var @event = context.Message;
            
            try
            {
                if (_cache != null)
                {
                    // For a newly created key, we want to ensure any stale cache entries are removed
                    // This forces the next request to load fresh data from the database
                    await _cache.InvalidateVirtualKeyAsync(@event.KeyHash);
                    
                    _logger.LogInformation(
                        "Virtual Key cache invalidated for newly created key {KeyId} (name: {KeyName}, hash: {KeyHash})",
                        @event.KeyId, 
                        @event.KeyName,
                        @event.KeyHash);
                }
                else
                {
                    _logger.LogDebug("Virtual Key cache not configured - skipping invalidation for newly created key {KeyId}", @event.KeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate Virtual Key cache for newly created key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles VirtualKeyUpdated events by invalidating the cache
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<VirtualKeyUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                if (_cache != null)
                {
                    await _cache.InvalidateVirtualKeyAsync(@event.KeyHash);
                    
                    _logger.LogInformation(
                        "Virtual Key cache invalidated for key {KeyId} (hash: {KeyHash}) - properties changed: {ChangedProperties}",
                        @event.KeyId, 
                        @event.KeyHash, 
                        string.Join(", ", @event.ChangedProperties));
                }
                else
                {
                    _logger.LogDebug("Virtual Key cache not configured - skipping invalidation for key {KeyId}", @event.KeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate Virtual Key cache for key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles VirtualKeyDeleted events by invalidating the cache
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<VirtualKeyDeleted> context)
        {
            var @event = context.Message;
            
            try
            {
                if (_cache != null)
                {
                    await _cache.InvalidateVirtualKeyAsync(@event.KeyHash);
                    
                    _logger.LogInformation(
                        "Virtual Key cache invalidated for deleted key {KeyId} (name: {KeyName})",
                        @event.KeyId, 
                        @event.KeyName);
                }
                else
                {
                    _logger.LogDebug("Virtual Key cache not configured - skipping invalidation for deleted key {KeyId}", @event.KeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate Virtual Key cache for deleted key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }

        /// <summary>
        /// Handles SpendUpdated events by invalidating the cache for the affected virtual key
        /// </summary>
        /// <param name="context">Message context containing the event</param>
        public async Task Consume(ConsumeContext<SpendUpdated> context)
        {
            var @event = context.Message;
            
            try
            {
                if (_cache != null)
                {
                    await _cache.InvalidateVirtualKeyAsync(@event.KeyHash);
                    
                    _logger.LogInformation(
                        "Virtual Key cache invalidated after spend update for key {KeyId} - new total: {NewTotalSpend}",
                        @event.KeyId, 
                        @event.NewTotalSpend);
                }
                else
                {
                    _logger.LogDebug("Virtual Key cache not configured - skipping invalidation after spend update for key {KeyId}", @event.KeyId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to invalidate Virtual Key cache after spend update for key {KeyId} (hash: {KeyHash})", 
                    @event.KeyId, @event.KeyHash);
                throw; // Re-throw to trigger MassTransit retry logic
            }
        }
    }
}