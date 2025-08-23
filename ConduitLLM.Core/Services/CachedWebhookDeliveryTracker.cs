using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Decorator for IWebhookDeliveryTracker that adds in-memory caching
    /// to reduce Redis calls for deduplication checks
    /// </summary>
    public class CachedWebhookDeliveryTracker : IWebhookDeliveryTracker
    {
        private readonly IWebhookDeliveryTracker _innerTracker;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedWebhookDeliveryTracker> _logger;
        private readonly MemoryCacheEntryOptions _cacheOptions;
        
        public CachedWebhookDeliveryTracker(
            IWebhookDeliveryTracker innerTracker,
            IMemoryCache cache,
            ILogger<CachedWebhookDeliveryTracker> logger)
        {
            _innerTracker = innerTracker ?? throw new ArgumentNullException(nameof(innerTracker));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Cache delivered status for 5 minutes to reduce Redis calls
            _cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
        }
        
        public async Task<bool> IsDeliveredAsync(string deliveryKey)
        {
            var cacheKey = $"webhook:delivered:{deliveryKey}";
            
            // Check cache first
            if (_cache.TryGetValue<bool>(cacheKey, out var isDelivered))
            {
                _logger.LogDebug("Webhook delivery status cache hit for {DeliveryKey}: {IsDelivered}", 
                    deliveryKey, isDelivered);
                return isDelivered;
            }
            
            // Cache miss - check Redis
            _logger.LogDebug("Webhook delivery status cache miss for {DeliveryKey}, checking Redis", deliveryKey);
            isDelivered = await _innerTracker.IsDeliveredAsync(deliveryKey);
            
            // Cache the result
            _cache.Set(cacheKey, isDelivered, _cacheOptions);
            
            return isDelivered;
        }
        
        public async Task MarkDeliveredAsync(string deliveryKey, string webhookUrl)
        {
            // Mark as delivered in Redis
            await _innerTracker.MarkDeliveredAsync(deliveryKey, webhookUrl);
            
            // Update cache to indicate delivered
            var cacheKey = $"webhook:delivered:{deliveryKey}";
            _cache.Set(cacheKey, true, _cacheOptions);
            
            _logger.LogDebug("Marked webhook as delivered and cached: {DeliveryKey}", deliveryKey);
        }
        
        public Task<WebhookDeliveryStats> GetStatsAsync(string webhookUrl)
        {
            // Stats don't need caching as they're not in the hot path
            return _innerTracker.GetStatsAsync(webhookUrl);
        }
        
        public Task RecordFailureAsync(string deliveryKey, string webhookUrl, string error)
        {
            // Failures don't affect the delivered status cache
            return _innerTracker.RecordFailureAsync(deliveryKey, webhookUrl, error);
        }
    }
}