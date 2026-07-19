using Prometheus;

namespace ConduitLLM.Core.Metrics;

/// <summary>
/// Shared Prometheus metrics for authentication operations.
/// Instantiate with a service prefix (e.g., "gateway", "admin") to create
/// service-scoped counters and histograms.
/// </summary>
public class AuthMetrics
{
    /// <summary>
    /// Total authentication attempts by scheme and result.
    /// </summary>
    public Counter AuthAttempts { get; }

    /// <summary>
    /// Authentication duration by scheme.
    /// </summary>
    public Histogram AuthDuration { get; }

    /// <summary>
    /// Authentication failures by scheme and reason.
    /// </summary>
    public Counter AuthFailures { get; }

    /// <summary>
    /// Creates a new <see cref="AuthMetrics"/> instance with metrics prefixed by the given service name.
    /// </summary>
    /// <param name="servicePrefix">Service prefix for metric names (e.g., "gateway", "admin").</param>
    public AuthMetrics(string servicePrefix)
    {
        AuthAttempts = Prometheus.Metrics
            .CreateCounter($"conduit_{servicePrefix}_auth_attempts_total", "Total authentication attempts",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "result" }
                });

        AuthDuration = Prometheus.Metrics
            .CreateHistogram($"conduit_{servicePrefix}_auth_duration_seconds", "Authentication duration",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "scheme" },
                    Buckets = Histogram.ExponentialBuckets(0.0001, 2, 14) // 0.1ms to ~1.6s
                });

        AuthFailures = Prometheus.Metrics
            .CreateCounter($"conduit_{servicePrefix}_auth_failures_total", "Authentication failures by reason",
                new CounterConfiguration
                {
                    LabelNames = new[] { "scheme", "reason" }
                });
    }

    /// <summary>Records a successful authentication attempt.</summary>
    public void RecordSuccess(string scheme)
        => AuthAttempts.WithLabels(scheme, "success").Inc();

    /// <summary>Records a failed authentication attempt.</summary>
    public void RecordFailure(string scheme, string reason)
    {
        AuthAttempts.WithLabels(scheme, "failure").Inc();
        AuthFailures.WithLabels(scheme, reason).Inc();
    }

    /// <summary>Records an authentication attempt with no result (pass-through).</summary>
    public void RecordNoResult(string scheme)
        => AuthAttempts.WithLabels(scheme, "no_result").Inc();

    /// <summary>Records an authentication error.</summary>
    public void RecordError(string scheme)
    {
        AuthAttempts.WithLabels(scheme, "error").Inc();
        AuthFailures.WithLabels(scheme, "error").Inc();
    }

    /// <summary>Records the duration of an authentication operation.</summary>
    public void RecordDuration(string scheme, double durationSeconds)
        => AuthDuration.WithLabels(scheme).Observe(durationSeconds);
}
