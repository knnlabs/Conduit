using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.RateLimiting;

/// <summary>
/// A token reservation held for the duration of one request.
/// </summary>
/// <param name="EntryId">Identifier of the window entries, for reconcile or release.</param>
/// <param name="ReservedTokens">Weight charged to each weighted window.</param>
/// <param name="WeightedKeys">
/// Every token window the entry was written to — key, group, and per-model. All of them must be
/// corrected together, or the ones left behind drift away from reality.
/// </param>
public sealed record TokenReservation(
    string EntryId,
    long ReservedTokens,
    IReadOnlyList<string> WeightedKeys)
{
    /// <summary>Weight the reservation has been corrected to, once usage is known.</summary>
    public long? SettledTokens { get; init; }
}

/// <summary>
/// Outcome of asking for room in the token and per-model windows that govern a request.
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
    /// Reserves capacity in every token and per-model window that governs this request, and
    /// records the reservation on the context so it can be reconciled later. Returns null when
    /// no such window applies, so the caller should proceed unimpeded.
    /// </summary>
    /// <param name="context">The request, carrying the key's and group's configured ceilings.</param>
    /// <param name="modelAlias">Alias the caller asked for, used to resolve per-model overrides.</param>
    /// <param name="estimatedTokens">Estimated prompt plus completion cost.</param>
    Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, string? modelAlias, long estimatedTokens);

    /// <summary>
    /// Corrects the request's reservation to the tokens it actually consumed. Safe to call more
    /// than once and safe to call when no reservation was made.
    /// </summary>
    Task ReconcileAsync(HttpContext context, long actualTokens);
}

/// <summary>
/// Estimate-then-reconcile enforcement of token ceilings, across the key, its group, and any
/// per-model override for the model the request names.
/// </summary>
/// <remarks>
/// <para>
/// Token cost is only known after a response exists, but admission has to be decided before the
/// provider is called — otherwise the limit polices nothing. So an estimate is charged to the
/// windows up front and corrected to the real figure once the response is billed.
/// </para>
/// <para>
/// Failure modes are bounded rather than eliminated. A request that dies before reconciliation
/// leaves its estimate standing, which over-charges until the entry ages out of the rolling
/// minute — never longer, because window entries carry a TTL. Reconciling an entry the window
/// has already passed is a no-op rather than a negative adjustment.
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

    public async Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, string? modelAlias, long estimatedTokens)
    {
        if (context.Items[RateLimitContextKeys.KeyHash] is not string keyHash || string.IsNullOrEmpty(keyHash))
        {
            return null;
        }

        var keyTpm = context.Items[RateLimitContextKeys.Tpm] as int?;
        var groupId = context.Items[RateLimitContextKeys.GroupId] as int?;
        var groupTpm = context.Items[RateLimitContextKeys.GroupTpm] as int?;
        var modelRule = ResolveModelRule(context, modelAlias);

        var hasKeyTpm = keyTpm is > 0;
        var hasGroupTpm = groupId is not null && groupTpm is > 0;
        var hasModelTpm = modelRule?.Tpm is > 0;
        var hasModelRpm = modelRule?.Rpm is > 0;

        if (!hasKeyTpm && !hasGroupTpm && !hasModelTpm && !hasModelRpm)
        {
            return null;
        }

        // A single request must never be structurally impossible to admit: clamp the
        // reservation to the tightest token ceiling so an oversized estimate produces one 429
        // rather than a permanent rejection that no amount of waiting resolves.
        var tightestTokenLimit = Min(
            hasKeyTpm ? keyTpm!.Value : (int?)null,
            hasGroupTpm ? groupTpm!.Value : null,
            hasModelTpm ? modelRule!.Tpm!.Value : null);
        var weight = tightestTokenLimit is null
            ? 1
            : Math.Max(1, Math.Min(estimatedTokens, tightestTokenLimit.Value));

        var windows = new List<RateLimitWindow>(4);
        var weightedKeys = new List<string>(3);

        if (hasKeyTpm)
        {
            var key = RedisKeys.RateLimit.VirtualKeyTpm(keyHash);
            windows.Add(new RateLimitWindow(key, ScopeName, MinuteWindowMs, keyTpm!.Value, weight, UnitWeight: false));
            weightedKeys.Add(key);
        }

        if (hasGroupTpm)
        {
            var key = RedisKeys.RateLimit.GroupTpm(groupId!.Value);
            windows.Add(new RateLimitWindow(key, GroupScopeName, MinuteWindowMs, groupTpm!.Value, weight, UnitWeight: false));
            weightedKeys.Add(key);
        }

        if (hasModelRpm)
        {
            // Request counting for this alias: weight 1, and never reconciled.
            windows.Add(new RateLimitWindow(
                RedisKeys.RateLimit.VirtualKeyModelRpm(keyHash, modelAlias!),
                $"model:{modelAlias}:rpm",
                MinuteWindowMs,
                modelRule!.Rpm!.Value));
        }

        if (hasModelTpm)
        {
            var key = RedisKeys.RateLimit.VirtualKeyModelTpm(keyHash, modelAlias!);
            windows.Add(new RateLimitWindow(
                key, $"model:{modelAlias}:tpm", MinuteWindowMs, modelRule!.Tpm!.Value, weight, UnitWeight: false));
            weightedKeys.Add(key);
        }

        var result = await _limiter.CheckAsync(windows, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        if (!result.IsAllowed)
        {
            var denied = result.DeniedWindow;
            _logger.LogWarning(
                "Virtual key {KeyHashPrefix} exceeded its {Scope} limit: {Current}/{Limit} in the last minute",
                SafePrefix(keyHash), denied?.Scope ?? ScopeName, denied?.Current ?? 0, denied?.Limit ?? 0);
            GatewayRateLimitMetrics.RecordRejected(denied?.Scope ?? ScopeName);

            return new TokenRateLimitDecision(
                IsAllowed: false,
                Scope: denied?.Scope ?? ScopeName,
                Limit: denied?.Limit ?? 0,
                ResetsAt: denied?.ResetsAt ?? DateTime.UtcNow.AddMinutes(1),
                Remaining: denied?.Remaining ?? 0);
        }

        // One entry id spans every window it was written to, so a single reconcile pass
        // corrects them all.
        if (result.EntryId is not null && weightedKeys.Count > 0)
        {
            context.Items[RateLimitContextKeys.TokenReservation] =
                new TokenReservation(result.EntryId, weight, weightedKeys);
        }

        var tightest = result.TightestWindow;
        GatewayRateLimitMetrics.RecordAllowed(tightest?.Scope ?? ScopeName);

        return new TokenRateLimitDecision(
            IsAllowed: true,
            Scope: tightest?.Scope ?? ScopeName,
            Limit: tightest?.Limit ?? 0,
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
        foreach (var key in reservation.WeightedKeys)
        {
            var adjusted = await _limiter.ReconcileAsync(
                key, reservation.EntryId, reservation.ReservedTokens, settled);

            if (!adjusted)
            {
                _logger.LogDebug(
                    "Token reservation {EntryId} was no longer in {Key} when usage settled at {ActualTokens} tokens",
                    reservation.EntryId, key, actualTokens);
            }
        }
    }

    /// <summary>
    /// Resolves the per-model override for the alias this request names, if the key has one.
    /// </summary>
    private static ModelRateLimitRule? ResolveModelRule(HttpContext context, string? modelAlias)
    {
        if (string.IsNullOrEmpty(modelAlias))
        {
            return null;
        }

        var rules = ModelRateLimitPolicy.Parse(context.Items[RateLimitContextKeys.ModelRateLimits] as string);
        return ModelRateLimitPolicy.Resolve(rules, modelAlias);
    }

    private static int? Min(params int?[] values)
    {
        int? smallest = null;
        foreach (var value in values)
        {
            if (value is not null && (smallest is null || value < smallest))
            {
                smallest = value;
            }
        }

        return smallest;
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
    public Task<TokenRateLimitDecision?> ReserveAsync(HttpContext context, string? modelAlias, long estimatedTokens) =>
        Task.FromResult<TokenRateLimitDecision?>(null);

    public Task ReconcileAsync(HttpContext context, long actualTokens) => Task.CompletedTask;
}
