using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of webhook delivery tracking
    /// Provides deduplication and statistics for webhook deliveries
    /// </summary>
    public class RedisWebhookDeliveryTracker : IWebhookDeliveryTracker
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisWebhookDeliveryTracker> _logger;
        private const string DELIVERY_KEY_PREFIX = "webhook:delivered:";
        private const string STATS_KEY_PREFIX = "webhook:stats:";
        private const int DELIVERY_KEY_EXPIRY_HOURS = 24;
        
        public RedisWebhookDeliveryTracker(
            IConnectionMultiplexer redis,
            ILogger<RedisWebhookDeliveryTracker> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <inheritdoc/>
        public async Task<bool> IsDeliveredAsync(string deliveryKey)
        {
            try
            {
                var db = _redis.GetDatabase();
                var result = await db.KeyExistsAsync($"{DELIVERY_KEY_PREFIX}{deliveryKey}");
                
                if (result)
                {
                    _logger.LogDebug("Webhook delivery key {DeliveryKey} already exists", deliveryKey);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking webhook delivery status for key {DeliveryKey}", deliveryKey);
                // In case of Redis failure, assume not delivered to avoid blocking webhooks
                return false;
            }
        }
        
        /// <inheritdoc/>
        public async Task MarkDeliveredAsync(string deliveryKey, string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var transaction = db.CreateTransaction();
                
                // Mark as delivered with expiry
                var deliveryTimestamp = DateTime.UtcNow.ToString("O");
                _ = transaction.StringSetAsync(
                    $"{DELIVERY_KEY_PREFIX}{deliveryKey}", 
                    deliveryTimestamp, 
                    TimeSpan.FromHours(DELIVERY_KEY_EXPIRY_HOURS));
                
                // Update delivery statistics
                var statsKey = $"{STATS_KEY_PREFIX}{webhookUrl}";
                _ = transaction.HashIncrementAsync(statsKey, "delivered", 1);
                _ = transaction.HashSetAsync(statsKey, "last_delivery", deliveryTimestamp);
                
                // Set stats expiry to 30 days
                _ = transaction.KeyExpireAsync(statsKey, TimeSpan.FromDays(30));
                
                var committed = await transaction.ExecuteAsync();
                
                if (committed)
                {
                    _logger.LogInformation(
                        "Marked webhook as delivered: {DeliveryKey} to {WebhookUrl}", 
                        deliveryKey, 
                        webhookUrl);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to commit webhook delivery transaction for {DeliveryKey}", 
                        deliveryKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error marking webhook as delivered: {DeliveryKey} to {WebhookUrl}", 
                    deliveryKey, 
                    webhookUrl);
                // Don't throw - webhook was likely delivered even if tracking failed
            }
        }
        
        /// <inheritdoc/>
        public async Task<WebhookDeliveryStats> GetStatsAsync(string webhookUrl)
        {
            try
            {
                var db = _redis.GetDatabase();
                var statsKey = $"{STATS_KEY_PREFIX}{webhookUrl}";
                var stats = await db.HashGetAllAsync(statsKey);
                
                var deliveredCount = 0L;
                var failedCount = 0L;
                DateTime? lastDeliveryTime = null;
                DateTime? lastFailureTime = null;
                
                foreach (var stat in stats)
                {
                    switch (stat.Name.ToString())
                    {
                        case "delivered":
                            deliveredCount = (long)stat.Value;
                            break;
                        case "failed":
                            failedCount = (long)stat.Value;
                            break;
                        case "last_delivery":
                            if (DateTime.TryParse(stat.Value, out var deliveryTime))
                            {
                                lastDeliveryTime = deliveryTime;
                            }
                            break;
                        case "last_failure":
                            if (DateTime.TryParse(stat.Value, out var failureTime))
                            {
                                lastFailureTime = failureTime;
                            }
                            break;
                    }
                }
                
                return new WebhookDeliveryStats
                {
                    DeliveredCount = deliveredCount,
                    FailedCount = failedCount,
                    LastDeliveryTime = lastDeliveryTime,
                    LastFailureTime = lastFailureTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving webhook stats for {WebhookUrl}", webhookUrl);
                return new WebhookDeliveryStats();
            }
        }
        
        /// <inheritdoc/>
        public async Task RecordFailureAsync(string deliveryKey, string webhookUrl, string error)
        {
            try
            {
                var db = _redis.GetDatabase();
                var transaction = db.CreateTransaction();
                
                // Update failure statistics
                var statsKey = $"{STATS_KEY_PREFIX}{webhookUrl}";
                var failureTimestamp = DateTime.UtcNow.ToString("O");
                
                _ = transaction.HashIncrementAsync(statsKey, "failed", 1);
                _ = transaction.HashSetAsync(statsKey, "last_failure", failureTimestamp);
                _ = transaction.HashSetAsync(statsKey, "last_error", error);
                
                // Set stats expiry to 30 days
                _ = transaction.KeyExpireAsync(statsKey, TimeSpan.FromDays(30));
                
                // Store failure details with shorter expiry
                var failureKey = $"webhook:failure:{deliveryKey}";
                _ = transaction.StringSetAsync(
                    failureKey, 
                    $"{failureTimestamp}|{error}", 
                    TimeSpan.FromHours(6));
                
                var committed = await transaction.ExecuteAsync();
                
                if (committed)
                {
                    _logger.LogWarning(
                        "Recorded webhook failure: {DeliveryKey} to {WebhookUrl} - {Error}", 
                        deliveryKey, 
                        webhookUrl, 
                        error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error recording webhook failure: {DeliveryKey} to {WebhookUrl}", 
                    deliveryKey, 
                    webhookUrl);
            }
        }
    }
}