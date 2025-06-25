using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using EmbeddingAlertSeverity = ConduitLLM.Core.Models.AlertSeverity;

namespace ConduitLLM.Http.HealthChecks
{
    /// <summary>
    /// Health check for embedding services and models.
    /// </summary>
    /// <remarks>
    /// This health check verifies:
    /// - Embedding cache availability
    /// - Model health status
    /// - Recent error rates
    /// - System performance metrics
    /// </remarks>
    public class EmbeddingServiceHealthCheck : IHealthCheck
    {
        private readonly ILLMRouter _router;
        private readonly IEmbeddingCache? _embeddingCache;
        private readonly IEmbeddingMonitoringService? _monitoringService;
        private readonly ILogger<EmbeddingServiceHealthCheck> _logger;

        /// <summary>
        /// Initializes a new instance of the EmbeddingServiceHealthCheck.
        /// </summary>
        /// <param name="router">LLM router for checking model availability.</param>
        /// <param name="embeddingCache">Optional embedding cache service.</param>
        /// <param name="monitoringService">Optional monitoring service.</param>
        /// <param name="logger">Logger instance.</param>
        public EmbeddingServiceHealthCheck(
            ILLMRouter router,
            IEmbeddingCache? embeddingCache,
            IEmbeddingMonitoringService? monitoringService,
            ILogger<EmbeddingServiceHealthCheck> logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _embeddingCache = embeddingCache;
            _monitoringService = monitoringService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var healthData = new Dictionary<string, object>();
                var warnings = new List<string>();
                var isHealthy = true;

                // Check available embedding models
                var availableModels = _router.GetAvailableModels();
                healthData["AvailableModels"] = availableModels.Count;
                healthData["ModelNames"] = availableModels.Take(10).ToArray(); // Limit to avoid large responses

                if (availableModels.Count == 0)
                {
                    warnings.Add("No embedding models are available");
                    isHealthy = false;
                }

                // Check embedding cache health
                if (_embeddingCache != null)
                {
                    var cacheAvailable = _embeddingCache.IsAvailable;
                    healthData["CacheAvailable"] = cacheAvailable;

                    if (cacheAvailable)
                    {
                        try
                        {
                            var cacheStats = await _embeddingCache.GetStatsAsync();
                            healthData["CacheHitRate"] = Math.Round(cacheStats.HitRate * 100, 2);
                            healthData["CacheEntryCount"] = cacheStats.EntryCount;
                            healthData["CacheSizeMB"] = Math.Round(cacheStats.TotalSizeBytes / 1024.0 / 1024.0, 2);
                            
                            // Warn on low cache hit rate
                            if (cacheStats.HitRate < 0.3 && cacheStats.HitCount + cacheStats.MissCount > 100)
                            {
                                warnings.Add($"Low cache hit rate: {cacheStats.HitRate:P1}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to get cache statistics");
                            warnings.Add("Failed to retrieve cache statistics");
                        }
                    }
                    else
                    {
                        warnings.Add("Embedding cache is not available");
                    }
                }
                else
                {
                    healthData["CacheConfigured"] = false;
                    warnings.Add("Embedding cache is not configured");
                }

                // Check monitoring service and get recent metrics
                if (_monitoringService != null)
                {
                    try
                    {
                        var recentMetrics = await _monitoringService.GetMetricsAsync(
                            TimeSpan.FromMinutes(15), cancellationToken);
                        
                        healthData["RecentRequestCount"] = recentMetrics.TotalRequests;
                        healthData["RecentSuccessRate"] = Math.Round(recentMetrics.SuccessRate, 2);
                        healthData["RecentAverageLatencyMs"] = Math.Round(recentMetrics.AverageLatencyMs, 2);

                        // Check for concerning metrics
                        if (recentMetrics.SuccessRate < 95 && recentMetrics.TotalRequests > 10)
                        {
                            warnings.Add($"Low success rate: {recentMetrics.SuccessRate:F1}%");
                            isHealthy = false;
                        }

                        if (recentMetrics.AverageLatencyMs > 5000) // 5 seconds
                        {
                            warnings.Add($"High average latency: {recentMetrics.AverageLatencyMs:F0}ms");
                        }

                        // Check for active alerts
                        var activeAlerts = await _monitoringService.GetActiveAlertsAsync(cancellationToken);
                        healthData["ActiveAlerts"] = activeAlerts.Count;

                        var criticalAlerts = activeAlerts.Where(a => a.Severity == EmbeddingAlertSeverity.Critical).ToList();
                        if (criticalAlerts.Any())
                        {
                            isHealthy = false;
                            warnings.Add($"{criticalAlerts.Count} critical alerts active");
                        }

                        var errorAlerts = activeAlerts.Where(a => a.Severity == EmbeddingAlertSeverity.Error).ToList();
                        if (errorAlerts.Any())
                        {
                            warnings.Add($"{errorAlerts.Count} error alerts active");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get monitoring metrics");
                        warnings.Add("Failed to retrieve monitoring metrics");
                    }
                }
                else
                {
                    healthData["MonitoringConfigured"] = false;
                    warnings.Add("Embedding monitoring is not configured");
                }

                // Test basic embedding functionality
                try
                {
                    var testRequest = new EmbeddingRequest
                    {
                        Input = "health check test",
                        Model = availableModels.FirstOrDefault() ?? "text-embedding-3-small",
                        EncodingFormat = "float"
                    };

                    if (availableModels.Count > 0)
                    {
                        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var testResponse = await _router.CreateEmbeddingAsync(
                            testRequest, "simple", null, testCts.Token);
                        
                        healthData["TestEmbeddingSuccessful"] = true;
                        healthData["TestResponseLength"] = testResponse.Data?.Count ?? 0;
                    }
                    else
                    {
                        healthData["TestEmbeddingSuccessful"] = false;
                        warnings.Add("No models available for testing");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Embedding health check test failed");
                    healthData["TestEmbeddingSuccessful"] = false;
                    warnings.Add($"Embedding test failed: {ex.Message}");
                    isHealthy = false;
                }

                // Add warnings to health data
                if (warnings.Any())
                {
                    healthData["Warnings"] = warnings.ToArray();
                }

                // Determine overall health status
                var status = isHealthy ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy : 
                            warnings.Any() ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded : Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy;

                var description = isHealthy ? "Embedding services are healthy" :
                                 $"Embedding services have issues: {string.Join(", ", warnings)}";

                return new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult(status, description, data: healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding health check failed with exception");
                return new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult(
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                    "Embedding health check failed with exception",
                    ex,
                    new Dictionary<string, object> { ["Exception"] = ex.Message });
            }
        }
    }
}