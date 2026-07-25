using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Atomic fixed-window counter for the security-layer limiters.
    /// </summary>
    /// <remarks>
    /// The security limiters cap abuse per IP rather than metering per key, so a fixed window is
    /// enough — sorted-set precision would be overkill. What they do need is atomicity: a
    /// read-then-write counter undercounts under load, which is exactly when a rate limiter is
    /// supposed to work.
    /// </remarks>
    public interface IFixedWindowCounter
    {
        /// <summary>
        /// Adds one to the window's counter and returns the new total. The window's lifetime is
        /// set when it is first opened and is not extended by later increments, so a client that
        /// keeps hammering cannot postpone its own recovery.
        /// </summary>
        Task<long> IncrementAsync(string key, int windowSeconds);

        /// <summary>Reads the current total without touching it.</summary>
        Task<long> ReadAsync(string key);
    }

    /// <summary>
    /// Redis-backed counter: INCR plus a first-write EXPIRE, in one atomic script.
    /// </summary>
    public sealed class RedisFixedWindowCounter : IFixedWindowCounter
    {
        // Setting the TTL only when the counter is created is what keeps the window fixed:
        // renewing it on every increment would let sustained abuse extend its own block window
        // indefinitely.
        private const string IncrementScript = @"
            local current = redis.call('INCR', KEYS[1])
            if current == 1 then
                redis.call('EXPIRE', KEYS[1], ARGV[1])
            end
            return current
        ";

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger _logger;

        public RedisFixedWindowCounter(IConnectionMultiplexer redis, ILogger logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<long> IncrementAsync(string key, int windowSeconds)
        {
            try
            {
                var result = await _redis.GetDatabase().ScriptEvaluateAsync(
                    IncrementScript,
                    new RedisKey[] { key },
                    new RedisValue[] { windowSeconds });

                return (long)result!;
            }
            catch (Exception ex)
            {
                // Fail open, consistent with every other limiter: a cache outage must not become
                // a data-plane outage.
                _logger.LogWarning(ex, "Rate limit counter unavailable for {Key}; allowing the request", key);
                return 0;
            }
        }

        public async Task<long> ReadAsync(string key)
        {
            try
            {
                var value = await _redis.GetDatabase().StringGetAsync(key);
                return value.HasValue && value.TryParse(out long count) ? count : 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rate limit counter unavailable for {Key}", key);
                return 0;
            }
        }
    }

    /// <summary>
    /// Single-instance fallback for deployments with no Redis.
    /// </summary>
    /// <remarks>
    /// Correct within one process and worthless across several — which is the honest position
    /// when there is no shared store. A deployment that runs more than one Gateway without Redis
    /// has no distributed rate limiting to make atomic in the first place.
    /// </remarks>
    public sealed class MemoryFixedWindowCounter : IFixedWindowCounter
    {
        private readonly IMemoryCache _cache;
        private readonly object _gate = new();

        public MemoryFixedWindowCounter(IMemoryCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public Task<long> IncrementAsync(string key, int windowSeconds)
        {
            lock (_gate)
            {
                var current = _cache.Get<long?>(key) ?? 0;
                current++;

                if (current == 1)
                {
                    _cache.Set(key, current, TimeSpan.FromSeconds(windowSeconds));
                }
                else
                {
                    // Preserve the original expiry: re-setting with a fresh TTL would slide the
                    // window forward on every request.
                    _cache.Set(key, current, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = _cache.Get<DateTimeOffset?>(ExpiryKey(key))
                            ?? DateTimeOffset.UtcNow.AddSeconds(windowSeconds)
                    });
                }

                if (current == 1)
                {
                    _cache.Set(ExpiryKey(key), DateTimeOffset.UtcNow.AddSeconds(windowSeconds),
                        TimeSpan.FromSeconds(windowSeconds));
                }

                return Task.FromResult(current);
            }
        }

        public Task<long> ReadAsync(string key) => Task.FromResult(_cache.Get<long?>(key) ?? 0);

        private static string ExpiryKey(string key) => $"{key}:expires";
    }
}
