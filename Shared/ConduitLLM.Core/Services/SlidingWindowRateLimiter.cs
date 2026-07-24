using System.Globalization;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// One window to evaluate: a Redis sorted set, its span, its ceiling, and how much
    /// this request costs against it.
    /// </summary>
    /// <param name="Key">Full Redis key of the sorted set (caller owns the prefix).</param>
    /// <param name="Scope">
    /// Label reported to clients in <c>X-RateLimit-Scope</c> when this window denies —
    /// e.g. <c>rpm</c>, <c>rpd</c>, <c>tpm</c>, <c>group:rpm</c>, <c>model:gpt-5:tpm</c>.
    /// </param>
    /// <param name="WindowMs">Window span in milliseconds (60000 for a minute, 86400000 for a day).</param>
    /// <param name="Limit">Maximum total weight permitted inside the window.</param>
    /// <param name="Weight">
    /// What this request costs. 1 for request-counting windows; an estimated token count for TPM.
    /// </param>
    /// <param name="UnitWeight">
    /// True when every entry in this window weighs exactly 1. Lets the limiter find the
    /// retry-at instant by rank (O(log n)) instead of walking the window.
    /// </param>
    public sealed record RateLimitWindow(
        string Key,
        string Scope,
        int WindowMs,
        long Limit,
        long Weight = 1,
        bool UnitWeight = true);

    /// <summary>
    /// State of a single window after a check.
    /// </summary>
    public sealed class RateLimitWindowState
    {
        public string Scope { get; init; } = "";

        /// <summary>Total weight inside the window — including this request when it was admitted.</summary>
        public long Current { get; init; }

        public long Limit { get; init; }

        /// <summary>
        /// When this window frees enough room for the request. For an admitted request this is
        /// when its own slot ages out; for the window that denied, it is the instant at which
        /// enough weight has aged out for the request to fit — the real <c>Retry-After</c>.
        /// </summary>
        public DateTime ResetsAt { get; init; }

        public long Remaining => Math.Max(0, Limit - Current);
    }

    /// <summary>
    /// Outcome of an all-or-nothing check across every configured window.
    /// </summary>
    public sealed class MultiWindowRateLimitResult
    {
        public bool IsAllowed { get; init; }

        /// <summary>
        /// True when the limiter could not reach Redis. The check result is a guess, and the
        /// caller decides whether to fail open or closed.
        /// </summary>
        public bool Degraded { get; init; }

        /// <summary>
        /// Identifier of the entry written to every window, for later reconcile/release.
        /// Null when nothing was written (denied, no windows, or degraded).
        /// </summary>
        public string? EntryId { get; init; }

        public IReadOnlyList<RateLimitWindowState> Windows { get; init; } = Array.Empty<RateLimitWindowState>();

        /// <summary>The window that rejected the request, or null when it was admitted.</summary>
        public RateLimitWindowState? DeniedWindow { get; init; }

        /// <summary>
        /// The window with the least headroom — what clients should be told about on an
        /// allowed response so they pace against the binding constraint.
        /// </summary>
        public RateLimitWindowState? TightestWindow
        {
            get
            {
                RateLimitWindowState? tightest = null;
                foreach (var window in Windows)
                {
                    if (tightest is null || window.Remaining < tightest.Remaining)
                    {
                        tightest = window;
                    }
                }

                return tightest;
            }
        }

        public static MultiWindowRateLimitResult Unlimited() => new() { IsAllowed = true };
    }

    /// <summary>
    /// Result of a single-window rate limit check.
    /// </summary>
    public class SlidingWindowResult
    {
        public bool IsAllowed { get; set; }
        public int Current { get; set; }
        public int Limit { get; set; }

        /// <summary>
        /// When the window frees room for this request — the oldest in-window entry's timestamp
        /// plus the window span, not a fixed calendar boundary.
        /// </summary>
        public DateTime ResetsAt { get; set; }
    }

    /// <summary>
    /// Atomic sliding-window rate limiter. Extracted as an interface so the services that
    /// compose several windows (RPM + RPD, key + group) can be unit tested without Redis.
    /// </summary>
    public interface ISlidingWindowRateLimiter
    {
        /// <inheritdoc cref="SlidingWindowRateLimiter.CheckAsync(IReadOnlyList{RateLimitWindow}, long, string?)"/>
        Task<MultiWindowRateLimitResult> CheckAsync(
            IReadOnlyList<RateLimitWindow> windows,
            long nowMs,
            string? entryId = null);

        /// <inheritdoc cref="SlidingWindowRateLimiter.CheckAsync(string, long, int, int)"/>
        Task<SlidingWindowResult> CheckAsync(string key, long nowMs, int windowMs, int limit);

        /// <inheritdoc cref="SlidingWindowRateLimiter.ReconcileAsync"/>
        Task<bool> ReconcileAsync(string key, string entryId, long oldWeight, long newWeight);

        /// <inheritdoc cref="SlidingWindowRateLimiter.ReleaseAsync"/>
        Task<bool> ReleaseAsync(string key, string entryId, long weight);
    }

    /// <summary>
    /// Reusable sliding-window rate limiter backed by Redis sorted sets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Storage per window: a sorted set scored by arrival time whose members are
    /// <c>{entryId}:{weight}</c>, plus a companion <c>{key}:sum</c> integer holding the total
    /// weight currently inside the window. Eviction subtracts the weight it removes, so reading
    /// the current total is O(1) and eviction is amortised O(1) — a weighted window (TPM) costs
    /// no more per request than a counting one.
    /// </para>
    /// <para>
    /// Checks are <b>all-or-nothing</b> across every window passed in: all are evaluated before
    /// any is written, so a request rejected by the daily window does not burn a minute slot, and
    /// one rejected by a group ceiling does not burn per-key quota. Evaluating several windows
    /// costs one Redis round-trip.
    /// </para>
    /// <para>
    /// Callers must supply a fresh <c>entryId</c> per request (the default is a new GUID).
    /// Reusing one against the same key would update the existing member's score instead of
    /// adding a second entry, while still incrementing the total.
    /// </para>
    /// </remarks>
    public class SlidingWindowRateLimiter : ISlidingWindowRateLimiter
    {
        // Atomic multi-window check-and-increment.
        //   KEYS[i]  = sorted set key for window i
        //   ARGV[1]  = now (unix ms)
        //   ARGV[2]  = entry id, unique per request
        //   ARGV[3]  = window count
        //   ARGV[4+] = per window: windowMs, limit, weight, unitWeight(0/1)
        // Returns a flat array: {allowed, deniedIndex, (current, limit, resetMs) * n}
        private const string MultiWindowScript = @"
            local now = tonumber(ARGV[1])
            local entryId = ARGV[2]
            local n = tonumber(ARGV[3])

            local windows, limits, weights, units = {}, {}, {}, {}
            local currents, resets = {}, {}
            local denied = 0

            for i = 1, n do
                local base = 3 + (i - 1) * 4
                windows[i] = tonumber(ARGV[base + 1])
                limits[i]  = tonumber(ARGV[base + 2])
                weights[i] = tonumber(ARGV[base + 3])
                units[i]   = tonumber(ARGV[base + 4])
            end

            -- Pass 1: evict, read totals, derive reset instants. No admission side effects yet.
            for i = 1, n do
                local key = KEYS[i]
                local sumKey = key .. ':sum'
                local cutoff = now - windows[i]

                local expired = redis.call('ZRANGEBYSCORE', key, '-inf', cutoff)
                if #expired > 0 then
                    local freed = 0
                    for j = 1, #expired do
                        freed = freed + (tonumber(string.match(expired[j], '([^:]+)$')) or 1)
                    end
                    redis.call('ZREMRANGEBYSCORE', key, '-inf', cutoff)
                    if freed > 0 then
                        redis.call('DECRBY', sumKey, freed)
                    end
                end

                local current = 0
                if redis.call('ZCARD', key) == 0 then
                    -- Window is empty: drop any drifted total so it cannot leak into the next window.
                    redis.call('DEL', sumKey)
                else
                    current = tonumber(redis.call('GET', sumKey) or '0')
                    if current < 0 then current = 0 end
                end
                currents[i] = current

                local oldest = redis.call('ZRANGE', key, 0, 0, 'WITHSCORES')
                if #oldest == 2 then
                    resets[i] = tonumber(oldest[2]) + windows[i]
                else
                    resets[i] = now + windows[i]
                end

                if denied == 0 and current + weights[i] > limits[i] then
                    denied = i
                end
            end

            -- Refine the denying window's reset to when enough weight has actually aged out.
            if denied > 0 then
                local key = KEYS[denied]
                local need = currents[denied] + weights[denied] - limits[denied]
                if need > 0 then
                    if units[denied] == 1 then
                        -- Every entry weighs 1, so the request fits once the entry at rank
                        -- need-1 leaves the window.
                        local at = redis.call('ZRANGE', key, need - 1, need - 1, 'WITHSCORES')
                        if #at == 2 then
                            resets[denied] = tonumber(at[2]) + windows[denied]
                        end
                    else
                        local entries = redis.call('ZRANGE', key, 0, -1, 'WITHSCORES')
                        local freed = 0
                        for j = 1, #entries, 2 do
                            freed = freed + (tonumber(string.match(entries[j], '([^:]+)$')) or 1)
                            if freed >= need then
                                resets[denied] = tonumber(entries[j + 1]) + windows[denied]
                                break
                            end
                        end
                    end
                end
            end

            -- Pass 2: admit into every window, or none of them.
            if denied == 0 then
                for i = 1, n do
                    local key = KEYS[i]
                    local sumKey = key .. ':sum'
                    local expiry = math.ceil(windows[i] / 1000) + 60
                    redis.call('ZADD', key, now, entryId .. ':' .. string.format('%d', weights[i]))
                    redis.call('INCRBY', sumKey, weights[i])
                    redis.call('EXPIRE', key, expiry)
                    redis.call('EXPIRE', sumKey, expiry)
                    currents[i] = currents[i] + weights[i]
                end
            end

            local out = {}
            out[1] = (denied == 0) and 1 or 0
            out[2] = denied
            for i = 1, n do
                out[#out + 1] = currents[i]
                out[#out + 1] = limits[i]
                out[#out + 1] = math.floor(resets[i])
            end
            return out
        ";

        // Adjusts an admitted entry's weight in place, preserving its position in the window.
        // Returns 1 when the entry was still in the window, 0 when it had already aged out.
        private const string ReconcileScript = @"
            local key = KEYS[1]
            local sumKey = key .. ':sum'
            local oldMember = ARGV[1] .. ':' .. ARGV[2]
            local newMember = ARGV[1] .. ':' .. ARGV[3]
            local delta = tonumber(ARGV[3]) - tonumber(ARGV[2])

            local score = redis.call('ZSCORE', key, oldMember)
            if not score then
                return 0
            end

            redis.call('ZREM', key, oldMember)
            redis.call('ZADD', key, score, newMember)
            redis.call('INCRBY', sumKey, delta)

            if tonumber(redis.call('GET', sumKey) or '0') < 0 then
                redis.call('SET', sumKey, 0)
            end
            return 1
        ";

        // Removes an admitted entry and gives its weight back. Idempotent: a second release
        // finds nothing to remove and leaves the total alone, so it cannot free someone else's slot.
        private const string ReleaseScript = @"
            local key = KEYS[1]
            local sumKey = key .. ':sum'
            local member = ARGV[1] .. ':' .. ARGV[2]

            if redis.call('ZREM', key, member) == 0 then
                return 0
            end

            redis.call('DECRBY', sumKey, tonumber(ARGV[2]))
            if redis.call('ZCARD', key) == 0 then
                redis.call('DEL', sumKey)
            elseif tonumber(redis.call('GET', sumKey) or '0') < 0 then
                redis.call('SET', sumKey, 0)
            end
            return 1
        ";

        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger _logger;

        public SlidingWindowRateLimiter(IConnectionMultiplexer redis, ILogger logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Atomically admits a request into every supplied window, or none of them.
        /// On Redis failure the request is allowed and <see cref="MultiWindowRateLimitResult.Degraded"/>
        /// is set so the caller can apply its configured failure policy.
        /// </summary>
        /// <param name="windows">Windows to evaluate. An empty list is an immediate allow.</param>
        /// <param name="nowMs">Current UTC time in milliseconds since the epoch.</param>
        /// <param name="entryId">
        /// Unique id for this request, used to reconcile or release the entry later.
        /// A new GUID is generated when omitted.
        /// </param>
        public async Task<MultiWindowRateLimitResult> CheckAsync(
            IReadOnlyList<RateLimitWindow> windows,
            long nowMs,
            string? entryId = null)
        {
            if (windows is null || windows.Count == 0)
            {
                return MultiWindowRateLimitResult.Unlimited();
            }

            entryId ??= Guid.NewGuid().ToString("N");

            try
            {
                var keys = new RedisKey[windows.Count];
                var args = new RedisValue[3 + (windows.Count * 4)];
                args[0] = nowMs;
                args[1] = entryId;
                args[2] = windows.Count;

                for (var i = 0; i < windows.Count; i++)
                {
                    keys[i] = windows[i].Key;
                    var offset = 3 + (i * 4);
                    args[offset] = windows[i].WindowMs;
                    args[offset + 1] = windows[i].Limit;
                    args[offset + 2] = windows[i].Weight;
                    args[offset + 3] = windows[i].UnitWeight ? 1 : 0;
                }

                var raw = await _redis.GetDatabase().ScriptEvaluateAsync(MultiWindowScript, keys, args);
                var array = (RedisValue[])raw!;

                var allowed = (int)array[0] == 1;
                var deniedIndex = (int)array[1];

                var states = new RateLimitWindowState[windows.Count];
                for (var i = 0; i < windows.Count; i++)
                {
                    var offset = 2 + (i * 3);
                    states[i] = new RateLimitWindowState
                    {
                        Scope = windows[i].Scope,
                        Current = (long)array[offset],
                        Limit = (long)array[offset + 1],
                        ResetsAt = DateTimeOffset.FromUnixTimeMilliseconds((long)array[offset + 2]).UtcDateTime
                    };
                }

                return new MultiWindowRateLimitResult
                {
                    IsAllowed = allowed,
                    EntryId = allowed ? entryId : null,
                    Windows = states,
                    DeniedWindow = deniedIndex > 0 ? states[deniedIndex - 1] : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing sliding window rate limit script for {WindowCount} window(s), first key {Key}",
                    windows.Count, windows[0].Key);

                // Report the failure but still admit — the caller owns the open/closed decision.
                return new MultiWindowRateLimitResult
                {
                    IsAllowed = true,
                    Degraded = true,
                    Windows = BuildDegradedStates(windows, nowMs)
                };
            }
        }

        /// <summary>
        /// Single-window convenience overload.
        /// </summary>
        /// <param name="key">The Redis sorted set key (caller provides the full key including prefix).</param>
        /// <param name="nowMs">Current UTC time in milliseconds since epoch.</param>
        /// <param name="windowMs">Window size in milliseconds (e.g. 60000 for RPM, 86400000 for RPD).</param>
        /// <param name="limit">Maximum number of requests allowed in the window.</param>
        public async Task<SlidingWindowResult> CheckAsync(string key, long nowMs, int windowMs, int limit)
        {
            var result = await CheckAsync(
                new[] { new RateLimitWindow(key, "window", windowMs, limit) },
                nowMs);

            var state = result.Windows.Count > 0 ? result.Windows[0] : null;
            return new SlidingWindowResult
            {
                IsAllowed = result.IsAllowed,
                Current = state is null ? 0 : (int)Math.Min(int.MaxValue, state.Current),
                Limit = limit,
                ResetsAt = state?.ResetsAt ?? DateTimeOffset.FromUnixTimeMilliseconds(nowMs + windowMs).UtcDateTime
            };
        }

        /// <summary>
        /// Adjusts an already-admitted entry's weight — the reconcile half of an
        /// estimate-then-reconcile reservation.
        /// </summary>
        /// <returns>
        /// False when the entry has already aged out of the window, in which case there is nothing
        /// to correct and the total is left untouched rather than driven negative.
        /// </returns>
        public async Task<bool> ReconcileAsync(string key, string entryId, long oldWeight, long newWeight)
        {
            if (string.IsNullOrEmpty(entryId) || oldWeight == newWeight)
            {
                return false;
            }

            try
            {
                var result = await _redis.GetDatabase().ScriptEvaluateAsync(
                    ReconcileScript,
                    new RedisKey[] { key },
                    new RedisValue[]
                    {
                        entryId,
                        oldWeight.ToString(CultureInfo.InvariantCulture),
                        newWeight.ToString(CultureInfo.InvariantCulture)
                    });

                return (int)result! == 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reconcile rate limit entry {EntryId} on {Key}; the reservation stands until the window slides", entryId, key);
                return false;
            }
        }

        /// <summary>
        /// Removes an admitted entry and returns its weight to the window.
        /// </summary>
        /// <returns>True when this call removed the entry; false when it was already gone.</returns>
        public async Task<bool> ReleaseAsync(string key, string entryId, long weight)
        {
            if (string.IsNullOrEmpty(entryId))
            {
                return false;
            }

            try
            {
                var result = await _redis.GetDatabase().ScriptEvaluateAsync(
                    ReleaseScript,
                    new RedisKey[] { key },
                    new RedisValue[] { entryId, weight.ToString(CultureInfo.InvariantCulture) });

                return (int)result! == 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release rate limit entry {EntryId} on {Key}; it will expire with the window", entryId, key);
                return false;
            }
        }

        private static RateLimitWindowState[] BuildDegradedStates(IReadOnlyList<RateLimitWindow> windows, long nowMs)
        {
            var states = new RateLimitWindowState[windows.Count];
            for (var i = 0; i < windows.Count; i++)
            {
                states[i] = new RateLimitWindowState
                {
                    Scope = windows[i].Scope,
                    Current = 0,
                    Limit = windows[i].Limit,
                    ResetsAt = DateTimeOffset.FromUnixTimeMilliseconds(nowMs + windows[i].WindowMs).UtcDateTime
                };
            }

            return states;
        }
    }
}
