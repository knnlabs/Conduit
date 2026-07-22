using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConduitLLM.Gateway.Metrics;

/// <summary>
/// OpenTelemetry instruments for client-visible SSE transport and accounting.
/// </summary>
public static class SseTransportMetrics
{
    public const string MeterName = "ConduitLLM.Gateway.Streaming";

    internal const string TimeToProviderFirstChunkInstrumentName =
        "conduit.stream.time_to_provider_first_chunk";
    internal const string TimeToClientFirstFlushInstrumentName =
        "conduit.stream.time_to_client_first_flush";
    internal const string AccountingFinalizationInstrumentName =
        "conduit.stream.accounting_finalization";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly UpDownCounter<long> ActiveStreams = Meter.CreateUpDownCounter<long>(
        "conduit.streams.active",
        description: "Number of active client-visible streams");

    public static readonly Counter<long> Streams = Meter.CreateCounter<long>(
        "conduit.streams",
        description: "Total streams by terminal outcome");

    public static readonly Histogram<double> TimeToProviderFirstChunk = Meter.CreateHistogram<double>(
        TimeToProviderFirstChunkInstrumentName,
        unit: "s",
        description: "Time from stream admission to the first provider chunk");

    public static readonly Histogram<double> TimeToClientFirstFlush = Meter.CreateHistogram<double>(
        TimeToClientFirstFlushInstrumentName,
        unit: "s",
        description: "Time from stream admission to the first completed client flush");

    public static readonly Counter<long> Chunks = Meter.CreateCounter<long>(
        "conduit.stream.chunks",
        description: "Provider chunks observed by provider type");

    public static readonly Counter<long> Bytes = Meter.CreateCounter<long>(
        "conduit.stream.bytes",
        description: "SSE bytes flushed by provider type");

    public static readonly Counter<long> ClientDisconnects = Meter.CreateCounter<long>(
        "conduit.stream.client_disconnects",
        description: "Client disconnects by stream phase");

    private static readonly Histogram<double> AccountingFinalization = Meter.CreateHistogram<double>(
        AccountingFinalizationInstrumentName,
        unit: "s",
        description: "Time spent finalizing stream accounting");

    public static readonly Counter<long> UsageEvidence = Meter.CreateCounter<long>(
        "conduit.stream.usage_evidence",
        description: "Streaming usage evidence by source");

    public static IDisposable MeasureAccountingFinalization(string outcome) => new TimingScope(outcome);

    private sealed class TimingScope(string outcome) : IDisposable
    {
        private readonly long _startedAt = Stopwatch.GetTimestamp();
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            AccountingFinalization.Record(
                Stopwatch.GetElapsedTime(_startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }
}
