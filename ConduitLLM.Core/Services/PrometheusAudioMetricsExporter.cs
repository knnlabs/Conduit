using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Exports audio metrics in Prometheus format for monitoring and alerting.
    /// </summary>
    public class PrometheusAudioMetricsExporter : IHostedService, IDisposable
    {
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly ILogger<PrometheusAudioMetricsExporter> _logger;
        private readonly PrometheusExporterOptions _options;
        private readonly StringBuilder _metricsBuffer;
        private Timer? _exportTimer;
        private readonly SemaphoreSlim _exportSemaphore;

        // Prometheus metric names
        private const string AUDIO_REQUESTS_TOTAL = "conduit_audio_requests_total";
        private const string AUDIO_REQUEST_DURATION_SECONDS = "conduit_audio_request_duration_seconds";
        private const string AUDIO_REQUEST_SIZE_BYTES = "conduit_audio_request_size_bytes";
        private const string AUDIO_CACHE_HIT_RATIO = "conduit_audio_cache_hit_ratio";
        private const string AUDIO_PROVIDER_ERROR_RATE = "conduit_audio_provider_error_rate";
        private const string AUDIO_PROVIDER_UPTIME_RATIO = "conduit_audio_provider_uptime_ratio";
        private const string AUDIO_ACTIVE_OPERATIONS = "conduit_audio_active_operations";
        private const string AUDIO_REALTIME_SESSIONS_TOTAL = "conduit_audio_realtime_sessions_total";
        private const string AUDIO_REALTIME_DURATION_SECONDS = "conduit_audio_realtime_duration_seconds";
        private const string AUDIO_REALTIME_LATENCY_SECONDS = "conduit_audio_realtime_latency_seconds";
        private const string AUDIO_COST_DOLLARS = "conduit_audio_cost_dollars";
        private const string AUDIO_CONFIDENCE_SCORE = "conduit_audio_confidence_score";
        private const string AUDIO_WORD_ERROR_RATE = "conduit_audio_word_error_rate";
        private const string AUDIO_QUALITY_SCORE = "conduit_audio_quality_score";

        private string? _cachedMetrics;
        private DateTime _lastExportTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusAudioMetricsExporter"/> class.
        /// </summary>
        public PrometheusAudioMetricsExporter(
            IAudioMetricsCollector metricsCollector,
            ILogger<PrometheusAudioMetricsExporter> logger,
            IOptions<PrometheusExporterOptions> options)
        {
            _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _metricsBuffer = new StringBuilder(4096);
            _exportSemaphore = new SemaphoreSlim(1, 1);
            _lastExportTime = DateTime.UtcNow;
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Prometheus audio metrics exporter");

            // Start periodic export
            _exportTimer = new Timer(
                ExportMetrics,
                null,
                TimeSpan.Zero,
                _options.ExportInterval);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Prometheus audio metrics exporter");

            _exportTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current metrics in Prometheus format.
        /// </summary>
        /// <returns>Metrics string in Prometheus exposition format</returns>
        public async Task<string> GetMetricsAsync()
        {
            await _exportSemaphore.WaitAsync();
            try
            {
                // Return cached metrics if still fresh
                if (_cachedMetrics != null && 
                    DateTime.UtcNow - _lastExportTime < _options.CacheExpiration)
                {
                    return _cachedMetrics;
                }

                // Generate fresh metrics
                _cachedMetrics = await GenerateMetricsAsync();
                _lastExportTime = DateTime.UtcNow;
                return _cachedMetrics;
            }
            finally
            {
                _exportSemaphore.Release();
            }
        }

        private void ExportMetrics(object? state)
        {
            _ = ExportMetricsAsync();
        }

        private async Task ExportMetricsAsync()
        {
            try
            {
                await _exportSemaphore.WaitAsync();
                try
                {
                    _cachedMetrics = await GenerateMetricsAsync();
                    _lastExportTime = DateTime.UtcNow;
                    
                    _logger.LogDebug("Exported {ByteCount} bytes of Prometheus metrics", 
                        _cachedMetrics?.Length ?? 0);
                }
                finally
                {
                    _exportSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Prometheus metrics");
            }
        }

        private async Task<string> GenerateMetricsAsync()
        {
            _metricsBuffer.Clear();

            var now = DateTime.UtcNow;
            var startTime = now.Subtract(_options.MetricsWindow);

            // Get aggregated metrics
            var metrics = await _metricsCollector.GetAggregatedMetricsAsync(startTime, now);
            var snapshot = await _metricsCollector.GetCurrentSnapshotAsync();

            // Write metric headers
            WriteMetricHelp();

            // Write transcription metrics
            WriteOperationMetrics(
                metrics.Transcription,
                "transcription",
                metrics.ProviderStats);

            // Write TTS metrics
            WriteOperationMetrics(
                metrics.TextToSpeech,
                "tts",
                metrics.ProviderStats);

            // Write realtime metrics
            WriteRealtimeMetrics(metrics.Realtime, metrics.ProviderStats);

            // Write provider health metrics
            WriteProviderHealthMetrics(metrics.ProviderStats);

            // Write active operations gauge
            WriteActiveOperations(snapshot);

            // Write cost metrics
            WriteCostMetrics(metrics.Costs);

            // Write cache metrics
            WriteCacheMetrics(metrics.Transcription, metrics.TextToSpeech);

            return _metricsBuffer.ToString();
        }

        private void WriteMetricHelp()
        {
            // Request count metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_REQUESTS_TOTAL} Total number of audio requests");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REQUESTS_TOTAL} counter");

            // Duration metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_REQUEST_DURATION_SECONDS} Audio request duration in seconds");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REQUEST_DURATION_SECONDS} histogram");

            // Size metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_REQUEST_SIZE_BYTES} Audio request size in bytes");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REQUEST_SIZE_BYTES} histogram");

            // Cache metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_CACHE_HIT_RATIO} Cache hit ratio for audio operations");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_CACHE_HIT_RATIO} gauge");

            // Provider metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_PROVIDER_ERROR_RATE} Provider error rate");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_PROVIDER_ERROR_RATE} gauge");

            _metricsBuffer.AppendLine($"# HELP {AUDIO_PROVIDER_UPTIME_RATIO} Provider uptime ratio");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_PROVIDER_UPTIME_RATIO} gauge");

            // Active operations
            _metricsBuffer.AppendLine($"# HELP {AUDIO_ACTIVE_OPERATIONS} Currently active audio operations");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_ACTIVE_OPERATIONS} gauge");

            // Realtime metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_REALTIME_SESSIONS_TOTAL} Total realtime sessions");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REALTIME_SESSIONS_TOTAL} counter");

            _metricsBuffer.AppendLine($"# HELP {AUDIO_REALTIME_DURATION_SECONDS} Realtime session duration in seconds");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REALTIME_DURATION_SECONDS} histogram");

            _metricsBuffer.AppendLine($"# HELP {AUDIO_REALTIME_LATENCY_SECONDS} Realtime latency in seconds");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_REALTIME_LATENCY_SECONDS} histogram");

            // Cost metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_COST_DOLLARS} Audio operation costs in dollars");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_COST_DOLLARS} counter");

            // Quality metrics
            _metricsBuffer.AppendLine($"# HELP {AUDIO_CONFIDENCE_SCORE} Audio transcription confidence score (0-1)");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_CONFIDENCE_SCORE} histogram");

            _metricsBuffer.AppendLine($"# HELP {AUDIO_WORD_ERROR_RATE} Audio transcription word error rate (0-1)");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_WORD_ERROR_RATE} histogram");

            _metricsBuffer.AppendLine($"# HELP {AUDIO_QUALITY_SCORE} Overall audio quality score (0-1)");
            _metricsBuffer.AppendLine($"# TYPE {AUDIO_QUALITY_SCORE} gauge");
        }

        private void WriteOperationMetrics(
            OperationStatistics stats,
            string operationType,
            Dictionary<string, ProviderStatistics> providerStats)
        {
            foreach (var provider in providerStats)
            {
                var labels = $"operation=\"{operationType}\",provider=\"{provider.Key}\"";

                // Request counts
                _metricsBuffer.AppendLine(
                    $"{AUDIO_REQUESTS_TOTAL}{{{labels},status=\"success\"}} {provider.Value.RequestCount * provider.Value.SuccessRate:F0}");
                _metricsBuffer.AppendLine(
                    $"{AUDIO_REQUESTS_TOTAL}{{{labels},status=\"failed\"}} {provider.Value.RequestCount * (1 - provider.Value.SuccessRate):F0}");

                // Duration histogram (simplified - in production, use proper histogram buckets)
                WriteDurationHistogram(
                    AUDIO_REQUEST_DURATION_SECONDS,
                    labels,
                    stats.AverageDurationMs / 1000.0,
                    stats.P95DurationMs / 1000.0,
                    stats.P99DurationMs / 1000.0,
                    stats.TotalRequests);

                // Size histogram
                if (stats.TotalDataBytes > 0)
                {
                    var avgSize = stats.TotalDataBytes / (double)stats.TotalRequests;
                    WriteSizeHistogram(
                        AUDIO_REQUEST_SIZE_BYTES,
                        labels,
                        avgSize,
                        stats.TotalRequests);
                }
            }
        }

        private void WriteRealtimeMetrics(
            RealtimeStatistics stats,
            Dictionary<string, ProviderStatistics> providerStats)
        {
            foreach (var provider in providerStats)
            {
                var labels = $"provider=\"{provider.Key}\"";

                // Session count
                _metricsBuffer.AppendLine(
                    $"{AUDIO_REALTIME_SESSIONS_TOTAL}{{{labels}}} {stats.TotalSessions}");

                // Session duration histogram
                WriteDurationHistogram(
                    AUDIO_REALTIME_DURATION_SECONDS,
                    labels,
                    stats.AverageSessionDurationSeconds,
                    stats.AverageSessionDurationSeconds * 1.5, // Estimate P95
                    stats.AverageSessionDurationSeconds * 2.0, // Estimate P99
                    stats.TotalSessions);

                // Latency histogram
                WriteDurationHistogram(
                    AUDIO_REALTIME_LATENCY_SECONDS,
                    labels,
                    stats.AverageLatencyMs / 1000.0,
                    stats.AverageLatencyMs * 1.5 / 1000.0, // Estimate P95
                    stats.AverageLatencyMs * 2.0 / 1000.0, // Estimate P99
                    stats.TotalSessions);

                // Disconnect reasons
                foreach (var reason in stats.DisconnectReasons)
                {
                    _metricsBuffer.AppendLine(
                        $"{AUDIO_REALTIME_SESSIONS_TOTAL}{{{labels},disconnect_reason=\"{reason.Key}\"}} {reason.Value}");
                }
            }
        }

        private void WriteProviderHealthMetrics(Dictionary<string, ProviderStatistics> providerStats)
        {
            foreach (var provider in providerStats)
            {
                var labels = $"provider=\"{provider.Key}\"";

                // Error rate
                _metricsBuffer.AppendLine(
                    $"{AUDIO_PROVIDER_ERROR_RATE}{{{labels}}} {1 - provider.Value.SuccessRate:F4}");

                // Uptime
                _metricsBuffer.AppendLine(
                    $"{AUDIO_PROVIDER_UPTIME_RATIO}{{{labels}}} {provider.Value.UptimePercentage / 100.0:F4}");

                // Error breakdown
                foreach (var error in provider.Value.ErrorBreakdown)
                {
                    var errorLabels = $"provider=\"{provider.Key}\",error_code=\"{error.Key}\"";
                    _metricsBuffer.AppendLine(
                        $"{AUDIO_REQUESTS_TOTAL}{{{errorLabels},status=\"error\"}} {error.Value}");
                }
            }
        }

        private void WriteActiveOperations(AudioMetricsSnapshot snapshot)
        {
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{operation=\"transcription\"}} {snapshot.ActiveTranscriptions}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{operation=\"tts\"}} {snapshot.ActiveTtsOperations}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{operation=\"realtime\"}} {snapshot.ActiveRealtimeSessions}");

            // System resources
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{resource=\"cpu_percent\"}} {snapshot.Resources.CpuUsagePercent:F2}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{resource=\"memory_mb\"}} {snapshot.Resources.MemoryUsageMb}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_ACTIVE_OPERATIONS}{{resource=\"connections\"}} {snapshot.Resources.ActiveConnections}");
        }

        private void WriteCostMetrics(CostAnalysis costs)
        {
            _metricsBuffer.AppendLine(
                $"{AUDIO_COST_DOLLARS}{{operation=\"transcription\"}} {costs.TranscriptionCost:F4}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_COST_DOLLARS}{{operation=\"tts\"}} {costs.TextToSpeechCost:F4}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_COST_DOLLARS}{{operation=\"realtime\"}} {costs.RealtimeCost:F4}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_COST_DOLLARS}{{operation=\"total\"}} {costs.TotalCost:F4}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_COST_DOLLARS}{{operation=\"cache_savings\"}} {costs.CachingSavings:F4}");
        }

        private void WriteCacheMetrics(OperationStatistics transcription, OperationStatistics tts)
        {
            _metricsBuffer.AppendLine(
                $"{AUDIO_CACHE_HIT_RATIO}{{operation=\"transcription\"}} {transcription.CacheHitRate:F4}");
            _metricsBuffer.AppendLine(
                $"{AUDIO_CACHE_HIT_RATIO}{{operation=\"tts\"}} {tts.CacheHitRate:F4}");
        }

        private void WriteDurationHistogram(
            string metricName,
            string labels,
            double avg,
            double p95,
            double p99,
            long count)
        {
            // Simplified histogram with common buckets
            var buckets = new[] { 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0, 60.0, double.PositiveInfinity };
            var sum = avg * count;

            foreach (var bucket in buckets)
            {
                var bucketCount = EstimateBucketCount(avg, p95, p99, count, bucket);
                var bucketLabel = double.IsPositiveInfinity(bucket) ? "Infinity" : bucket.ToString("F1");
                _metricsBuffer.AppendLine(
                    $"{metricName}_bucket{{{labels},le=\"{bucketLabel}\"}} {bucketCount}");
            }

            _metricsBuffer.AppendLine($"{metricName}_sum{{{labels}}} {sum:F2}");
            _metricsBuffer.AppendLine($"{metricName}_count{{{labels}}} {count}");
        }

        private void WriteSizeHistogram(
            string metricName,
            string labels,
            double avgSize,
            long count)
        {
            // Size buckets in bytes
            var buckets = new[] { 1024, 10240, 102400, 1048576, 10485760, double.PositiveInfinity };
            var sum = avgSize * count;

            foreach (var bucket in buckets)
            {
                var bucketCount = EstimateSizeBucketCount(avgSize, count, bucket);
                var bucketLabel = double.IsPositiveInfinity(bucket) ? "Infinity" : bucket.ToString("F0");
                _metricsBuffer.AppendLine(
                    $"{metricName}_bucket{{{labels},le=\"{bucketLabel}\"}} {bucketCount}");
            }

            _metricsBuffer.AppendLine($"{metricName}_sum{{{labels}}} {sum:F0}");
            _metricsBuffer.AppendLine($"{metricName}_count{{{labels}}} {count}");
        }

        private long EstimateBucketCount(double avg, double p95, double p99, long total, double bucketLimit)
        {
            // Simple estimation based on normal distribution
            if (bucketLimit >= p99) return total;
            if (bucketLimit >= p95) return (long)(total * 0.95);
            if (bucketLimit >= avg) return (long)(total * 0.5);
            return (long)(total * 0.1);
        }

        private long EstimateSizeBucketCount(double avgSize, long total, double bucketLimit)
        {
            // Simple estimation for size distribution
            if (bucketLimit >= avgSize * 10) return total;
            if (bucketLimit >= avgSize * 2) return (long)(total * 0.9);
            if (bucketLimit >= avgSize) return (long)(total * 0.5);
            return (long)(total * 0.1);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _exportTimer?.Dispose();
            _exportSemaphore?.Dispose();
        }
    }

    /// <summary>
    /// Options for Prometheus metrics exporter.
    /// </summary>
    public class PrometheusExporterOptions
    {
        /// <summary>
        /// Gets or sets the export interval.
        /// </summary>
        public TimeSpan ExportInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Gets or sets the metrics window for aggregation.
        /// </summary>
        public TimeSpan MetricsWindow { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the cache expiration for metrics.
        /// </summary>
        public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromSeconds(10);
    }
}