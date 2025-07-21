using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Caching;

namespace ConduitLLM.Core.HealthChecks
{
    /// <summary>
    /// Comprehensive health check for cache infrastructure including monitoring integration
    /// </summary>
    public class CacheHealthCheck : IHealthCheck
    {
        private readonly ICacheManager _cacheManager;
        private readonly ICacheMonitoringService _monitoringService;
        private readonly ICacheMetricsService _metricsService;
        private readonly ICacheRegistry _cacheRegistry;
        private readonly ILogger<CacheHealthCheck> _logger;

        // Health check thresholds
        private const double DegradedHitRateThreshold = 0.7;
        private const double UnhealthyHitRateThreshold = 0.5;
        private const double DegradedMemoryThreshold = 0.8;
        private const double UnhealthyMemoryThreshold = 0.95;
        private const double DegradedResponseTimeMs = 50;
        private const double UnhealthyResponseTimeMs = 100;
        private const int MinRequestsForEvaluation = 10;

        public CacheHealthCheck(
            ICacheManager cacheManager,
            ICacheMonitoringService monitoringService,
            ICacheMetricsService metricsService,
            ICacheRegistry cacheRegistry,
            ILogger<CacheHealthCheck> logger)
        {
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
            _monitoringService = monitoringService ?? throw new ArgumentNullException(nameof(monitoringService));
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _cacheRegistry = cacheRegistry ?? throw new ArgumentNullException(nameof(cacheRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var issues = new List<string>();
            var isHealthy = true;
            var isDegraded = false;

            try
            {
                // Check cache manager health
                var healthStatus = await _cacheManager.GetHealthStatusAsync();
                data["cacheManagerHealthy"] = healthStatus.IsHealthy;
                data["memoryCacheResponseTime"] = healthStatus.MemoryCacheResponseTime?.TotalMilliseconds ?? 0;
                data["distributedCacheResponseTime"] = healthStatus.DistributedCacheResponseTime?.TotalMilliseconds ?? 0;
                data["components"] = healthStatus.ComponentStatus;

                if (!healthStatus.IsHealthy)
                {
                    isHealthy = false;
                    issues.AddRange(healthStatus.Issues.Select(i => $"Cache Manager: {i}"));
                }

                // Check monitoring service status
                var monitoringStatus = await _monitoringService.GetStatusAsync(cancellationToken);
                data["lastMonitoringCheck"] = monitoringStatus.LastCheck;
                data["activeAlerts"] = monitoringStatus.ActiveAlerts;
                data["monitoringHealthy"] = monitoringStatus.IsHealthy;

                if (!monitoringStatus.IsHealthy)
                {
                    isDegraded = true;
                    issues.Add($"Monitoring service unhealthy with {monitoringStatus.ActiveAlerts} active alerts");
                }

                // Check cache metrics
                var totalRequests = _metricsService.GetTotalRequests();
                var hitRate = _metricsService.GetHitRate();
                var avgResponseTime = _metricsService.GetAverageRetrievalTimeMs();

                data["totalRequests"] = totalRequests;
                data["hitRate"] = hitRate;
                data["avgResponseTimeMs"] = avgResponseTime;

                // Evaluate metrics if we have enough data
                if (totalRequests >= MinRequestsForEvaluation)
                {
                    // Check hit rate
                    if (hitRate < UnhealthyHitRateThreshold)
                    {
                        isHealthy = false;
                        issues.Add($"Cache hit rate critically low: {hitRate:P1}");
                    }
                    else if (hitRate < DegradedHitRateThreshold)
                    {
                        isDegraded = true;
                        issues.Add($"Cache hit rate below optimal: {hitRate:P1}");
                    }

                    // Check response time
                    if (avgResponseTime > UnhealthyResponseTimeMs)
                    {
                        isHealthy = false;
                        issues.Add($"Cache response time critically high: {avgResponseTime:F1}ms");
                    }
                    else if (avgResponseTime > DegradedResponseTimeMs)
                    {
                        isDegraded = true;
                        issues.Add($"Cache response time above optimal: {avgResponseTime:F1}ms");
                    }
                }

                // Check cache regions
                var regions = _cacheRegistry.GetAllRegions();
                var regionStats = new Dictionary<string, object>();
                double totalMemoryUsed = 0;
                double totalMemoryLimit = 0;
                int unhealthyRegions = 0;
                int degradedRegions = 0;

                foreach (var regionKvp in regions)
                {
                    var region = regionKvp.Key;
                    var regionConfig = regionKvp.Value;
                    
                    try
                    {
                        var stats = await _cacheManager.GetRegionStatisticsAsync(region, cancellationToken);
                        if (stats != null)
                        {
                            totalMemoryUsed += stats.TotalSizeBytes;
                            totalMemoryLimit += regionConfig.MaxSizeInBytes ?? 0;

                            var regionData = new Dictionary<string, object>
                            {
                                ["enabled"] = regionConfig.UseMemoryCache || regionConfig.UseDistributedCache,
                                ["entryCount"] = stats.EntryCount,
                                ["currentSizeBytes"] = stats.TotalSizeBytes,
                                ["maxSizeBytes"] = regionConfig.MaxSizeInBytes ?? 0,
                                ["hitCount"] = stats.HitCount,
                                ["missCount"] = stats.MissCount,
                                ["evictionCount"] = stats.EvictionCount
                            };

                            // Check region memory usage
                            if (regionConfig.MaxSizeInBytes > 0)
                            {
                                var memoryUsage = (double)stats.TotalSizeBytes / regionConfig.MaxSizeInBytes.Value;
                                regionData["memoryUsagePercent"] = memoryUsage;

                                if (memoryUsage > UnhealthyMemoryThreshold)
                                {
                                    unhealthyRegions++;
                                    issues.Add($"Region '{region}' memory critical: {memoryUsage:P1}");
                                }
                                else if (memoryUsage > DegradedMemoryThreshold)
                                {
                                    degradedRegions++;
                                    issues.Add($"Region '{region}' memory high: {memoryUsage:P1}");
                                }
                            }

                            // Check region hit rate
                            var regionHitRate = stats.HitCount + stats.MissCount > 0
                                ? (double)stats.HitCount / (stats.HitCount + stats.MissCount)
                                : 0;
                            regionData["hitRate"] = regionHitRate;

                            regionStats[region.ToString()] = regionData;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get statistics for cache region {RegionName}", region);
                        regionStats[region.ToString()] = new Dictionary<string, object>
                        {
                            ["error"] = ex.Message
                        };
                    }
                }

                data["regions"] = regionStats;
                data["regionCount"] = regions.Count();
                data["unhealthyRegions"] = unhealthyRegions;
                data["degradedRegions"] = degradedRegions;

                if (unhealthyRegions > 0)
                {
                    isHealthy = false;
                }
                if (degradedRegions > 0)
                {
                    isDegraded = true;
                }

                // Calculate overall memory usage
                if (totalMemoryLimit > 0)
                {
                    var overallMemoryUsage = totalMemoryUsed / totalMemoryLimit;
                    data["overallMemoryUsagePercent"] = overallMemoryUsage;

                    if (overallMemoryUsage > UnhealthyMemoryThreshold)
                    {
                        isHealthy = false;
                        issues.Add($"Overall cache memory critical: {overallMemoryUsage:P1}");
                    }
                    else if (overallMemoryUsage > DegradedMemoryThreshold)
                    {
                        isDegraded = true;
                        issues.Add($"Overall cache memory high: {overallMemoryUsage:P1}");
                    }
                }

                // Get recent alerts
                var recentAlerts = _monitoringService.GetRecentAlerts(5);
                if (recentAlerts.Count > 0)
                {
                    data["recentAlerts"] = recentAlerts.Select(a => new
                    {
                        a.AlertType,
                        a.Message,
                        a.Severity,
                        a.Timestamp,
                        a.Region
                    }).ToList();
                }

                // Add model-specific metrics
                var modelMetrics = _metricsService.GetModelMetrics();
                if (modelMetrics.Count > 0)
                {
                    data["modelMetrics"] = modelMetrics.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            HitRate = kvp.Value.GetHitRate(),
                            AvgResponseTime = kvp.Value.GetAverageRetrievalTimeMs(),
                            Hits = kvp.Value.Hits,
                            Misses = kvp.Value.Misses
                        });
                }

                // Determine final health status
                if (!isHealthy)
                {
                    data["issues"] = issues;
                    return HealthCheckResult.Unhealthy("Cache infrastructure has critical issues", data: data);
                }
                else if (isDegraded || issues.Count > 0)
                {
                    data["issues"] = issues;
                    return HealthCheckResult.Degraded("Cache infrastructure is degraded", data: data);
                }
                else
                {
                    data["summary"] = new
                    {
                        hitRate = $"{hitRate:P1}",
                        avgResponseTime = $"{avgResponseTime:F1}ms",
                        overallMemoryUsage = totalMemoryLimit > 0 ? $"{(totalMemoryUsed / totalMemoryLimit):P1}" : "N/A",
                        activeRegions = regions.Count(r => r.Value.UseMemoryCache || r.Value.UseDistributedCache)
                    };
                    return HealthCheckResult.Healthy("Cache infrastructure is healthy", data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cache health check failed");
                return HealthCheckResult.Unhealthy("Cache health check failed", ex, data);
            }
        }
    }
}