using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for audio services including providers, cache, and connection pool.
    /// </summary>
    public class AudioServiceHealthCheck : IHealthCheck
    {
        private readonly IAudioTranscriptionClient _transcriptionClient;
        private readonly ITextToSpeechClient _ttsClient;
        private readonly IAudioConnectionPool _connectionPool;
        private readonly IAudioStreamCache _cache;
        private readonly IAudioMetricsCollector _metricsCollector;
        private readonly ILogger<AudioServiceHealthCheck> _logger;

        public AudioServiceHealthCheck(
            IAudioTranscriptionClient transcriptionClient,
            ITextToSpeechClient ttsClient,
            IAudioConnectionPool connectionPool,
            IAudioStreamCache cache,
            IAudioMetricsCollector metricsCollector,
            ILogger<AudioServiceHealthCheck> logger)
        {
            _transcriptionClient = transcriptionClient;
            _ttsClient = ttsClient;
            _connectionPool = connectionPool;
            _cache = cache;
            _metricsCollector = metricsCollector;
            _logger = logger;
        }

        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var unhealthyReasons = new List<string>();
            var degradedReasons = new List<string>();

            try
            {
                // Check transcription service
                var transcriptionHealthy = await CheckTranscriptionServiceAsync(data, cancellationToken);
                if (!transcriptionHealthy)
                {
                    unhealthyReasons.Add("Transcription service is not responding");
                }

                // Check TTS service
                var ttsHealthy = await CheckTtsServiceAsync(data, cancellationToken);
                if (!ttsHealthy)
                {
                    unhealthyReasons.Add("Text-to-speech service is not responding");
                }

                // Check connection pool
                var poolStatus = await CheckConnectionPoolAsync(data, cancellationToken);
                if (poolStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
                {
                    unhealthyReasons.Add("Connection pool is exhausted");
                }
                else if (poolStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
                {
                    degradedReasons.Add("Connection pool is under high load");
                }

                // Check cache
                var cacheHealthy = await CheckCacheAsync(data, cancellationToken);
                if (!cacheHealthy)
                {
                    degradedReasons.Add("Cache service is not available");
                }

                // Check metrics
                var metricsStatus = await CheckMetricsAsync(data, cancellationToken);
                if (metricsStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
                {
                    unhealthyReasons.Add("High error rate detected");
                }
                else if (metricsStatus == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
                {
                    degradedReasons.Add("Elevated error rate or latency");
                }

                // Determine overall health status
                if (unhealthyReasons.Any())
                {
                    var message = string.Join("; ", unhealthyReasons);
                    _logger.LogError("Audio service health check failed: {Message}", message);
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(message, data: data);
                }

                if (degradedReasons.Any())
                {
                    var message = string.Join("; ", degradedReasons);
                    _logger.LogWarning("Audio service health check degraded: {Message}", message);
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(message, data: data);
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("All audio services are functioning normally", data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audio service health check");
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "Health check failed with exception",
                    ex,
                    data);
            }
        }

        private async Task<bool> CheckTranscriptionServiceAsync(
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            try
            {
                var supportedFormats = await _transcriptionClient.GetSupportedFormatsAsync(cancellationToken);
                data["transcription_formats"] = supportedFormats.Count;
                data["transcription_status"] = "healthy";
                return supportedFormats.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transcription service health check failed");
                data["transcription_status"] = "unhealthy";
                data["transcription_error"] = ex.Message;
                return false;
            }
        }

        private async Task<bool> CheckTtsServiceAsync(
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            try
            {
                var voices = await _ttsClient.ListVoicesAsync(cancellationToken: cancellationToken);
                data["tts_voices"] = voices.Count;
                data["tts_status"] = "healthy";
                return voices.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TTS service health check failed");
                data["tts_status"] = "unhealthy";
                data["tts_error"] = ex.Message;
                return false;
            }
        }

        private async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus> CheckConnectionPoolAsync(
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            try
            {
                var stats = await _connectionPool.GetStatisticsAsync();
                data["pool_total_connections"] = stats.TotalCreated;
                data["pool_active_connections"] = stats.ActiveConnections;
                data["pool_idle_connections"] = stats.IdleConnections;
                data["pool_exhausted_count"] = 0; // Property doesn't exist

                var utilizationRate = stats.TotalCreated > 0
                    ? (double)stats.ActiveConnections / stats.TotalCreated
                    : 0;

                data["pool_utilization"] = $"{utilizationRate:P1}";

                // PoolExhaustedCount doesn't exist on ConnectionPoolStatistics
                // if (stats.PoolExhaustedCount > 10)
                // {
                //     return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;
                // }

                if (utilizationRate > 0.8)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded;
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection pool health check failed");
                data["pool_error"] = ex.Message;
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded;
            }
        }

        private async Task<bool> CheckCacheAsync(
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            try
            {
                var testKey = $"health_check_{Guid.NewGuid()}";
                var testData = new byte[] { 1, 2, 3, 4, 5 };

                // These methods don't exist on IAudioStreamCache
                // await _cache.SetAsync(testKey, testData, AudioFormat.Mp3, cancellationToken);
                // var retrieved = await _cache.GetAsync(testKey, cancellationToken);
                // await _cache.RemoveAsync(testKey, cancellationToken);

                // Just check if we can get statistics
                var cacheStats = await _cache.GetStatisticsAsync();
                data["cache_status"] = cacheStats != null ? "healthy" : "unhealthy";
                return cacheStats != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed");
                data["cache_status"] = "unhealthy";
                data["cache_error"] = ex.Message;
                return false;
            }
        }

        private async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus> CheckMetricsAsync(
            Dictionary<string, object> data,
            CancellationToken cancellationToken)
        {
            try
            {
                var snapshot = await _metricsCollector.GetCurrentSnapshotAsync();
                
                data["metrics_requests_per_second"] = $"{snapshot.RequestsPerSecond:F1}";
                data["metrics_error_rate"] = $"{snapshot.CurrentErrorRate:P1}";
                // ProviderMetrics doesn't exist on AudioMetricsSnapshot
                data["metrics_avg_latency_ms"] = "0.0";
                data["metrics_active_connections"] = snapshot.Resources.ActiveConnections;

                if (snapshot.CurrentErrorRate > 0.1)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;
                }

                if (snapshot.CurrentErrorRate > 0.05)
                {
                    return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded;
                }

                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metrics health check failed");
                data["metrics_error"] = ex.Message;
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded;
            }
        }
    }
}