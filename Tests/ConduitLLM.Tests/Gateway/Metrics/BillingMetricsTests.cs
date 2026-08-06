using System.Text;
using ConduitLLM.Gateway.Metrics;
using Prometheus;

namespace ConduitLLM.Tests.Gateway.Metrics;

public sealed class BillingMetricsTests
{
    [Fact]
    public async Task SpendUpdateFailures_UsesOnlyBoundedErrorTypeLabel()
    {
        BillingMetrics.RecordSpendUpdateFailure("issue_1141_test");

        await using var stream = new MemoryStream();
        await Prometheus.Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, default);
        var exposition = Encoding.UTF8.GetString(stream.ToArray());
        var metric = exposition.Split('\n').Single(line =>
            line.StartsWith(
                "conduit_spend_update_failures_total{error_type=\"issue_1141_test\"}",
                StringComparison.Ordinal));

        Assert.DoesNotContain("virtual_key_id", metric);
        Assert.DoesNotContain("conduit_spend_update_attempts_total", exposition);
    }
}
