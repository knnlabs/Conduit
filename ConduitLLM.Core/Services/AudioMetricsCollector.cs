using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Collects and aggregates audio operation metrics.
    /// </summary>
    public class AudioMetricsCollector : IAudioMetricsCollector
    {
        private readonly ILogger<AudioMetricsCollector> _logger;
        private readonly AudioMetricsOptions _options;
        private readonly ConcurrentDictionary<string, MetricsBucket> _metricsBuckets = new();
        private readonly ReaderWriterLockSlim _aggregationLock = new();
        private readonly Timer _aggregationTimer;
        private readonly IAudioAlertingService? _alertingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMetricsCollector"/> class.
        /// </summary>
        public AudioMetricsCollector(
            ILogger<AudioMetricsCollector> logger,
            IOptions<AudioMetricsOptions> options,
            IAudioAlertingService? alertingService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _alertingService = alertingService;

            // Start aggregation timer
            _aggregationTimer = new Timer(
                AggregateMetrics,
                null,
                _options.AggregationInterval,
                _options.AggregationInterval);
        }

        /// <inheritdoc />
        public Task RecordTranscriptionMetricAsync(TranscriptionMetric metric)
        {
            try
            {
                var bucket = GetOrCreateBucket(DateTime.UtcNow);

                bucket.TranscriptionMetrics.Add(metric);
                bucket.UpdateOperation(AudioOperation.Transcription, metric.Success, metric.DurationMs);

                if (metric.ServedFromCache)
                {
                    Interlocked.Increment(ref bucket.CacheHits);
                }

                _logger.LogDebug("Recorded transcription metric: Provider={Provider}, Duration={Duration}ms, Success={Success}",
                metric.Provider.Replace(Environment.NewLine, ""),
                metric.DurationMs,
                metric.Success);

                // Check for anomalies
                if (metric.DurationMs > _options.TranscriptionLatencyThreshold)
                {
                    _logger.LogWarning(
                        "High transcription latency detected: {Duration}ms (threshold: {Threshold}ms)",
                        metric.DurationMs, _options.TranscriptionLatencyThreshold);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error recording transcription metric");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task RecordTtsMetricAsync(TtsMetric metric)
        {
            try
            {
                var bucket = GetOrCreateBucket(DateTime.UtcNow);

                bucket.TtsMetrics.Add(metric);
                bucket.UpdateOperation(AudioOperation.TextToSpeech, metric.Success, metric.DurationMs);

                if (metric.ServedFromCache)
                {
                    Interlocked.Increment(ref bucket.CacheHits);
                }

                if (metric.UploadedToCdn)
                {
                    Interlocked.Increment(ref bucket.CdnUploads);
                }

                _logger.LogDebug("Recorded TTS metric: Provider={Provider}, Voice={Voice}, Duration={Duration}ms",
                metric.Provider.Replace(Environment.NewLine, ""),
                metric.Voice.Replace(Environment.NewLine, ""),
                metric.DurationMs);

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error recording TTS metric");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task RecordRealtimeMetricAsync(RealtimeMetric metric)
        {
            try
            {
                var bucket = GetOrCreateBucket(DateTime.UtcNow);

                bucket.RealtimeMetrics.Add(metric);
                bucket.UpdateOperation(AudioOperation.Realtime, metric.Success, metric.DurationMs);

                // Track session statistics
                Interlocked.Add(ref bucket.TotalRealtimeSeconds, (long)metric.SessionDurationSeconds);
                Interlocked.Add(ref bucket.TotalRealtimeTurns, metric.TurnCount);

                _logger.LogDebug("Recorded realtime metric: Provider={Provider}, Session={SessionId}, Duration={Duration}s",
                metric.Provider.Replace(Environment.NewLine, ""),
                metric.SessionId,
                metric.SessionDurationSeconds);

                // Check for high latency
                if (metric.AverageLatencyMs > _options.RealtimeLatencyThreshold)
                {
                    _logger.LogWarning(
                        "High realtime latency detected: {Latency}ms (threshold: {Threshold}ms)",
                        metric.AverageLatencyMs, _options.RealtimeLatencyThreshold);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error recording realtime metric");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task RecordRoutingMetricAsync(RoutingMetric metric)
        {
            try
            {
                var bucket = GetOrCreateBucket(DateTime.UtcNow);

                bucket.RoutingMetrics.Add(metric);

                // Track routing decisions
                bucket.TrackRoutingDecision(metric.SelectedProvider, metric.RoutingStrategy);

                _logger.LogDebug("Recorded routing metric: Operation={Operation}, Provider={Provider}, Strategy={Strategy}",
                metric.Operation.ToString(),
                metric.SelectedProvider.Replace(Environment.NewLine, ""),
                metric.RoutingStrategy.Replace(Environment.NewLine, ""));

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error recording routing metric");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task RecordProviderHealthMetricAsync(ProviderHealthMetric metric)
        {
            try
            {
                var bucket = GetOrCreateBucket(DateTime.UtcNow);

                bucket.ProviderHealthMetrics.Add(metric);

                // Update provider statistics
                bucket.UpdateProviderHealth(metric.Provider, metric.IsHealthy, metric.ErrorRate);

                _logger.LogDebug("Recorded provider health: Provider={Provider}, Healthy={Healthy}, ErrorRate={ErrorRate}%",
                metric.Provider.Replace(Environment.NewLine, ""),
                metric.IsHealthy,
                metric.ErrorRate * 100);

                // Alert on provider issues
                if (!metric.IsHealthy && _alertingService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await _alertingService.EvaluateMetricsAsync(
                            await GetCurrentSnapshotAsync());
                    });
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error recording provider health metric");
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task<AggregatedAudioMetrics> GetAggregatedMetricsAsync(
            DateTime startTime,
            DateTime endTime,
            string? provider = null)
        {
            _aggregationLock.EnterReadLock();
            try
            {
                var relevantBuckets = _metricsBuckets.Values
                    .Where(b => b.Timestamp >= startTime && b.Timestamp <= endTime)
                    .ToList();

                var aggregated = new AggregatedAudioMetrics
                {
                    Period = new DateTimeRange { Start = startTime, End = endTime }
                };

                // Aggregate transcription metrics
                var transcriptionMetrics = relevantBuckets
                    .SelectMany(b => b.TranscriptionMetrics)
                    .Where(m => provider == null || m.Provider == provider)
                    .ToList();

                aggregated.Transcription = AggregateOperationMetrics(transcriptionMetrics);

                // Aggregate TTS metrics
                var ttsMetrics = relevantBuckets
                    .SelectMany(b => b.TtsMetrics)
                    .Where(m => provider == null || m.Provider == provider)
                    .ToList();

                aggregated.TextToSpeech = AggregateOperationMetrics(ttsMetrics);

                // Aggregate realtime metrics
                var realtimeMetrics = relevantBuckets
                    .SelectMany(b => b.RealtimeMetrics)
                    .Where(m => provider == null || m.Provider == provider)
                    .ToList();

                aggregated.Realtime = AggregateRealtimeMetrics(realtimeMetrics);

                // Aggregate provider statistics
                aggregated.ProviderStats = AggregateProviderStats(relevantBuckets, provider);

                // Calculate costs
                aggregated.Costs = CalculateCosts(relevantBuckets, provider);

                return Task.FromResult(aggregated);
            }
            finally
            {
                _aggregationLock.ExitReadLock();
            }
        }

        /// <inheritdoc />
        public async Task<AudioMetricsSnapshot> GetCurrentSnapshotAsync()
        {
            var now = DateTime.UtcNow;
            var recentBuckets = _metricsBuckets.Values
                .Where(b => b.Timestamp >= now.AddMinutes(-5))
                .ToList();

            var snapshot = new AudioMetricsSnapshot
            {
                Timestamp = now,
                ActiveTranscriptions = CountActiveOperations(recentBuckets, AudioOperation.Transcription),
                ActiveTtsOperations = CountActiveOperations(recentBuckets, AudioOperation.TextToSpeech),
                ActiveRealtimeSessions = CountActiveOperations(recentBuckets, AudioOperation.Realtime),
                RequestsPerSecond = CalculateRequestRate(recentBuckets),
                CurrentErrorRate = CalculateErrorRate(recentBuckets),
                ProviderHealth = GetProviderHealthStatus(recentBuckets),
                Resources = await GetSystemResourcesAsync()
            };

            return snapshot;
        }

        private MetricsBucket GetOrCreateBucket(DateTime timestamp)
        {
            var bucketKey = GetBucketKey(timestamp);
            return _metricsBuckets.GetOrAdd(bucketKey, _ => new MetricsBucket { Timestamp = timestamp });
        }

        private string GetBucketKey(DateTime timestamp)
        {
            // Round to nearest minute
            var rounded = new DateTime(
                timestamp.Year,
                timestamp.Month,
                timestamp.Day,
                timestamp.Hour,
                timestamp.Minute,
                0,
                DateTimeKind.Utc);
            return rounded.ToString("yyyy-MM-dd-HH-mm");
        }

        private void AggregateMetrics(object? state)
        {
            try
            {
                _aggregationLock.EnterWriteLock();

                // Clean up old buckets
                var cutoff = DateTime.UtcNow.Subtract(_options.RetentionPeriod);
                var keysToRemove = _metricsBuckets
                    .Where(kvp => kvp.Value.Timestamp < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _metricsBuckets.TryRemove(key, out _);
                }

                _logger.LogDebug("Cleaned up {Count} old metric buckets",
                keysToRemove.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                "Error during metrics aggregation");
            }
            finally
            {
                _aggregationLock.ExitWriteLock();
            }
        }

        private OperationStatistics AggregateOperationMetrics<T>(List<T> metrics) where T : AudioMetricBase
        {
            if (!metrics.Any())
            {
                return new OperationStatistics();
            }

            var successful = metrics.Where(m => m.Success).ToList();
            var durations = metrics.Select(m => m.DurationMs).OrderBy(d => d).ToList();

            return new OperationStatistics
            {
                TotalRequests = metrics.Count,
                SuccessfulRequests = successful.Count,
                FailedRequests = metrics.Count - successful.Count,
                AverageDurationMs = durations.Average(),
                P95DurationMs = GetPercentile(durations, 0.95),
                P99DurationMs = GetPercentile(durations, 0.99),
                CacheHitRate = CalculateCacheHitRate(metrics),
                TotalDataBytes = CalculateTotalDataBytes(metrics)
            };
        }

        private RealtimeStatistics AggregateRealtimeMetrics(List<RealtimeMetric> metrics)
        {
            if (!metrics.Any())
            {
                return new RealtimeStatistics();
            }

            var disconnectReasons = metrics
                .Where(m => !string.IsNullOrEmpty(m.DisconnectReason))
                .GroupBy(m => m.DisconnectReason!)
                .ToDictionary(g => g.Key, g => (long)g.Count());

            return new RealtimeStatistics
            {
                TotalSessions = metrics.Count,
                AverageSessionDurationSeconds = metrics.Average(m => m.SessionDurationSeconds),
                TotalAudioMinutes = metrics.Sum(m => (m.TotalAudioSentSeconds + m.TotalAudioReceivedSeconds) / 60),
                AverageLatencyMs = metrics.Average(m => m.AverageLatencyMs),
                DisconnectReasons = disconnectReasons
            };
        }

        private Dictionary<string, ProviderStatistics> AggregateProviderStats(
            List<MetricsBucket> buckets,
            string? provider)
        {
            var allMetrics = buckets
                .SelectMany(b => b.TranscriptionMetrics.Cast<AudioMetricBase>()
                    .Concat(b.TtsMetrics)
                    .Concat(b.RealtimeMetrics))
                .Where(m => provider == null || m.Provider == provider)
                .GroupBy(m => m.Provider)
                .ToList();

            var result = new Dictionary<string, ProviderStatistics>();

            foreach (var group in allMetrics)
            {
                var providerMetrics = group.ToList();
                var successful = providerMetrics.Count(m => m.Success);
                var errorGroups = providerMetrics
                    .Where(m => !m.Success && !string.IsNullOrEmpty(m.ErrorCode))
                    .GroupBy(m => m.ErrorCode!)
                    .ToDictionary(g => g.Key, g => (long)g.Count());

                result[group.Key] = new ProviderStatistics
                {
                    Provider = group.Key,
                    RequestCount = providerMetrics.Count,
                    SuccessRate = providerMetrics.Count > 0 ? (double)successful / providerMetrics.Count : 0,
                    AverageLatencyMs = providerMetrics.Average(m => m.DurationMs),
                    UptimePercentage = CalculateUptime(buckets, group.Key),
                    ErrorBreakdown = errorGroups
                };
            }

            return result;
        }

        private CostAnalysis CalculateCosts(List<MetricsBucket> buckets, string? provider)
        {
            // This is a simplified cost calculation
            // In production, this would integrate with actual billing data
            var costs = new CostAnalysis();

            var transcriptionMinutes = buckets
                .SelectMany(b => b.TranscriptionMetrics)
                .Where(m => provider == null || m.Provider == provider)
                .Sum(m => m.AudioDurationSeconds / 60);

            var ttsCharacters = buckets
                .SelectMany(b => b.TtsMetrics)
                .Where(m => provider == null || m.Provider == provider)
                .Sum(m => m.CharacterCount);

            var realtimeMinutes = buckets
                .SelectMany(b => b.RealtimeMetrics)
                .Where(m => provider == null || m.Provider == provider)
                .Sum(m => m.SessionDurationSeconds / 60);

            // Example cost rates (would come from configuration)
            costs.TranscriptionCost = (decimal)(transcriptionMinutes * 0.006); // $0.006/minute
            costs.TextToSpeechCost = (decimal)(ttsCharacters * 0.000016); // $16/1M chars
            costs.RealtimeCost = (decimal)(realtimeMinutes * 0.06); // $0.06/minute
            costs.TotalCost = costs.TranscriptionCost + costs.TextToSpeechCost + costs.RealtimeCost;

            // Calculate cache savings
            var cacheHits = buckets.Sum(b => b.CacheHits);
            costs.CachingSavings = (decimal)(cacheHits * 0.001); // Estimated savings per cache hit

            return costs;
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;

            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        private double CalculateCacheHitRate<T>(List<T> metrics) where T : AudioMetricBase
        {
            if (metrics is List<TranscriptionMetric> transcriptions)
            {
                var cached = transcriptions.Count(m => m.ServedFromCache);
                return transcriptions.Count > 0 ? (double)cached / transcriptions.Count : 0;
            }

            if (metrics is List<TtsMetric> ttsMetrics)
            {
                var cached = ttsMetrics.Count(m => m.ServedFromCache);
                return ttsMetrics.Count > 0 ? (double)cached / ttsMetrics.Count : 0;
            }

            return 0;
        }

        private long CalculateTotalDataBytes<T>(List<T> metrics) where T : AudioMetricBase
        {
            if (metrics is List<TranscriptionMetric> transcriptions)
            {
                return transcriptions.Sum(m => m.FileSizeBytes);
            }

            if (metrics is List<TtsMetric> ttsMetrics)
            {
                return ttsMetrics.Sum(m => m.OutputSizeBytes);
            }

            return 0;
        }

        private int CountActiveOperations(List<MetricsBucket> buckets, AudioOperation operation)
        {
            return buckets.Sum(b => b.ActiveOperations.GetValueOrDefault(operation, 0));
        }

        private double CalculateRequestRate(List<MetricsBucket> buckets)
        {
            if (!buckets.Any()) return 0;

            var totalRequests = buckets.Sum(b => b.TotalRequests);
            var timeSpan = buckets.Max(b => b.Timestamp) - buckets.Min(b => b.Timestamp);

            return timeSpan.TotalSeconds > 0 ? totalRequests / timeSpan.TotalSeconds : 0;
        }

        private double CalculateErrorRate(List<MetricsBucket> buckets)
        {
            var total = buckets.Sum(b => b.TotalRequests);
            var errors = buckets.Sum(b => b.FailedRequests);

            return total > 0 ? (double)errors / total : 0;
        }

        private Dictionary<string, bool> GetProviderHealthStatus(List<MetricsBucket> buckets)
        {
            var latestHealth = buckets
                .SelectMany(b => b.ProviderHealthMetrics)
                .GroupBy(h => h.Provider)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(h => h.Timestamp).First().IsHealthy);

            return latestHealth;
        }

        private double CalculateUptime(List<MetricsBucket> buckets, string provider)
        {
            var healthMetrics = buckets
                .SelectMany(b => b.ProviderHealthMetrics)
                .Where(h => h.Provider == provider)
                .ToList();

            if (!healthMetrics.Any()) return 100;

            var healthyCount = healthMetrics.Count(h => h.IsHealthy);
            return (double)healthyCount / healthMetrics.Count * 100;
        }

        private async Task<SystemResources> GetSystemResourcesAsync()
        {
            // This would integrate with system monitoring
            return await Task.FromResult(new SystemResources
            {
                CpuUsagePercent = 35.2,
                MemoryUsageMb = 2048,
                ActiveConnections = 42,
                CacheSizeMb = 512
            });
        }

        /// <summary>
        /// Disposes the metrics collector.
        /// </summary>
        public void Dispose()
        {
            _aggregationTimer?.Dispose();
            _aggregationLock?.Dispose();
        }
    }

    /// <summary>
    /// Metrics bucket for time-based aggregation.
    /// </summary>
    internal class MetricsBucket
    {
        public DateTime Timestamp { get; set; }
        public ConcurrentBag<TranscriptionMetric> TranscriptionMetrics { get; } = new();
        public ConcurrentBag<TtsMetric> TtsMetrics { get; } = new();
        public ConcurrentBag<RealtimeMetric> RealtimeMetrics { get; } = new();
        public ConcurrentBag<RoutingMetric> RoutingMetrics { get; } = new();
        public ConcurrentBag<ProviderHealthMetric> ProviderHealthMetrics { get; } = new();

        public ConcurrentDictionary<AudioOperation, int> ActiveOperations { get; } = new();
        public ConcurrentDictionary<string, int> ProviderRequests { get; } = new();
        public ConcurrentDictionary<string, int> RoutingStrategies { get; } = new();

        public long TotalRequests;
        public long SuccessfulRequests;
        public long FailedRequests;
        public long CacheHits;
        public long CdnUploads;
        public long TotalRealtimeSeconds;
        public long TotalRealtimeTurns;

        public void UpdateOperation(AudioOperation operation, bool success, double durationMs)
        {
            Interlocked.Increment(ref TotalRequests);
            if (success)
            {
                Interlocked.Increment(ref SuccessfulRequests);
            }
            else
            {
                Interlocked.Increment(ref FailedRequests);
            }

            ActiveOperations.AddOrUpdate(operation, 1, (_, count) => count + 1);
        }

        public void TrackRoutingDecision(string provider, string strategy)
        {
            ProviderRequests.AddOrUpdate(provider, 1, (_, count) => count + 1);
            RoutingStrategies.AddOrUpdate(strategy, 1, (_, count) => count + 1);
        }

        public void UpdateProviderHealth(string provider, bool healthy, double errorRate)
        {
            // Provider health is tracked in the ProviderHealthMetrics collection
        }
    }

    /// <summary>
    /// Options for audio metrics collection.
    /// </summary>
    public class AudioMetricsOptions
    {
        /// <summary>
        /// Gets or sets the aggregation interval.
        /// </summary>
        public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the retention period for metrics.
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

        /// <summary>
        /// Gets or sets the transcription latency threshold.
        /// </summary>
        public double TranscriptionLatencyThreshold { get; set; } = 5000; // 5 seconds

        /// <summary>
        /// Gets or sets the realtime latency threshold.
        /// </summary>
        public double RealtimeLatencyThreshold { get; set; } = 200; // 200ms
    }
}
