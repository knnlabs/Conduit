using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Partial class containing metric recording functionality.
    /// </summary>
    public partial class AudioMetricsCollector
    {
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
    }
}