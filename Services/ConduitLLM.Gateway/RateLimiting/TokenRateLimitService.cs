using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// A token reservation held for the duration of one request.
/// </summary>
/// <param name="Key">Redis key of the token window the reservation sits in.</param>
/// <param name="EntryId">Identifier of the window entry, for reconcile or release.</param>
/// <param name="ReservedTokens">Weight currently charged to the window.</param>
public sealed record TokenReservation(string Key, string EntryId, long ReservedTokens)
{
    /// <summary>
    /// Group token window the same entry was written to, when the key belongs to a group with
    /// its own token ceiling. Reconciliation has to correct both or the group total drifts.
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>Weight the reservation has been corrected to, once usage is known.</summary>
    public long? SettledTokens { get; init; }
}

/// <summary>
/// Outcome of asking for room in a key's token window.
/// </summary>
public sealed record TokenRateLimitDecision(
    bool IsAllowed,
    string Scope,
    long Limit,
    DateTime ResetsAt,
    long Remaining);

public interface ITokenRateLimitService
{
    /// <summary>
    /// Reserves <paramref name="estimatedTokens"/> against the request's token window and
    /// records the reservation on the context so it can be reconciled later. Returns null when
    /// the key has no token ceiling, so the caller should proceed unimpeded.
    /// </summary>
    Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, long estimatedTokens);

    /// <summary>
    /// Corrects the request's reservation to the tokens it actually consumed. Safe to call more
    /// than once and safe to call when no reservation was made.
    /// </summary>
    Task ReconcileAsync(HttpContext context, long actualTokens);
}

/// <summary>
/// Estimate-then-reconcile enforcement of per-key tokens-per-minute ceilings.
/// </summary>
/// <remarks>
/// <para>
/// Token cost is only known after a response exists, but admission has to be decided before the
/// provider is called — otherwise the limit polices nothing. So an estimate is charged to the
/// window up front and corrected to the real figure once the response is billed.
/// </para>
/// <para>
/// Failure modes are bounded rather than eliminated. A request that dies before reconciliation
/// leaves its estimate standing, which over-charges the key until the entry ages out of the
/// rolling minute — never longer, because window entries carry a TTL. Reconciling an entry the
/// window has already passed is a no-op rather than a negative adjustment.
/// </para>
/// </remarks>
public sealed class TokenRateLimitService : ITokenRateLimitService
{
    internal const int MinuteWindowMs = 60_000;
    internal const string ScopeName = "TPM";
    internal const string GroupScopeName = "group:TPM";

    private readonly ISlidingWindowRateLimiter _limiter;
    private readonly ILogger<TokenRateLimitService> _logger;

    public TokenRateLimitService(ISlidingWindowRateLimiter limiter, ILogger<TokenRateLimitService> logger)
    {
        _limiter = limiter;
        _logger = logger;
    }

    public async Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, long estimatedTokens)
    {
        if (context.Items[RateLimitContextKeys.KeyHash] is not string keyHash || string.IsNullOrEmpty(keyHash))
        {
            return null;
        }

        var keyTpm = context.Items[RateLimitContextKeys.Tpm] as int?;
        var groupId = context.Items[RateLimitContextKeys.GroupId] as int?;
        var groupTpm = context.Items[RateLimitContextKeys.GroupTpm] as int?;

        var hasKeyLimit = keyTpm is > 0;
        var hasGroupLimit = groupId is not null && groupTpm is > 0;
        if (!hasKeyLimit && !hasGroupLimit)
        {
            return null;
        }

        // A single request must never be structurally impossible to admit: clamp the
        // reservation to the tightest ceiling so an oversized estimate produces one 429 rather
        // than a permanent rejection that no amount of waiting resolves.
        var tightestLimit = Math.Min(
            hasKeyLimit ? keyTpm.Value : int.MaxValue,
            hasGroupLimit ? groupTpm.Value : int.MaxValue);
        var weight = Math.Max(1, Math.Min(estimatedTokens, tightestLimit));

        var key = RedisKeys.RateLimit.VirtualKeyTpm(keyHash);
        var windows = new List<RateLimitWindow>(2);

        if (hasKeyLimit)
        {
            windows.Add(new RateLimitWindow(key, ScopeName, MinuteWindowMs, keyTpm.Value, weight, UnitWeight: false));
        }

        string? groupKey = null;
        if (hasGroupLimit)
        {
            groupKey = RedisKeys.RateLimit.GroupTpm(groupId.Value);
            windows.Add(new RateLimitWindow(
                groupKey, GroupScopeName, MinuteWindowMs, groupTpm.Value, weight, UnitWeight: false));
        }

        var result = await _limiter.CheckAsync(windows, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (!result.IsAllowed)
        {
            var denied = result.DeniedWindow;
            _logger.LogWarning(
                "Virtual key {KeyHashPrefix} exceeded its {Scope} limit: {Current}/{Limit} tokens in the last minute",
                SafePrefix(keyHash), denied?.Scope ?? ScopeName, denied?.Current ?? 0, denied?.Limit ?? 0);
            GatewayRateLimitMetrics.RecordRejected(denied?.Scope ?? ScopeName);

            return new TokenRateLimitDecision(
                IsAllowed: false,
                Scope: denied?.Scope ?? ScopeName,
                Limit: denied?.Limit ?? tightestLimit,
                ResetsAt: denied?.ResetsAt ?? DateTime.UtcNow.AddMinutes(1),
                Remaining: denied?.Remaining ?? 0);
        }

        // One entry id covers every window it was written to, so a single reconcile corrects
        // the key and the group together.
        if (result.EntryId is not null)
        {
            var reservationKey = hasKeyLimit ? key : groupKey;
            context.Items[RateLimitContextKeys.TokenReservation] =
                new TokenReservation(reservationKey, result.EntryId, weight)
                {
                    GroupKey = hasKeyLimit ? groupKey : null
                };
        }

        var tightest = result.TightestWindow;
        GatewayRateLimitMetrics.RecordAllowed(tightest?.Scope ?? ScopeName);

        return new TokenRateLimitDecision(
            IsAllowed: true,
            Scope: tightest?.Scope ?? ScopeName,
            Limit: tightest?.Limit ?? tightestLimit,
            ResetsAt: tightest?.ResetsAt ?? DateTime.UtcNow.AddMinutes(1),
            Remaining: tightest?.Remaining ?? 0);
    }

    public async Task ReconcileAsync(HttpContext context, long actualTokens)
    {
        if (context.Items[RateLimitContextKeys.TokenReservation] is not TokenReservation reservation)
        {
            return;
        }

        if (reservation.SettledTokens is not null)
        {
            return;
        }

        // Mark settled before awaiting so a second caller on the same context cannot double-adjust.
        context.Items[RateLimitContextKeys.TokenReservation] = reservation with { SettledTokens = actualTokens };

        if (actualTokens == reservation.ReservedTokens)
        {
            return;
        }

        var settled = Math.Max(0, actualTokens);
        var adjusted = await _limiter.ReconcileAsync(
            reservation.Key,
            reservation.EntryId,
            reservation.ReservedTokens,
            settled);

        // The same entry sits in the group window too; correcting only one would let the
        // group total drift away from reality.
        if (reservation.GroupKey is not null)
        {
            await _limiter.ReconcileAsync(
                reservation.GroupKey,
                reservation.EntryId,
                reservation.ReservedTokens,
                settled);
        }

        if (!adjusted)
        {
            _logger.LogDebug(
                "Token reservation {EntryId} was no longer in the window when usage settled at {ActualTokens} tokens",
                reservation.EntryId, actualTokens);
        }
    }

    private static string SafePrefix(string keyHash) => keyHash.Length <= 8 ? keyHash : keyHash[..8];
}

/// <summary>
/// Stand-in used when no Redis is configured. Token limits are inherently distributed state,
/// so without a shared store there is nothing to enforce — this admits every request rather
/// than failing one, matching how the other limiters behave when their store is unreachable.
/// </summary>
public sealed class UnlimitedTokenRateLimitService : ITokenRateLimitService
{
    public Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, long estimatedTokens) =>
        Task.FromResult<TokenRateLimitDecision?>(null);

    public Task ReconcileAsync(HttpContext context, long actualTokens) => Task.CompletedTask;
}
