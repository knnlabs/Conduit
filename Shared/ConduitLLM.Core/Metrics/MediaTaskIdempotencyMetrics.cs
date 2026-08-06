using System.Diagnostics.Metrics;

namespace ConduitLLM.Core.Metrics;

/// <summary>Metrics for media outcomes that require replay-safety decisions.</summary>
public static class MediaTaskIdempotencyMetrics
{
    private static readonly Meter Meter = new("ConduitLLM.Media.Idempotency");
    private static readonly Counter<long> IndeterminateTasks =
        Meter.CreateCounter<long>("media_task_indeterminate");
    private static readonly Counter<long> OperatorRetries =
        Meter.CreateCounter<long>("media_task_operator_retries");

    public static void RecordIndeterminate(string source, long count = 1) =>
        IndeterminateTasks.Add(count, new KeyValuePair<string, object?>("source", source));

    public static void RecordOperatorRetry(string taskType) =>
        OperatorRetries.Add(1, new KeyValuePair<string, object?>("task_type", taskType));
}
