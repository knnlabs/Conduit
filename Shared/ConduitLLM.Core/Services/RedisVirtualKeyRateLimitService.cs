using ConduitLLM.Core.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Interface for distributed virtual key rate limiting
    /// </summary>
    public interface IVirtualKeyRateLimitService
    {
        /// <summary>
        /// Checks if a request is allowed under the rate limit using sliding window algorithm
        /// </summary>
        Task<RateLimitCheckResult> CheckRateLimitAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit);
        
        /// <summary>
        /// Gets current usage statistics for a virtual key
        /// </summary>
        Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash);
        
        /// <summary>
        /// Updates cached rate limits for a virtual key
        /// </summary>
        Task UpdateRateLimitsAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit);
        
        /// <summary>
        /// Removes all rate limit data for a virtual key
        /// </summary>
        Task RemoveRateLimitsAsync(string virtualKeyHash);
    }
    
    /// <summary>
    /// Result of a rate limit check
    /// </summary>
    public class RateLimitCheckResult
    {
        public bool IsAllowed { get; set; }
        public int RequestsRemaining { get; set; }
        public int Limit { get; set; }

        /// <summary>
        /// When the reported window frees room. On a denial this is the instant at which the
        /// request would actually succeed on retry — the oldest in-window entry ageing out —
        /// not a calendar boundary.
        /// </summary>
        public DateTime ResetsAt { get; set; }
        public string LimitType { get; set; } = ""; // RPM or RPD

        /// <summary>
        /// True when the limiter could not reach Redis and the verdict is a guess.
        /// </summary>
        public bool Degraded { get; set; }
    }
    
    /// <summary>
    /// Current usage statistics
    /// </summary>
    public class RateLimitUsage
    {
        public int RequestsThisMinute { get; set; }
        public int RequestsToday { get; set; }
        public DateTime MinuteWindowStart { get; set; }
        public DateTime DayWindowStart { get; set; }
    }
    
    /// <summary>
    /// Redis-based implementation of virtual key rate limiting
    /// Uses sliding window algorithm with sorted sets for accuracy
    /// </summary>
    public class RedisVirtualKeyRateLimitService : IVirtualKeyRateLimitService
    {
        internal const int MinuteWindowMs = 60_000;
        internal const int DayWindowMs = 86_400_000;

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisVirtualKeyRateLimitService> _logger;
        private readonly ISlidingWindowRateLimiter _slidingWindow;

        public RedisVirtualKeyRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisVirtualKeyRateLimitService> logger)
            : this(redis, logger, new SlidingWindowRateLimiter(
                redis ?? throw new ArgumentNullException(nameof(redis)),
                logger ?? throw new ArgumentNullException(nameof(logger))))
        {
        }

        /// <summary>
        /// Test seam: lets unit tests drive the window matrix without Redis.
        /// </summary>
        internal RedisVirtualKeyRateLimitService(
            IConnectionMultiplexer redis,
            ILogger<RedisVirtualKeyRateLimitService> logger,
            ISlidingWindowRateLimiter slidingWindow)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _slidingWindow = slidingWindow ?? throw new ArgumentNullException(nameof(slidingWindow));
        }

        /// <summary>
        /// Checks the per-minute and per-day windows in a single atomic operation.
        /// </summary>
        /// <remarks>
        /// Both windows are evaluated before either is written, so a request rejected by the
        /// daily ceiling does not consume a minute slot. The reported reset instant is when the
        /// window genuinely frees room — the oldest entry ageing out — because enforcement is a
        /// rolling window, not a calendar-aligned one.
        /// </remarks>
        public async Task<RateLimitCheckResult> CheckRateLimitAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));

            var windows = new List<RateLimitWindow>(2);

            if (HasLimit(rpmLimit))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash), "RPM", MinuteWindowMs, rpmLimit!.Value));
            }

            if (HasLimit(rpdLimit))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash), "RPD", DayWindowMs, rpdLimit!.Value));
            }

            if (windows.Count == 0)
            {
                return new RateLimitCheckResult
                {
                    IsAllowed = true,
                    RequestsRemaining = int.MaxValue,
                    Limit = int.MaxValue,
                    ResetsAt = DateTime.UtcNow.AddHours(1)
                };
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var result = await _slidingWindow.CheckAsync(windows, now);

            if (!result.IsAllowed && result.DeniedWindow is { } denied)
            {
                _logger.LogWarning("Virtual key {KeyHash} exceeded {LimitType} limit: {Current}/{Limit}",
                    virtualKeyHash, denied.Scope, denied.Current, denied.Limit);

                return new RateLimitCheckResult
                {
                    IsAllowed = false,
                    RequestsRemaining = 0,
                    Limit = (int)denied.Limit,
                    ResetsAt = denied.ResetsAt,
                    LimitType = denied.Scope,
                    Degraded = result.Degraded
                };
            }

            var tightest = result.TightestWindow;
            return new RateLimitCheckResult
            {
                IsAllowed = true,
                RequestsRemaining = tightest is null ? int.MaxValue : (int)Math.Min(int.MaxValue, tightest.Remaining),
                Limit = tightest is null ? int.MaxValue : (int)tightest.Limit,
                ResetsAt = tightest?.ResetsAt ?? DateTime.UtcNow.AddHours(1),
                LimitType = tightest?.Scope ?? "",
                Degraded = result.Degraded
            };
        }

        private static bool HasLimit(int? limit) => limit.HasValue && limit.Value > 0;
        
        public async Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            var usage = new RateLimitUsage
            {
                MinuteWindowStart = DateTime.UtcNow.AddMinutes(-1),
                DayWindowStart = DateTime.UtcNow.Date
            };
            
            // Get RPM usage
            var rpmKey = RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash);
            await db.SortedSetRemoveRangeByScoreAsync(rpmKey, 0, now - 60000);
            usage.RequestsThisMinute = (int)await db.SortedSetLengthAsync(rpmKey);

            // Get RPD usage
            var rpdKey = RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash);
            await db.SortedSetRemoveRangeByScoreAsync(rpdKey, 0, now - 86400000);
            usage.RequestsToday = (int)await db.SortedSetLengthAsync(rpdKey);
            
            return usage;
        }
        
        public async Task UpdateRateLimitsAsync(string virtualKeyHash, int? rpmLimit, int? rpdLimit)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            var limitsKey = RedisKeys.RateLimit.VirtualKeyLimits(virtualKeyHash);
            
            var transaction = db.CreateTransaction();
            
            if (rpmLimit.HasValue)
                _ = transaction.HashSetAsync(limitsKey, "rpm", rpmLimit.Value);
            else
                _ = transaction.HashDeleteAsync(limitsKey, "rpm");
            
            if (rpdLimit.HasValue)
                _ = transaction.HashSetAsync(limitsKey, "rpd", rpdLimit.Value);
            else
                _ = transaction.HashDeleteAsync(limitsKey, "rpd");
            
            _ = transaction.HashSetAsync(limitsKey, "updated", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _ = transaction.KeyExpireAsync(limitsKey, TimeSpan.FromDays(7));
            
            await transaction.ExecuteAsync();
            
            _logger.LogDebug("Updated rate limits for virtual key {KeyHash}: RPM={RPM}, RPD={RPD}", 
                virtualKeyHash, rpmLimit, rpdLimit);
        }
        
        public async Task RemoveRateLimitsAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));
            
            var db = _redis.GetDatabase();
            
            var rpmKey = RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash);
            var rpdKey = RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash);

            var transaction = db.CreateTransaction();
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.VirtualKeyLimits(virtualKeyHash));
            _ = transaction.KeyDeleteAsync(rpmKey);
            _ = transaction.KeyDeleteAsync(rpdKey);
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.WindowSum(rpmKey));
            _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.WindowSum(rpdKey));
            
            await transaction.ExecuteAsync();
            
            _logger.LogDebug("Removed all rate limit data for virtual key {KeyHash}", virtualKeyHash);
        }
        
    }
}