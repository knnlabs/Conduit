using ConduitLLM.Core.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Interface for distributed virtual key rate limiting
    /// </summary>
    /// <summary>
    /// Request-count ceilings for one tier of the key → group hierarchy.
    /// </summary>
    public readonly record struct RequestRateLimits(int? Rpm, int? Rpd)
    {
        /// <summary>True when this tier imposes no request-count ceiling at all.</summary>
        public bool IsUnlimited => Rpm is not > 0 && Rpd is not > 0;
    }

    public interface IVirtualKeyRateLimitService
    {
        /// <summary>
        /// Checks a request against the key's own windows and, when the key belongs to a group
        /// with its own ceilings, the group's windows too — all in one atomic operation.
        /// </summary>
        /// <param name="virtualKeyHash">Partition for the key-scope windows.</param>
        /// <param name="keyLimits">The key's own ceilings.</param>
        /// <param name="groupId">Group the key belongs to, or null when it has no group limits.</param>
        /// <param name="groupLimits">Ceilings shared across the group.</param>
        Task<RateLimitCheckResult> CheckRateLimitAsync(
            string virtualKeyHash,
            RequestRateLimits keyLimits,
            int? groupId = null,
            RequestRateLimits groupLimits = default);

        /// <summary>
        /// Reports what each of a key's windows currently holds, for operator display. Reads
        /// only — it never admits a request.
        /// </summary>
        Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash, int? groupId = null);
        
        
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
    /// What a key is currently consuming, across every window that governs it.
    /// </summary>
    public class RateLimitUsage
    {
        public int RequestsThisMinute { get; set; }
        public int RequestsToday { get; set; }

        /// <summary>Prompt plus completion tokens charged to the rolling minute.</summary>
        public long TokensThisMinute { get; set; }

        /// <summary>Requests currently holding a concurrency slot.</summary>
        public int RequestsInFlight { get; set; }

        /// <summary>Group-scope equivalents, present only when the key belongs to a group.</summary>
        public int? GroupRequestsThisMinute { get; set; }
        public int? GroupRequestsToday { get; set; }
        public long? GroupTokensThisMinute { get; set; }
        public int? GroupRequestsInFlight { get; set; }

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

        // Concurrency slots are released explicitly rather than ageing out, so reading them
        // just needs a span comfortably beyond the configured slot lifetime.
        internal const int ConcurrencyReadWindowMs = 86_400_000;

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
        /// Checks every configured request window — key minute, key day, and the group's
        /// equivalents — in a single atomic operation.
        /// </summary>
        /// <remarks>
        /// All windows are evaluated before any is written, so a request rejected by the daily
        /// ceiling does not consume a minute slot and one rejected by a group ceiling does not
        /// consume the key's quota. Group and key limits both apply: the effective ceiling is
        /// whichever is tighter. The reported reset instant is when the window genuinely frees
        /// room — the oldest entry ageing out — because enforcement is a rolling window, not a
        /// calendar-aligned one.
        /// </remarks>
        public async Task<RateLimitCheckResult> CheckRateLimitAsync(
            string virtualKeyHash,
            RequestRateLimits keyLimits,
            int? groupId = null,
            RequestRateLimits groupLimits = default)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));

            var windows = new List<RateLimitWindow>(4);

            if (HasLimit(keyLimits.Rpm))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash), "RPM", MinuteWindowMs, keyLimits.Rpm!.Value));
            }

            if (HasLimit(keyLimits.Rpd))
            {
                windows.Add(new RateLimitWindow(
                    RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash), "RPD", DayWindowMs, keyLimits.Rpd!.Value));
            }

            if (groupId is int group)
            {
                if (HasLimit(groupLimits.Rpm))
                {
                    windows.Add(new RateLimitWindow(
                        RedisKeys.RateLimit.GroupRpm(group), "group:RPM", MinuteWindowMs, groupLimits.Rpm!.Value));
                }

                if (HasLimit(groupLimits.Rpd))
                {
                    windows.Add(new RateLimitWindow(
                        RedisKeys.RateLimit.GroupRpd(group), "group:RPD", DayWindowMs, groupLimits.Rpd!.Value));
                }
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
        
        /// <remarks>
        /// The windows are rolling, so these figures cover the last minute and the last 24 hours
        /// from now — not since a calendar boundary. Reading evicts what has aged out, because
        /// reporting a stale window misleads in exactly the direction that matters.
        /// </remarks>
        public async Task<RateLimitUsage> GetUsageAsync(string virtualKeyHash, int? groupId = null)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var rpm = await _slidingWindow.ReadWindowAsync(
                RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash), now, MinuteWindowMs);
            var rpd = await _slidingWindow.ReadWindowAsync(
                RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash), now, DayWindowMs);
            var tpm = await _slidingWindow.ReadWindowAsync(
                RedisKeys.RateLimit.VirtualKeyTpm(virtualKeyHash), now, MinuteWindowMs);
            var concurrency = await _slidingWindow.ReadWindowAsync(
                RedisKeys.RateLimit.VirtualKeyConcurrency(virtualKeyHash), now, ConcurrencyReadWindowMs);

            var usage = new RateLimitUsage
            {
                MinuteWindowStart = DateTime.UtcNow.AddMinutes(-1),
                DayWindowStart = DateTime.UtcNow.AddDays(-1),
                RequestsThisMinute = (int)rpm.Entries,
                RequestsToday = (int)rpd.Entries,
                TokensThisMinute = tpm.Total,
                RequestsInFlight = (int)concurrency.Entries
            };

            if (groupId is int group)
            {
                usage.GroupRequestsThisMinute = (int)(await _slidingWindow.ReadWindowAsync(
                    RedisKeys.RateLimit.GroupRpm(group), now, MinuteWindowMs)).Entries;
                usage.GroupRequestsToday = (int)(await _slidingWindow.ReadWindowAsync(
                    RedisKeys.RateLimit.GroupRpd(group), now, DayWindowMs)).Entries;
                usage.GroupTokensThisMinute = (await _slidingWindow.ReadWindowAsync(
                    RedisKeys.RateLimit.GroupTpm(group), now, MinuteWindowMs)).Total;
                usage.GroupRequestsInFlight = (int)(await _slidingWindow.ReadWindowAsync(
                    RedisKeys.RateLimit.GroupConcurrency(group), now, ConcurrencyReadWindowMs)).Entries;
            }

            return usage;
        }

        public async Task RemoveRateLimitsAsync(string virtualKeyHash)
        {
            if (string.IsNullOrEmpty(virtualKeyHash))
                throw new ArgumentException("Virtual key hash cannot be null or empty", nameof(virtualKeyHash));

            var db = _redis.GetDatabase();

            var windowKeys = new[]
            {
                RedisKeys.RateLimit.VirtualKeyRpm(virtualKeyHash),
                RedisKeys.RateLimit.VirtualKeyRpd(virtualKeyHash),
                RedisKeys.RateLimit.VirtualKeyTpm(virtualKeyHash),
                RedisKeys.RateLimit.VirtualKeyConcurrency(virtualKeyHash)
            };

            var transaction = db.CreateTransaction();
            foreach (var windowKey in windowKeys)
            {
                _ = transaction.KeyDeleteAsync(windowKey);
                _ = transaction.KeyDeleteAsync(RedisKeys.RateLimit.WindowSum(windowKey));
            }

            await transaction.ExecuteAsync();

            _logger.LogDebug("Removed all rate limit data for virtual key {KeyHash}", virtualKeyHash);
        }
        
    }
}