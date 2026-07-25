using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// A held concurrency slot. Releasing it returns capacity to the key immediately rather than
/// waiting for the slot's expiry.
/// </summary>
public sealed record ConcurrencySlot(string Key, string EntryId);

/// <summary>
/// Outcome of asking for a concurrency slot. <see cref="Slot"/> is set only when one was taken.
/// </summary>
public sealed record ConcurrencyDecision(bool IsAllowed, long Limit, long InFlight, ConcurrencySlot? Slot);

public interface IConcurrencyRateLimitService
{
    /// <summary>
    /// Takes a slot for the current request, or reports that the key is at its ceiling.
    /// Returns null when the key has no concurrency cap.
    /// </summary>
    Task<ConcurrencyDecision?> TryAcquireAsync(HttpContext context);

    /// <summary>
    /// Returns a slot. Safe to call twice: the second call finds nothing to release and cannot
    /// free a slot belonging to another request.
    /// </summary>
    Task ReleaseAsync(ConcurrencySlot slot);
}

/// <summary>
/// Caps how many requests a virtual key may have in flight at once.
/// </summary>
/// <remarks>
/// <para>
/// Slots are entries in a sliding window whose span is the slot lifetime, so the leak problem
/// solves itself: a node that dies mid-request never releases its slots, but they age out of
/// the window instead of being held forever. The window span therefore has to exceed the
/// longest legitimate request — streaming responses can run for minutes — which is why it is
/// configurable rather than tied to a request timeout.
/// </para>
/// <para>
/// Each slot carries a unique id, so a double release removes nothing rather than freeing
/// whichever slot happens to be there.
/// </para>
/// </remarks>
public sealed class ConcurrencyRateLimitService : IConcurrencyRateLimitService
{
    internal const string ScopeName = "concurrency";

    private readonly ISlidingWindowRateLimiter _limiter;
    private readonly RateLimitOptions _options;
    private readonly ILogger<ConcurrencyRateLimitService> _logger;

    public ConcurrencyRateLimitService(
        ISlidingWindowRateLimiter limiter,
        RateLimitOptions options,
        ILogger<ConcurrencyRateLimitService> logger)
    {
        _limiter = limiter;
        _options = options;
        _logger = logger;
    }

    public async Task<ConcurrencyDecision?> TryAcquireAsync(HttpContext context)
    {
        if (context.Items[RateLimitContextKeys.KeyHash] is not string keyHash || string.IsNullOrEmpty(keyHash))
        {
            return null;
        }

        if (context.Items[RateLimitContextKeys.MaxParallelRequests] is not int maxParallel || maxParallel <= 0)
        {
            return null;
        }

        var key = RedisKeys.RateLimit.VirtualKeyConcurrency(keyHash);
        var window = new RateLimitWindow(key, ScopeName, _options.ConcurrencySlotTtlSeconds * 1000, maxParallel);

        var result = await _limiter.CheckAsync(new[] { window }, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var state = result.Windows.Count > 0 ? result.Windows[0] : null;

        if (!result.IsAllowed)
        {
            _logger.LogWarning(
                "Virtual key {KeyHashPrefix} is at its concurrency ceiling: {InFlight}/{Limit} requests in flight",
                SafePrefix(keyHash), state?.Current ?? 0, maxParallel);
            GatewayRateLimitMetrics.RecordRejected(ScopeName);

            return new ConcurrencyDecision(false, maxParallel, state?.Current ?? maxParallel, null);
        }

        GatewayRateLimitMetrics.RecordAllowed(ScopeName);

        return new ConcurrencyDecision(
            true,
            maxParallel,
            state?.Current ?? 0,
            result.EntryId is null ? null : new ConcurrencySlot(key, result.EntryId));
    }

    public async Task ReleaseAsync(ConcurrencySlot slot)
    {
        // Weight is 1 for every slot, so a release always returns exactly one unit.
        await _limiter.ReleaseAsync(slot.Key, slot.EntryId, 1);
    }

    private static string SafePrefix(string keyHash) => keyHash.Length <= 8 ? keyHash : keyHash[..8];
}

/// <summary>
/// Stand-in for deployments with no Redis: concurrency is shared state across instances, so
/// without a shared store there is nothing to enforce.
/// </summary>
public sealed class UnlimitedConcurrencyRateLimitService : IConcurrencyRateLimitService
{
    public Task<ConcurrencyDecision?> TryAcquireAsync(HttpContext context) =>
        Task.FromResult<ConcurrencyDecision?>(null);

    public Task ReleaseAsync(ConcurrencySlot slot) => Task.CompletedTask;
}
