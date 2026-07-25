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
        /// Atomically admits a connection when the key is under its ceiling, or reports that it
        /// is not. Check and increment are one operation: doing them separately let a burst of
        /// simultaneous connections all observe the same pre-increment count and be admitted.
        /// </summary>
        Task<ConnectionLimitResult> TryAcquireConnectionAsync(string virtualKeyHash, int maxConnections);
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
        private const int ConnectionTtlSeconds = 86_400;

        // Admit-if-under-ceiling, in one round trip.
        //   KEYS[1] = the key's connection hash
        //   ARGV[1] = max connections, ARGV[2] = now (unix seconds), ARGV[3] = hash TTL
        // Returns {admitted (0/1), count}
        private const string AcquireConnectionScript = @"
            local current = tonumber(redis.call('HGET', KEYS[1], 'count') or '0')
            if current < 0 then
                current = 0
            end

            if current >= tonumber(ARGV[1]) then
                return {0, current}
            end

            current = redis.call('HINCRBY', KEYS[1], 'count', 1)
            redis.call('HSET', KEYS[1], 'last_connected', ARGV[2])
            redis.call('EXPIRE', KEYS[1], ARGV[3])
            return {1, current}
        ";

        // Decrement clamped at zero, so a disconnect with no matching connect — a node that died
        // holding connections, a replayed lifetime event — cannot drive the count negative and
        // silently hand the key extra capacity.
        private const string ReleaseConnectionScript = @"
            local current = tonumber(redis.call('HGET', KEYS[1], 'count') or '0')
            if current <= 0 then
                redis.call('HSET', KEYS[1], 'count', 0)
                return 0
            end

            return redis.call('HINCRBY', KEYS[1], 'count', -1)
        ";

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
        /// Both windows are evaluated before either is written, so an invocation rejected by the
        /// daily ceiling does not consume a minute slot. When both allow, the reported limit is
        /// whichever has the fewest invocations left, and the reset instant is when that window
        /// genuinely frees room rather than a calendar boundary.
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

            var windows = new List<RateLimitWindow>(2);

            if (HasLimit(rpmLimit))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.SignalRRateLimit.Rpm(virtualKeyHash), "RPM", MinuteWindowMs, rpmLimit!.Value));
            }

            // The daily window is evaluated even when an RPM limit is configured — these are
            // independent, and a key under its per-minute ceiling can still be over its daily one.
            if (HasLimit(rpdLimit))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.SignalRRateLimit.Rpd(virtualKeyHash), "RPD", DayWindowMs, rpdLimit!.Value));
            }

            // Get current connection count
            var connectionCount = await GetConnectionCountAsync(virtualKeyHash);

            if (windows.Count == 0)
            {
                return new SignalRRateLimitResult
                {
                    IsAllowed = true,
                    ActiveConnections = connectionCount
                };
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var result = await _slidingWindow.CheckAsync(windows, now);

            if (!result.IsAllowed && result.DeniedWindow is { } denied)
            {
                _logger.LogWarning("SignalR virtual key {KeyHash} exceeded {LimitType} limit: {Current}/{Limit}",
                    virtualKeyHash, denied.Scope, denied.Current, denied.Limit);

                return new SignalRRateLimitResult
                {
                    IsAllowed = false,
                    DenialReason = denied.Scope == "RPD"
                        ? "Daily rate limit exceeded. Please try again tomorrow."
                        : "Rate limit exceeded. Please try again later.",
                    RequestsRemaining = 0,
                    Limit = (int)denied.Limit,
                    ResetsAt = denied.ResetsAt,
                    LimitType = denied.Scope,
                    ActiveConnections = connectionCount
                };
            }

            var tightest = result.TightestWindow;
            return new SignalRRateLimitResult
            {
                IsAllowed = true,
                RequestsRemaining = tightest is null ? 0 : (int)Math.Min(int.MaxValue, tightest.Remaining),
                Limit = tightest is null ? 0 : (int)tightest.Limit,
                ResetsAt = tightest?.ResetsAt ?? DateTime.UtcNow.AddMinutes(1),
                LimitType = tightest?.Scope ?? "",
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
        
        /// <inheritdoc />
        public async Task<ConnectionLimitResult> TryAcquireConnectionAsync(string virtualKeyHash, int maxConnections)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
            {
                return new ConnectionLimitResult { IsAllowed = true, MaxConnections = maxConnections };
            }

            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeys.SignalRRateLimit.Connections(virtualKeyHash);

                var raw = await db.ScriptEvaluateAsync(
                    AcquireConnectionScript,
                    new RedisKey[] { key },
                    new RedisValue[] { maxConnections, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), ConnectionTtlSeconds });

                var result = (RedisValue[])raw!;
                var admitted = (int)result[0] == 1;
                var current = (int)result[1];

                if (!admitted)
                {
                    return new ConnectionLimitResult
                    {
                        IsAllowed = false,
                        CurrentConnections = current,
                        MaxConnections = maxConnections,
                        DenialReason = $"Connection limit exceeded ({current}/{maxConnections}). Please close existing connections before opening new ones."
                    };
                }

                _logger.LogDebug("Virtual key {KeyHash} connected. Active connections: {Count}",
                    virtualKeyHash, current);

                return new ConnectionLimitResult
                {
                    IsAllowed = true,
                    CurrentConnections = current,
                    MaxConnections = maxConnections
                };
            }
            catch (Exception ex)
            {
                // Fail open, consistent with the other limiters: a Redis outage must not stop
                // clients connecting.
                _logger.LogError(ex, "Error acquiring a connection slot for {KeyHash}; allowing the connection", virtualKeyHash);
                return new ConnectionLimitResult { IsAllowed = true, MaxConnections = maxConnections };
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
                
                // Clamping inside the script keeps the read and the correction atomic. Doing it
                // in two steps let a concurrent connect observe the transient negative value and
                // admit past the ceiling.
                var raw = await db.ScriptEvaluateAsync(ReleaseConnectionScript, new RedisKey[] { key });
                var count = (long)raw!;
                
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

    }
}