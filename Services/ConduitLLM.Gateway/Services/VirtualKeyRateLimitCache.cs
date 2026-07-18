using System.Collections.Concurrent;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Manages virtual key rate limit configurations using Redis for distributed consistency.
    /// This service ensures all instances share the same rate limit configurations.
    /// </summary>
    public class VirtualKeyRateLimitCache : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<VirtualKeyRateLimitCache> _logger;
        private readonly IConnectionMultiplexer? _redis;
        private Timer? _refreshTimer;
        

        /// <summary>
        /// Represents rate limit configuration for a virtual key
        /// </summary>
        public class VirtualKeyRateLimits
        {
            public int? RateLimitRpm { get; set; }
            public int? RateLimitRpd { get; set; }
            public DateTime LastUpdated { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of VirtualKeyRateLimitCache
        /// </summary>
        public VirtualKeyRateLimitCache(
            IServiceProvider serviceProvider,
            ILogger<VirtualKeyRateLimitCache> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            
            // Try to get Redis connection if available
            try
            {
                _redis = serviceProvider.GetService<IConnectionMultiplexer>();
                if (_redis != null && _redis.IsConnected)
                {
                    _logger.LogInformation("VirtualKeyRateLimitCache using Redis for distributed configuration storage");
                }
                else
                {
                    _logger.LogWarning("Redis not available for VirtualKeyRateLimitCache - rate limiting may not work correctly in multi-instance deployments");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Redis connection for VirtualKeyRateLimitCache");
            }
        }

        /// <summary>
        /// Gets rate limits for a virtual key from Redis or database
        /// </summary>
        public VirtualKeyRateLimits? GetRateLimits(string virtualKeyHash)
        {
            try
            {
                // If Redis is available, try to get from Redis first
                if (_redis != null && _redis.IsConnected)
                {
                    var db = _redis.GetDatabase();
                    var key = RedisKeys.RateLimit.Config(virtualKeyHash);
                    
                    var hashEntries = db.HashGetAll(key);
                    if (hashEntries.Length > 0)
                    {
                        var limits = new VirtualKeyRateLimits
                        {
                            LastUpdated = DateTime.UtcNow
                        };
                        
                        foreach (var entry in hashEntries)
                        {
                            if (entry.Name == "rpm" && entry.Value.HasValue)
                            {
                                limits.RateLimitRpm = (int)entry.Value;
                            }
                            else if (entry.Name == "rpd" && entry.Value.HasValue)
                            {
                                limits.RateLimitRpd = (int)entry.Value;
                            }
                            else if (entry.Name == "updated" && entry.Value.HasValue)
                            {
                                var unixTime = (long)entry.Value;
                                limits.LastUpdated = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
                            }
                        }
                        
                        // Check if data is fresh (less than 5 minutes old)
                        if (DateTime.UtcNow - limits.LastUpdated < TimeSpan.FromMinutes(5))
                        {
                            return limits;
                        }
                    }
                }
                
                // Fallback: get from database and cache in Redis
                // This would be done asynchronously in the refresh timer
                // For now, return null to indicate no cached limits available
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting rate limits for virtual key {KeyHash}", virtualKeyHash);
                return null;
            }
        }

        /// <summary>
        /// Updates rate limits for a virtual key in Redis
        /// </summary>
        public void UpdateRateLimits(string virtualKeyHash, int? rpm, int? rpd)
        {
            try
            {
                if (_redis != null && _redis.IsConnected)
                {
                    var db = _redis.GetDatabase();
                    var key = RedisKeys.RateLimit.Config(virtualKeyHash);
                    
                    var transaction = db.CreateTransaction();
                    
                    if (rpm.HasValue)
                        _ = transaction.HashSetAsync(key, "rpm", rpm.Value);
                    else
                        _ = transaction.HashDeleteAsync(key, "rpm");
                    
                    if (rpd.HasValue)
                        _ = transaction.HashSetAsync(key, "rpd", rpd.Value);
                    else
                        _ = transaction.HashDeleteAsync(key, "rpd");
                    
                    _ = transaction.HashSetAsync(key, "updated", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    _ = transaction.KeyExpireAsync(key, TimeSpan.FromDays(7));
                    
                    // Fire and forget - this is a cache update
                    _ = transaction.ExecuteAsync();
                    
                    _logger.LogDebug("Updated rate limit configuration in Redis for virtual key {KeyHash}: RPM={RPM}, RPD={RPD}", 
                        virtualKeyHash, rpm, rpd);
                }
                else
                {
                    _logger.LogWarning("Cannot update rate limits - Redis not available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rate limits for virtual key {KeyHash}", virtualKeyHash);
            }
        }

        /// <summary>
        /// Removes rate limits for a virtual key from Redis
        /// </summary>
        public void RemoveRateLimits(string virtualKeyHash)
        {
            try
            {
                if (_redis != null && _redis.IsConnected)
                {
                    var db = _redis.GetDatabase();
                    var key = RedisKeys.RateLimit.Config(virtualKeyHash);
                    
                    // Fire and forget deletion
                    _ = db.KeyDeleteAsync(key);
                    
                    _logger.LogDebug("Removed rate limit configuration from Redis for virtual key {KeyHash}", virtualKeyHash);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing rate limits for virtual key {KeyHash}", virtualKeyHash);
            }
        }

        /// <summary>
        /// Starts the background service
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Key Rate Limit Cache service starting");
            
            // Refresh rate limits every 30 seconds
            _refreshTimer = new Timer(RefreshRateLimits, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Stops the background service
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Virtual Key Rate Limit Cache service stopping");
            
            _refreshTimer?.Change(Timeout.Infinite, 0);
            _refreshTimer?.Dispose();
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Refreshes rate limits from the database to Redis
        /// </summary>
        private void RefreshRateLimits(object? state)
        {
            try
            {
                // Note: This refresh method is no longer needed since we can't get the full API key
                // from ListVirtualKeysAsync. Rate limits will be cached when keys are validated
                // during authentication. This is actually more efficient as we only cache active keys.
                _logger.LogDebug("Rate limit refresh timer fired - skipping bulk refresh");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in refresh timer");
            }
        }
    }
}