using ConduitLLM.Core.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Interface for distributed SignalR connection and rate limit tracking
    /// </summary>
    public interface ISignalRRateLimitService
    {
        /// <summary>
        /// Checks if a SignalR method invocation is allowed under rate limits
        /// </summary>
        Task<SignalRRateLimitResult> CheckMethodInvocationAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit);
        
        /// <summary>
        /// Records a connection for a virtual key
        /// </summary>
        Task<int> IncrementConnectionCountAsync(string virtualKeyHash);
        
        /// <summary>
        /// Records a disconnection for a virtual key
        /// </summary>
        Task<int> DecrementConnectionCountAsync(string virtualKeyHash);
        
        /// <summary>
        /// Gets current connection count for a virtual key
        /// </summary>
        Task<int> GetConnectionCountAsync(string virtualKeyHash);
        
        /// <summary>
        /// Cleans up stale connection data
        /// </summary>
        Task CleanupStaleConnectionsAsync(string virtualKeyHash);

        /// <summary>
        /// Checks if a connection would exceed the limit (without incrementing)
        /// </summary>
        Task<ConnectionLimitResult> CheckConnectionLimitAsync(string virtualKeyHash, int maxConnections);
    }
    
    /// <summary>
    /// Result of a SignalR rate limit check
    /// </summary>
    public class SignalRRateLimitResult
    {
        public bool IsAllowed { get; set; }
        public string DenialReason { get; set; } = "";
        public int RequestsRemaining { get; set; }
        public int Limit { get; set; }
        public DateTime ResetsAt { get; set; }
        public string LimitType { get; set; } = ""; // RPM or RPD
        public int ActiveConnections { get; set; }
    }

    /// <summary>
    /// Result of a connection limit check
    /// </summary>
    public class ConnectionLimitResult
    {
        public bool IsAllowed { get; set; }
        public int CurrentConnections { get; set; }
        public int MaxConnections { get; set; }
        public string DenialReason { get; set; } = "";
    }
    
    /// <summary>
    /// Redis-based implementation for distributed SignalR rate limiting
    /// Tracks connections and method invocations across all instances
    /// </summary>
    public class RedisSignalRRateLimitService : ISignalRRateLimitService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisSignalRRateLimitService> _logger;
        private readonly SlidingWindowRateLimiter _slidingWindow;

        public RedisSignalRRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisSignalRRateLimitService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _slidingWindow = new SlidingWindowRateLimiter(redis, logger);
        }
        
        public async Task<SignalRRateLimitResult> CheckMethodInvocationAsync(
            string virtualKeyHash, 
            int? rpmLimit, 
            int? rpdLimit)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
            {
                return new SignalRRateLimitResult { IsAllowed = true };
            }
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Get current connection count
            var connectionCount = await GetConnectionCountAsync(virtualKeyHash);

            // Check RPM limit first (more restrictive)
            if (rpmLimit.HasValue && rpmLimit.Value > 0)
            {
                var rpmKey = RedisKeys.SignalRRateLimit.Rpm(virtualKeyHash);
                var rpmResult = await _slidingWindow.CheckAsync(rpmKey, now, 60000, rpmLimit.Value);
                
                if (!rpmResult.IsAllowed)
                {
                    _logger.LogWarning("SignalR virtual key {KeyHash} exceeded RPM limit: {Current}/{Limit}", 
                        virtualKeyHash, rpmResult.Current, rpmLimit.Value);
                    
                    return new SignalRRateLimitResult
                    {
                        IsAllowed = false,
                        DenialReason = "Rate limit exceeded. Please try again later.",
                        RequestsRemaining = 0,
                        Limit = rpmLimit.Value,
                        ResetsAt = DateTime.UtcNow.AddMinutes(1),
                        LimitType = "RPM",
                        ActiveConnections = connectionCount
                    };
                }
                
                return new SignalRRateLimitResult
                {
                    IsAllowed = true,
                    RequestsRemaining = Math.Max(0, rpmLimit.Value - rpmResult.Current),
                    Limit = rpmLimit.Value,
                    ResetsAt = DateTime.UtcNow.AddMinutes(1),
                    LimitType = "RPM",
                    ActiveConnections = connectionCount
                };
            }
            
            // Check RPD limit
            if (rpdLimit.HasValue && rpdLimit.Value > 0)
            {
                var rpdKey = RedisKeys.SignalRRateLimit.Rpd(virtualKeyHash);
                var rpdResult = await _slidingWindow.CheckAsync(rpdKey, now, 86400000, rpdLimit.Value);
                
                if (!rpdResult.IsAllowed)
                {
                    _logger.LogWarning("SignalR virtual key {KeyHash} exceeded RPD limit: {Current}/{Limit}", 
                        virtualKeyHash, rpdResult.Current, rpdLimit.Value);
                    
                    return new SignalRRateLimitResult
                    {
                        IsAllowed = false,
                        DenialReason = "Daily rate limit exceeded. Please try again tomorrow.",
                        RequestsRemaining = 0,
                        Limit = rpdLimit.Value,
                        ResetsAt = DateTime.UtcNow.Date.AddDays(1),
                        LimitType = "RPD",
                        ActiveConnections = connectionCount
                    };
                }
                
                return new SignalRRateLimitResult
                {
                    IsAllowed = true,
                    RequestsRemaining = Math.Max(0, rpdLimit.Value - rpdResult.Current),
                    Limit = rpdLimit.Value,
                    ResetsAt = DateTime.UtcNow.Date.AddDays(1),
                    LimitType = "RPD",
                    ActiveConnections = connectionCount
                };
            }
            
            // No limits
            return new SignalRRateLimitResult
            {
                IsAllowed = true,
                ActiveConnections = connectionCount
            };
        }
        
        public async Task<int> IncrementConnectionCountAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                return 0;
            
            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeys.SignalRRateLimit.Connections(virtualKeyHash);
                
                var count = await db.HashIncrementAsync(key, "count");
                await db.HashSetAsync(key, "last_connected", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                await db.KeyExpireAsync(key, TimeSpan.FromHours(24));
                
                _logger.LogDebug("Virtual key {KeyHash} connected. Active connections: {Count}", 
                    virtualKeyHash, count);
                
                return (int)count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing connection count for {KeyHash}", virtualKeyHash);
                return 0;
            }
        }
        
        public async Task<int> DecrementConnectionCountAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                return 0;
            
            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeys.SignalRRateLimit.Connections(virtualKeyHash);
                
                var count = await db.HashDecrementAsync(key, "count");
                
                // Ensure count doesn't go negative
                if (count < 0)
                {
                    await db.HashSetAsync(key, "count", 0);
                    count = 0;
                }
                
                await db.HashSetAsync(key, "last_disconnected", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                
                // Clean up if no connections
                if (count == 0)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromMinutes(5));
                }
                
                _logger.LogDebug("Virtual key {KeyHash} disconnected. Active connections: {Count}", 
                    virtualKeyHash, count);
                
                return (int)count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrementing connection count for {KeyHash}", virtualKeyHash);
                return 0;
            }
        }
        
        public async Task<int> GetConnectionCountAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                return 0;
            
            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeys.SignalRRateLimit.Connections(virtualKeyHash);
                
                var count = await db.HashGetAsync(key, "count");
                return count.HasValue ? (int)count : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection count for {KeyHash}", virtualKeyHash);
                return 0;
            }
        }
        
        public async Task CleanupStaleConnectionsAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                return;
            
            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeys.SignalRRateLimit.Connections(virtualKeyHash);
                
                var lastActivity = await db.HashGetAsync(key, "last_disconnected");
                if (lastActivity.HasValue)
                {
                    var lastTime = DateTimeOffset.FromUnixTimeSeconds((long)lastActivity);
                    if (DateTime.UtcNow - lastTime > TimeSpan.FromHours(1))
                    {
                        // Reset count if stale
                        await db.HashSetAsync(key, "count", 0);
                        _logger.LogDebug("Reset stale connection count for {KeyHash}", virtualKeyHash);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up stale connections for {KeyHash}", virtualKeyHash);
            }
        }

        public async Task<ConnectionLimitResult> CheckConnectionLimitAsync(string virtualKeyHash, int maxConnections)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
            {
                return new ConnectionLimitResult { IsAllowed = true, MaxConnections = maxConnections };
            }

            var currentCount = await GetConnectionCountAsync(virtualKeyHash);

            if (currentCount >= maxConnections)
            {
                return new ConnectionLimitResult
                {
                    IsAllowed = false,
                    CurrentConnections = currentCount,
                    MaxConnections = maxConnections,
                    DenialReason = $"Connection limit exceeded ({currentCount}/{maxConnections}). Please close existing connections before opening new ones."
                };
            }

            return new ConnectionLimitResult
            {
                IsAllowed = true,
                CurrentConnections = currentCount,
                MaxConnections = maxConnections
            };
        }

    }
}