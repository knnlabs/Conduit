namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for collecting audio operation metrics.
    /// </summary>
    public interface IAudioMetricsCollector
    {
        /// <summary>
        /// Records a transcription operation metric.
        /// </summary>
        /// <param name="metric">The transcription metric.</param>
        Task RecordTranscriptionMetricAsync(TranscriptionMetric metric);

        /// <summary>
        /// Records a text-to-speech operation metric.
        /// </summary>
        /// <param name="metric">The TTS metric.</param>
        Task RecordTtsMetricAsync(TtsMetric metric);

        /// <summary>
        /// Records a real-time session metric.
        /// </summary>
        /// <param name="metric">The real-time metric.</param>
        Task RecordRealtimeMetricAsync(RealtimeMetric metric);

        /// <summary>
        /// Records an audio routing decision.
        /// </summary>
        /// <param name="metric">The routing metric.</param>
        Task RecordRoutingMetricAsync(RoutingMetric metric);

        /// <summary>
        /// Records a provider health metric.
        /// </summary>
        /// <param name="metric">The health metric.</param>
        Task RecordProviderHealthMetricAsync(ProviderHealthMetric metric);

        /// <summary>
        /// Gets aggregated metrics for a time period.
        /// </summary>
        /// <param name="startTime">Start time for metrics.</param>
        /// <param name="endTime">End time for metrics.</param>
        /// <param name="provider">Optional provider filter.</param>
        /// <returns>Aggregated audio metrics.</returns>
        Task<AggregatedAudioMetrics> GetAggregatedMetricsAsync(
            DateTime startTime,
            DateTime endTime,
            string? provider = null);

        /// <summary>
        /// Gets real-time metrics snapshot.
        /// </summary>
        /// <returns>Current metrics snapshot.</returns>
        Task<AudioMetricsSnapshot> GetCurrentSnapshotAsync();
    }

    /// <summary>
    /// Base class for audio metrics.
    /// </summary>
    public abstract class AudioMetricBase
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the virtual key.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the error code if failed.
        /// </summary>
        public string? ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }

        /// <summary>
        /// Gets or sets custom tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Transcription operation metric.
    /// </summary>
    public class TranscriptionMetric : AudioMetricBase
    {
        /// <summary>
        /// Gets or sets the audio format.
        /// </summary>
        public string AudioFormat { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the audio duration in seconds.
        /// </summary>
        public double AudioDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the detected language.
        /// </summary>
        public string? DetectedLanguage { get; set; }

        /// <summary>
        /// Gets or sets the confidence score.
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Gets or sets the word count.
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Gets or sets whether it was served from cache.
        /// </summary>
        public bool ServedFromCache { get; set; }
    }

    /// <summary>
    /// Text-to-speech operation metric.
    /// </summary>
    public class TtsMetric : AudioMetricBase
    {
        /// <summary>
        /// Gets or sets the voice used.
        /// </summary>
        public string Voice { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the character count.
        /// </summary>
        public int CharacterCount { get; set; }

        /// <summary>
        /// Gets or sets the output format.
        /// </summary>
        public string OutputFormat { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the generated audio duration.
        /// </summary>
        public double GeneratedDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the output size in bytes.
        /// </summary>
        public long OutputSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets whether it was served from cache.
        /// </summary>
        public bool ServedFromCache { get; set; }

        /// <summary>
        /// Gets or sets whether it was uploaded to CDN.
        /// </summary>
        public bool UploadedToCdn { get; set; }
    }

    /// <summary>
    /// Real-time session metric.
    /// </summary>
    public class RealtimeMetric : AudioMetricBase
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the session duration.
        /// </summary>
        public double SessionDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the number of turns.
        /// </summary>
        public int TurnCount { get; set; }

        /// <summary>
        /// Gets or sets the total audio sent.
        /// </summary>
        public double TotalAudioSentSeconds { get; set; }

        /// <summary>
        /// Gets or sets the total audio received.
        /// </summary>
        public double TotalAudioReceivedSeconds { get; set; }

        /// <summary>
        /// Gets or sets the average latency.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the disconnect reason.
        /// </summary>
        public string? DisconnectReason { get; set; }
    }

    /// <summary>
    /// Routing decision metric.
    /// </summary>
    public class RoutingMetric : AudioMetricBase
    {
        /// <summary>
        /// Gets or sets the operation type.
        /// </summary>
        public AudioOperation Operation { get; set; }

        /// <summary>
        /// Gets or sets the routing strategy used.
        /// </summary>
        public string RoutingStrategy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the selected provider.
        /// </summary>
        public string SelectedProvider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the candidate providers considered.
        /// </summary>
        public List<string> CandidateProviders { get; set; } = new();

        /// <summary>
        /// Gets or sets the routing decision time.
        /// </summary>
        public double DecisionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the routing reason.
        /// </summary>
        public string? RoutingReason { get; set; }
    }

    /// <summary>
    /// Provider health metric.
    /// </summary>
    public class ProviderHealthMetric
    {
        /// <summary>
        /// Gets or sets the timestamp.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the provider is healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the response time.
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the error rate.
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// Gets or sets the success rate.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets health check details.
        /// </summary>
        public Dictionary<string, object> HealthDetails { get; set; } = new();
    }

    /// <summary>
    /// Aggregated audio metrics.
    /// </summary>
    public class AggregatedAudioMetrics
    {
        /// <summary>
        /// Gets or sets the time period.
        /// </summary>
        public DateTimeRange Period { get; set; } = new();

        /// <summary>
        /// Gets or sets transcription statistics.
        /// </summary>
        public OperationStatistics Transcription { get; set; } = new();

        /// <summary>
        /// Gets or sets TTS statistics.
        /// </summary>
        public OperationStatistics TextToSpeech { get; set; } = new();

        /// <summary>
        /// Gets or sets real-time statistics.
        /// </summary>
        public RealtimeStatistics Realtime { get; set; } = new();

        /// <summary>
        /// Gets or sets provider statistics.
        /// </summary>
        public Dictionary<string, ProviderStatistics> ProviderStats { get; set; } = new();

        /// <summary>
        /// Gets or sets cost analysis.
        /// </summary>
        public CostAnalysis Costs { get; set; } = new();
    }

    /// <summary>
    /// Operation statistics.
    /// </summary>
    public class OperationStatistics
    {
        /// <summary>
        /// Gets or sets total requests.
        /// </summary>
        public long TotalRequests { get; set; }

        /// <summary>
        /// Gets or sets successful requests.
        /// </summary>
        public long SuccessfulRequests { get; set; }

        /// <summary>
        /// Gets or sets failed requests.
        /// </summary>
        public long FailedRequests { get; set; }

        /// <summary>
        /// Gets or sets average duration.
        /// </summary>
        public double AverageDurationMs { get; set; }

        /// <summary>
        /// Gets or sets P95 duration.
        /// </summary>
        public double P95DurationMs { get; set; }

        /// <summary>
        /// Gets or sets P99 duration.
        /// </summary>
        public double P99DurationMs { get; set; }

        /// <summary>
        /// Gets or sets cache hit rate.
        /// </summary>
        public double CacheHitRate { get; set; }

        /// <summary>
        /// Gets or sets total data processed.
        /// </summary>
        public long TotalDataBytes { get; set; }
    }

    /// <summary>
    /// Real-time statistics.
    /// </summary>
    public class RealtimeStatistics
    {
        /// <summary>
        /// Gets or sets total sessions.
        /// </summary>
        public long TotalSessions { get; set; }

        /// <summary>
        /// Gets or sets average session duration.
        /// </summary>
        public double AverageSessionDurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets total audio minutes.
        /// </summary>
        public double TotalAudioMinutes { get; set; }

        /// <summary>
        /// Gets or sets average latency.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets disconnect reasons.
        /// </summary>
        public Dictionary<string, long> DisconnectReasons { get; set; } = new();
    }

    /// <summary>
    /// Provider statistics.
    /// </summary>
    public class ProviderStatistics
    {
        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets request count.
        /// </summary>
        public long RequestCount { get; set; }

        /// <summary>
        /// Gets or sets success rate.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets average latency.
        /// </summary>
        public double AverageLatencyMs { get; set; }

        /// <summary>
        /// Gets or sets uptime percentage.
        /// </summary>
        public double UptimePercentage { get; set; }

        /// <summary>
        /// Gets or sets error breakdown.
        /// </summary>
        public Dictionary<string, long> ErrorBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Cost analysis.
    /// </summary>
    public class CostAnalysis
    {
        /// <summary>
        /// Gets or sets total cost.
        /// </summary>
        public decimal TotalCost { get; set; }

        /// <summary>
        /// Gets or sets transcription cost.
        /// </summary>
        public decimal TranscriptionCost { get; set; }

        /// <summary>
        /// Gets or sets TTS cost.
        /// </summary>
        public decimal TextToSpeechCost { get; set; }

        /// <summary>
        /// Gets or sets real-time cost.
        /// </summary>
        public decimal RealtimeCost { get; set; }

        /// <summary>
        /// Gets or sets cost by provider.
        /// </summary>
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        /// <summary>
        /// Gets or sets cost savings from caching.
        /// </summary>
        public decimal CachingSavings { get; set; }
    }

    /// <summary>
    /// Current metrics snapshot.
    /// </summary>
    public class AudioMetricsSnapshot
    {
        /// <summary>
        /// Gets or sets the snapshot time.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets active transcriptions.
        /// </summary>
        public int ActiveTranscriptions { get; set; }

        /// <summary>
        /// Gets or sets active TTS operations.
        /// </summary>
        public int ActiveTtsOperations { get; set; }

        /// <summary>
        /// Gets or sets active real-time sessions.
        /// </summary>
        public int ActiveRealtimeSessions { get; set; }

        /// <summary>
        /// Gets or sets requests per second.
        /// </summary>
        public double RequestsPerSecond { get; set; }

        /// <summary>
        /// Gets or sets current error rate.
        /// </summary>
        public double CurrentErrorRate { get; set; }

        /// <summary>
        /// Gets or sets provider health status.
        /// </summary>
        public Dictionary<string, bool> ProviderHealth { get; set; } = new();

        /// <summary>
        /// Gets or sets system resources.
        /// </summary>
        public SystemResources Resources { get; set; } = new();
    }

    /// <summary>
    /// System resources.
    /// </summary>
    public class SystemResources
    {
        /// <summary>
        /// Gets or sets CPU usage percentage.
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// Gets or sets memory usage in MB.
        /// </summary>
        public double MemoryUsageMb { get; set; }

        /// <summary>
        /// Gets or sets active connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Gets or sets cache size in MB.
        /// </summary>
        public double CacheSizeMb { get; set; }
    }

    /// <summary>
    /// Date time range.
    /// </summary>
    public class DateTimeRange
    {
        /// <summary>
        /// Gets or sets the start time.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Gets or sets the end time.
        /// </summary>
        public DateTime End { get; set; }
    }
}
