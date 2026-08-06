using ConduitLLM.Configuration.Options;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Gateway.RateLimiting;

public interface IRateLimitFailurePolicy
{
    /// <summary>
    /// Records that a limit could not be evaluated and returns whether the request should be
    /// rejected. False means enforcement is degraded and the request proceeds unchecked.
    /// </summary>
    bool ShouldReject(string scope, string reason);

    /// <summary>True while recent checks have been failing.</summary>
    bool IsDegraded { get; }
}

/// <summary>
/// Decides what happens when a rate limit cannot be evaluated — a Redis outage, a timeout, a
/// script failure.
/// </summary>
/// <remarks>
/// <para>
/// Failing open is the default and matches every other limiter here: rate limiting is
/// defence-in-depth, and a cache outage should not take the data plane with it. But that means
/// a Redis outage silently disables all rate limiting, which for a deployment where limits are
/// a hard commercial boundary is the wrong trade. Fail-closed makes that a deployment choice
/// rather than a property of the code.
/// </para>
/// <para>
/// Either way the degradation is now visible: a counter and a rate-limited warning fire
/// whenever the fail path is taken, so "the limiter is not running" is an observable state
/// rather than something you notice in the invoice.
/// </para>
/// <para>
/// Keys with no limits configured never reach here — they take no Redis round-trip in the
/// first place, so fail-closed cannot reject traffic that was never limited.
/// </para>
/// </remarks>
public sealed class RateLimitFailurePolicy : IRateLimitFailurePolicy
{
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitFailurePolicy> _logger;

    private long _lastLoggedTicks;
    private long _lastFailureTicks;

    public RateLimitFailurePolicy(RateLimitOptions options, ILogger<RateLimitFailurePolicy> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsDegraded
    {
        get
        {
            var last = Interlocked.Read(ref _lastFailureTicks);
            return last != 0 &&
                   DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc)
                       < TimeSpan.FromSeconds(_options.FailureDebounceSeconds);
        }
    }

    public bool ShouldReject(string scope, string reason)
    {
        var now = DateTime.UtcNow;
        Interlocked.Exchange(ref _lastFailureTicks, now.Ticks);
        GatewayRateLimitMetrics.RecordError();
        GatewayRateLimitMetrics.RecordDegraded(scope, _options.FailureMode.ToString().ToLowerInvariant());

        // One warning per debounce window rather than one per request: a flapping Redis would
        // otherwise bury the signal under thousands of identical lines.
        var lastLogged = Interlocked.Read(ref _lastLoggedTicks);
        if (lastLogged == 0 ||
            now - new DateTime(lastLogged, DateTimeKind.Utc) >= TimeSpan.FromSeconds(_options.FailureDebounceSeconds))
        {
            Interlocked.Exchange(ref _lastLoggedTicks, now.Ticks);
            _logger.LogWarning(
                "Rate limiting is degraded for {Scope}: {Reason}. Failure mode is {Mode}, so requests are being {Action}.",
                scope,
                reason,
                _options.FailureMode,
                _options.FailureMode == RateLimitFailureMode.Closed ? "rejected" : "admitted unchecked");
        }

        return _options.FailureMode == RateLimitFailureMode.Closed;
    }
}
