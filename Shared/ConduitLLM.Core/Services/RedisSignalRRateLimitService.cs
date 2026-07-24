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
        private const int MinuteWindowMs = 60_000;
        private const int DayWindowMs = 86_400_000;

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisSignalRRateLimitService> _logger;
        private readonly ISlidingWindowRateLimiter _slidingWindow;

        public RedisSignalRRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisSignalRRateLimitService> logger)
            : this(redis, logger, new SlidingWindowRateLimiter(
                redis ?? throw new ArgumentNullException(nameof(redis)),
                logger ?? throw new ArgumentNullException(nameof(logger))))
        {
        }

        /// <summary>
        /// Test seam: lets unit tests drive the RPM/RPD matrix through a fake window.
        /// </summary>
        internal RedisSignalRRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisSignalRRateLimitService> logger,
            ISlidingWindowRateLimiter slidingWindow)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _slidingWindow = slidingWindow ?? throw new ArgumentNullException(nameof(slidingWindow));
        }

        /// <summary>
        /// Checks a SignalR method invocation against both the per-minute and per-day windows.
        /// </summary>
        /// <remarks>
        /// Quota semantics: RPM is evaluated first and a denial short-circuits, so a request
        /// rejected by RPM does not consume daily quota. The reverse is not true — an RPD denial
        /// happens after the RPM window has already recorded the invocation, so it costs one
        /// minute-slot. This mirrors the HTTP path and is the same trade-off LiteLLM makes.
        /// When both windows allow, the reported limit is the one with the fewest requests left.
        /// </remarks>
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

            SlidingWindowResult? rpmResult = null;
            SlidingWindowResult? rpdResult = null;

            // Check RPM limit first (more restrictive)
            if (HasLimit(rpmLimit))
            {
                var rpmKey = RedisKeys.SignalRRateLimit.Rpm(virtualKeyHash);
                rpmResult = await _slidingWindow.CheckAsync(rpmKey, now, MinuteWindowMs, rpmLimit!.Value);

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
            }

            // Check RPD limit even when an RPM limit is configured — these are independent
            // windows and a key under its per-minute ceiling can still be over its daily one.
            if (HasLimit(rpdLimit))
            {
                var rpdKey = RedisKeys.SignalRRateLimit.Rpd(virtualKeyHash);
                rpdResult = await _slidingWindow.CheckAsync(rpdKey, now, DayWindowMs, rpdLimit!.Value);

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
            }

            // Allowed by every configured window — report whichever has the least headroom.
            var rpmRemaining = rpmResult is null ? int.MaxValue : Math.Max(0, rpmLimit!.Value - rpmResult.Current);
            var rpdRemaining = rpdResult is null ? int.MaxValue : Math.Max(0, rpdLimit!.Value - rpdResult.Current);

            if (rpmResult is not null && rpmRemaining <= rpdRemaining)
            {
                return new SignalRRateLimitResult
                {
                    IsAllowed = true,
                    RequestsRemaining = rpmRemaining,
                    Limit = rpmLimit!.Value,
                    ResetsAt = DateTime.UtcNow.AddMinutes(1),
                    LimitType = "RPM",
                    ActiveConnections = connectionCount
                };
            }

            if (rpdResult is not null)
            {
                return new SignalRRateLimitResult
                {
                    IsAllowed = true,
                    RequestsRemaining = rpdRemaining,
                    Limit = rpdLimit!.Value,
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

        private static bool HasLimit(int? limit) => limit.HasValue && limit.Value > 0;
        
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