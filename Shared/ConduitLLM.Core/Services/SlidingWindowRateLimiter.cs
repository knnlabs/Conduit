using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Result of a sliding window rate limit check.
    /// </summary>
    public class SlidingWindowResult
    {
        public bool IsAllowed { get; set; }
        public int Current { get; set; }
        public int Limit { get; set; }
    }

    /// <summary>
    /// Reusable sliding-window rate limiter backed by Redis sorted sets.
    /// Uses a Lua script for atomic check-and-increment to prevent race conditions.
    /// </summary>
    public class SlidingWindowRateLimiter
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger _logger;

        // Lua script for atomic sliding window rate limit check and increment.
        // Uses sorted sets: timestamp as score, unique member per request.
        // KEYS[1] = the sorted set key
        // ARGV[1] = current time in milliseconds
        // ARGV[2] = window size in milliseconds
        // ARGV[3] = max allowed requests in the window
        // Returns {isAllowed (0/1), currentCount, limit}
        private const string SlidingWindowScript = @"
            local key = KEYS[1]
            local now = tonumber(ARGV[1])
            local window = tonumber(ARGV[2])
            local limit = tonumber(ARGV[3])

            -- Remove old entries outside the window
            redis.call('ZREMRANGEBYSCORE', key, 0, now - window)

            -- Count current entries in window
            local current = redis.call('ZCARD', key)

            -- Check if limit would be exceeded
            if current >= limit then
                return {0, current, limit}
            end

            -- Add new entry with unique member (timestamp:sequence)
            redis.call('ZADD', key, now, now .. ':' .. redis.call('INCR', key .. ':seq'))

            -- Expire both the window and its sequence counter after the same idle period.
            local expiry = math.ceil(window / 1000) + 60
            redis.call('EXPIRE', key, expiry)
            redis.call('EXPIRE', key .. ':seq', expiry)

            -- Return allowed, current count + 1, limit
            return {1, current + 1, limit}
        ";

        public SlidingWindowRateLimiter(IConnectionMultiplexer redis, ILogger logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Atomically checks if a request is within the sliding window limit and,
        /// if allowed, records it. On Redis failure, allows the request to prevent
        /// total service outage.
        /// </summary>
        /// <param name="key">The Redis sorted set key (caller provides the full key including prefix).</param>
        /// <param name="nowMs">Current UTC time in milliseconds since epoch.</param>
        /// <param name="windowMs">Window size in milliseconds (e.g. 60000 for RPM, 86400000 for RPD).</param>
        /// <param name="limit">Maximum number of requests allowed in the window.</param>
        public async Task<SlidingWindowResult> CheckAsync(string key, long nowMs, int windowMs, int limit)
        {
            try
            {
                var db = _redis.GetDatabase();
                var result = await db.ScriptEvaluateAsync(
                    SlidingWindowScript,
                    new RedisKey[] { key },
                    new RedisValue[] { nowMs, windowMs, limit });

                var array = (RedisValue[])result!;
                return new SlidingWindowResult
                {
                    IsAllowed = (int)array[0] == 1,
                    Current = (int)array[1],
                    Limit = (int)array[2]
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing sliding window rate limit script for key {Key}", key);
                // On Redis error, allow the request to prevent total service failure
                return new SlidingWindowResult { IsAllowed = true, Current = 0, Limit = limit };
            }
        }
    }
}
