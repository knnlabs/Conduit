using System.Diagnostics.Metrics;

using Prometheus;

namespace ConduitLLM.Gateway.Metrics;

/// <summary>
/// Configures how prometheus-net exports <see cref="System.Diagnostics.Metrics"/> histograms.
/// </summary>
/// <remarks>
/// OpenTelemetry views do not apply to prometheus-net's meter adapter, so any histogram that
/// needs non-default buckets must be configured here before its meter is initialized.
/// </remarks>
internal static class PrometheusMeterAdapterConfiguration
{
    // Stream startup includes network and provider latency. These buckets retain useful
    // resolution from a fast 5 ms response through a long-tail 15 second response.
    private static readonly double[] StreamStartupLatencyBucketsSeconds =
    [
        Milliseconds(5), Milliseconds(10), Milliseconds(25), Milliseconds(50),
        Milliseconds(100), Milliseconds(250), Milliseconds(500), Milliseconds(750),
        Seconds(1), Seconds(2.5), Seconds(5), Seconds(7.5), Seconds(10), Seconds(15)
    ];

    // Accounting finalization is local post-stream work and should normally be much faster,
    // so its buckets provide additional resolution in the 1-5 ms range.
    private static readonly double[] AccountingFinalizationBucketsSeconds =
    [
        Milliseconds(1), Milliseconds(2), Milliseconds(5), Milliseconds(10),
        Milliseconds(25), Milliseconds(50), Milliseconds(100), Milliseconds(250),
        Milliseconds(500), Seconds(1), Seconds(2.5), Seconds(5), Seconds(10)
    ];

    internal static void Configure()
    {
        var defaultBucketResolver = MeterAdapterOptions.Default.ResolveHistogramBuckets;

        Prometheus.Metrics.ConfigureMeterAdapter(options =>
        {
            options.ResolveHistogramBuckets = instrument =>
                ResolveHistogramBuckets(instrument, defaultBucketResolver);
        });
    }

    internal static double[] ResolveHistogramBuckets(
        Instrument instrument,
        Func<Instrument, double[]> fallback)
    {
        return instrument.Name switch
        {
            SseTransportMetrics.TimeToProviderFirstChunkInstrumentName or
            SseTransportMetrics.TimeToClientFirstFlushInstrumentName => StreamStartupLatencyBucketsSeconds,
            SseTransportMetrics.AccountingFinalizationInstrumentName => AccountingFinalizationBucketsSeconds,
            _ => fallback(instrument)
        };
    }

    private static double Milliseconds(double value) => TimeSpan.FromMilliseconds(value).TotalSeconds;

    private static double Seconds(double value) => TimeSpan.FromSeconds(value).TotalSeconds;
}
