using System.Text.Json;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Interfaces;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class DistributedPerformanceMonitoringService
    {
        private async Task CheckThresholdsAsync()
        {
            try
            {
                var metrics = await GetAggregatedMetricsAsync();

                // Check response time thresholds
                if (metrics.P99ResponseTimeMs > _options.ResponseTimeP99CriticalMs)
                {
                    await TriggerPerformanceAlertAsync(
                        AlertSeverity.Critical,
                        "Critical Response Time",
                        $"P99 response time is {metrics.P99ResponseTimeMs:F0}ms (threshold: {_options.ResponseTimeP99CriticalMs}ms)",
                        metrics);
                }
                else if (metrics.P95ResponseTimeMs > _options.ResponseTimeP95WarningMs)
                {
                    await TriggerPerformanceAlertAsync(
                        AlertSeverity.Warning,
                        "High Response Time",
                        $"P95 response time is {metrics.P95ResponseTimeMs:F0}ms (threshold: {_options.ResponseTimeP95WarningMs}ms)",
                        metrics);
                }

                // Check error rate thresholds
                if (metrics.ErrorRatePercent > _options.ErrorRateCriticalPercent)
                {
                    await TriggerPerformanceAlertAsync(
                        AlertSeverity.Critical,
                        "Critical Error Rate",
                        $"Error rate is {metrics.ErrorRatePercent:F1}% (threshold: {_options.ErrorRateCriticalPercent}%)",
                        metrics);
                }
                else if (metrics.ErrorRatePercent > _options.ErrorRateWarningPercent)
                {
                    await TriggerPerformanceAlertAsync(
                        AlertSeverity.Warning,
                        "High Error Rate",
                        $"Error rate is {metrics.ErrorRatePercent:F1}% (threshold: {_options.ErrorRateWarningPercent}%)",
                        metrics);
                }

                // Check request rate thresholds
                if (metrics.RequestsPerSecond > _options.RequestRateHighThreshold)
                {
                    await TriggerPerformanceAlertAsync(
                        AlertSeverity.Warning,
                        "High Request Rate",
                        $"Request rate is {metrics.RequestsPerSecond:F1} req/s (threshold: {_options.RequestRateHighThreshold} req/s)",
                        metrics);
                }

                // Check database and cache performance
                await CheckDatabasePerformanceAsync();
                await CheckCachePerformanceAsync();
                await CheckConnectionPoolsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking performance thresholds");
            }
        }

        private async Task CheckDatabasePerformanceAsync()
        {
            var windowStart = DateTime.UtcNow.AddSeconds(-_options.MetricsWindowSeconds);
            var entries = await _database.StreamRangeAsync(DatabaseOpsStreamKey,
                $"{windowStart.Ticks}", "+", _options.MaxMetricsRetention);

            var recentQueries = entries
                .Select(entry => JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Values[0].Value.ToString()))
                .Where(q => q != null && long.Parse(q.GetValueOrDefault("Timestamp", "0")?.ToString() ?? "0") > windowStart.Ticks)
                .ToList();

            if (recentQueries.Count == 0) return;

            var slowQueries = recentQueries
                .Where(q => q != null && double.Parse(q.GetValueOrDefault("ExecutionTimeMs", "0")?.ToString() ?? "0") > _options.DatabaseSlowQueryThresholdMs)
                .ToList();

            if (slowQueries.Count > _options.DatabaseSlowQueryCountThreshold)
            {
                var avgSlowQueryTime = slowQueries.Average(q => q != null ? double.Parse(q.GetValueOrDefault("ExecutionTimeMs", "0")?.ToString() ?? "0") : 0);
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.PerformanceDegradation,
                    Component = "Database",
                    Title = "High Number of Slow Queries",
                    Message = $"Detected {slowQueries.Count} slow queries across all instances in the last {_options.MetricsWindowSeconds} seconds. Average execution time: {avgSlowQueryTime:F0}ms",
                    Context = new Dictionary<string, object>
                    {
                        ["slowQueryCount"] = slowQueries.Count,
                        ["averageExecutionTime"] = avgSlowQueryTime,
                        ["threshold"] = _options.DatabaseSlowQueryThresholdMs,
                        ["detectedByInstance"] = InstanceId,
                        ["operations"] = slowQueries.Where(q => q != null).GroupBy(q => q!.GetValueOrDefault("Operation", "")?.ToString() ?? "")
                            .Select(g => new { Operation = g.Key, Count = g.Count() })
                            .OrderByDescending(x => x.Count)
                            .Take(5)
                            .ToList()
                    },
                    SuggestedActions = new List<string>
                    {
                        "Review slow query log",
                        "Check for missing database indexes",
                        "Analyze query execution plans",
                        "Consider query optimization"
                    }
                });
            }
        }

        private async Task CheckCachePerformanceAsync()
        {
            var pattern = $"{CacheMetricsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            foreach (var key in keys)
            {
                var hash = await _database.HashGetAllAsync(key);
                if (hash.Length == 0) continue;

                var hashDict = hash.ToDictionary(x => x.Name, x => x.Value);
                var operation = hashDict.GetValueOrDefault("operation", "");
                var totalRequests = (int)hashDict.GetValueOrDefault("total_requests", 0);
                var hits = (int)hashDict.GetValueOrDefault("hits", 0);

                var hitRate = totalRequests > 0 ? (double)hits / totalRequests * 100 : 100;

                if (hitRate < _options.CacheHitRateLowThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.PerformanceDegradation,
                        Component = "Cache",
                        Title = $"Low Cache Hit Rate for {operation}",
                        Message = $"Cache hit rate is {hitRate:F1}% (threshold: {_options.CacheHitRateLowThreshold}%)",
                        Context = new Dictionary<string, object>
                        {
                            ["operation"] = operation,
                            ["hitRate"] = hitRate,
                            ["totalRequests"] = totalRequests,
                            ["hits"] = hits,
                            ["detectedByInstance"] = InstanceId
                        },
                        SuggestedActions = new List<string>
                        {
                            "Review cache eviction policies",
                            "Increase cache size if needed",
                            "Analyze cache key patterns",
                            "Check for cache invalidation issues"
                        }
                    });
                }
            }
        }

        private async Task CheckConnectionPoolsAsync()
        {
            var pattern = $"{ConnectionPoolMetricsPrefix}:*";
            var server = _database.Multiplexer.GetPrimaryServer();
            var keys = server.Keys(pattern: pattern);

            foreach (var key in keys)
            {
                var hash = await _database.HashGetAllAsync(key);
                if (hash.Length == 0) continue;

                var hashDict = hash.ToDictionary(x => x.Name, x => x.Value);
                var poolName = hashDict.GetValueOrDefault("pool_name", "");
                var activeConnections = (int)hashDict.GetValueOrDefault("active_connections", 0);
                var idleConnections = (int)hashDict.GetValueOrDefault("idle_connections", 0);
                var waitQueueLength = (int)hashDict.GetValueOrDefault("wait_queue_length", 0);

                var totalConnections = activeConnections + idleConnections;
                var utilizationPercent = totalConnections > 0 ? (double)activeConnections / totalConnections * 100 : 0;

                if (utilizationPercent > _options.ConnectionPoolHighUtilizationThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.ResourceExhaustion,
                        Component = $"{poolName} Connection Pool",
                        Title = "High Connection Pool Utilization",
                        Message = $"Connection pool utilization is {utilizationPercent:F1}% with {waitQueueLength} requests waiting",
                        Context = new Dictionary<string, object>
                        {
                            ["poolName"] = poolName,
                            ["activeConnections"] = activeConnections,
                            ["idleConnections"] = idleConnections,
                            ["waitQueueLength"] = waitQueueLength,
                            ["utilizationPercent"] = utilizationPercent,
                            ["detectedByInstance"] = InstanceId
                        }
                    });
                }

                if (waitQueueLength > _options.ConnectionPoolQueueWarningThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Error,
                        Type = AlertType.ResourceExhaustion,
                        Component = $"{poolName} Connection Pool",
                        Title = "Connection Pool Queue Buildup",
                        Message = $"Connection pool has {waitQueueLength} requests waiting in queue",
                        Context = new Dictionary<string, object>
                        {
                            ["poolName"] = poolName,
                            ["waitQueueLength"] = waitQueueLength,
                            ["activeConnections"] = activeConnections,
                            ["detectedByInstance"] = InstanceId
                        }
                    });
                }
            }
        }

        private async Task TriggerPerformanceAlertAsync(
            AlertSeverity severity,
            string title,
            string message,
            PerformanceMetrics metrics)
        {
            await _alertManagementService.TriggerAlertAsync(new HealthAlert
            {
                Severity = severity,
                Type = AlertType.PerformanceDegradation,
                Component = "API Performance",
                Title = title,
                Message = message,
                Context = new Dictionary<string, object>
                {
                    ["requestsPerSecond"] = metrics.RequestsPerSecond,
                    ["errorRatePercent"] = metrics.ErrorRatePercent,
                    ["averageResponseTime"] = metrics.AverageResponseTimeMs,
                    ["p95ResponseTime"] = metrics.P95ResponseTimeMs,
                    ["p99ResponseTime"] = metrics.P99ResponseTimeMs,
                    ["detectedByInstance"] = InstanceId
                }
            });
        }
    }
}
