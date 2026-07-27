using ConduitLLM.Gateway.Metrics;

using AwesomeAssertions;

using Prometheus;

namespace ConduitLLM.Tests.Gateway.Metrics;

/// <summary>
/// First coverage for the rate-limit counters. Operators page on these, so a mislabelled
/// or unincremented series is a silent monitoring outage.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "GatewayRateLimitMetrics")]
public class GatewayRateLimitMetricsTests
{
    private static readonly Counter Decisions = Prometheus.Metrics.CreateCounter(
        "conduit_gateway_rate_limit_decisions_total",
        "Number of rate-limit decisions by outcome",
        new CounterConfiguration { LabelNames = new[] { "outcome", "scope" } });

    private static readonly Counter Errors = Prometheus.Metrics.CreateCounter(
        "conduit_gateway_rate_limit_errors_total",
        "Number of rate-limit check errors (Redis unavailable, etc.) — these fail open");

    [Fact]
    public void RecordAllowed_IncrementsTheAllowedSeriesForItsScope()
    {
        var before = Decisions.WithLabels("allowed", "RPM").Value;

        GatewayRateLimitMetrics.RecordAllowed("RPM");

        Decisions.WithLabels("allowed", "RPM").Value.Should().Be(before + 1);
    }

    [Fact]
    public void RecordRejected_IncrementsTheRejectedSeriesForItsScope()
    {
        var before = Decisions.WithLabels("rejected", "RPD").Value;

        GatewayRateLimitMetrics.RecordRejected("RPD");

        Decisions.WithLabels("rejected", "RPD").Value.Should().Be(before + 1);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void RecordAllowed_WithoutAScope_LabelsTheSeriesNone(string? scope)
    {
        // An unlabelled series would be dropped by Prometheus; "none" keeps unlimited keys visible.
        var before = Decisions.WithLabels("allowed", "none").Value;

        GatewayRateLimitMetrics.RecordAllowed(scope!);

        Decisions.WithLabels("allowed", "none").Value.Should().Be(before + 1);
    }

    [Fact]
    public void RecordAllowed_AndRecordRejected_UseDistinctSeries()
    {
        var allowedBefore = Decisions.WithLabels("allowed", "TPM").Value;
        var rejectedBefore = Decisions.WithLabels("rejected", "TPM").Value;

        GatewayRateLimitMetrics.RecordRejected("TPM");

        Decisions.WithLabels("allowed", "TPM").Value.Should().Be(allowedBefore);
        Decisions.WithLabels("rejected", "TPM").Value.Should().Be(rejectedBefore + 1);
    }

    [Fact]
    public void RecordError_IncrementsTheFailOpenCounter()
    {
        var before = Errors.Value;

        GatewayRateLimitMetrics.RecordError();

        Errors.Value.Should().Be(before + 1);
    }
}
