using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for monitoring performance metrics and triggering alerts based on thresholds
    /// </summary>
    public interface IPerformanceMonitoringService
    {
        void RecordRequestMetric(string endpoint, double responseTimeMs, bool isSuccess);
        void RecordDatabaseQueryMetric(string operation, double executionTimeMs);
        void RecordCacheMetric(string operation, bool isHit);
        void RecordConnectionPoolMetric(string poolName, int active, int idle, int waitQueue);
        Task<PerformanceMetrics> GetCurrentMetricsAsync();
        Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync();
    }

    /// <summary>
    /// Implementation of performance monitoring service
    /// </summary>
    public class PerformanceMonitoringService : IPerformanceMonitoringService, IHostedService, IDisposable
    {
        private readonly IAlertManagementService _alertManagementService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly PerformanceMonitoringOptions _options;
        
        // Metrics storage
        private readonly ConcurrentDictionary<string, EndpointMetrics> _endpointMetrics;
        private readonly ConcurrentQueue<RequestMetric> _recentRequests;
        private readonly ConcurrentQueue<DatabaseMetric> _recentDatabaseOps;
        private readonly ConcurrentDictionary<string, CacheMetrics> _cacheMetrics;
        private readonly ConcurrentDictionary<string, ConnectionPoolMetrics> _connectionPoolMetrics;
        
        private Timer? _metricsAggregationTimer;
        private Timer? _thresholdCheckTimer;
        private readonly SemaphoreSlim _aggregationSemaphore;

        public PerformanceMonitoringService(
            IAlertManagementService alertManagementService,
            IMemoryCache cache,
            ILogger<PerformanceMonitoringService> logger,
            IOptions<PerformanceMonitoringOptions> options)
        {
            _alertManagementService = alertManagementService;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
            
            _endpointMetrics = new ConcurrentDictionary<string, EndpointMetrics>();
            _recentRequests = new ConcurrentQueue<RequestMetric>();
            _recentDatabaseOps = new ConcurrentQueue<DatabaseMetric>();
            _cacheMetrics = new ConcurrentDictionary<string, CacheMetrics>();
            _connectionPoolMetrics = new ConcurrentDictionary<string, ConnectionPoolMetrics>();
            _aggregationSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Record a request metric
        /// </summary>
        public void RecordRequestMetric(string endpoint, double responseTimeMs, bool isSuccess)
        {
            var metric = new RequestMetric
            {
                Endpoint = endpoint,
                ResponseTimeMs = responseTimeMs,
                IsSuccess = isSuccess,
                Timestamp = DateTime.UtcNow
            };

            _recentRequests.Enqueue(metric);
            
            // Keep only recent metrics
            while (_recentRequests.Count > _options.MaxMetricsRetention)
            {
                _recentRequests.TryDequeue(out _);
            }

            // Update endpoint-specific metrics
            _endpointMetrics.AddOrUpdate(endpoint,
                new EndpointMetrics
                {
                    Endpoint = endpoint,
                    TotalRequests = 1,
                    SuccessfulRequests = isSuccess ? 1 : 0,
                    TotalResponseTime = responseTimeMs,
                    MaxResponseTime = responseTimeMs,
                    MinResponseTime = responseTimeMs,
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.TotalRequests++;
                    if (isSuccess) existing.SuccessfulRequests++;
                    existing.TotalResponseTime += responseTimeMs;
                    existing.MaxResponseTime = Math.Max(existing.MaxResponseTime, responseTimeMs);
                    existing.MinResponseTime = Math.Min(existing.MinResponseTime, responseTimeMs);
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Record a database query metric
        /// </summary>
        public void RecordDatabaseQueryMetric(string operation, double executionTimeMs)
        {
            var metric = new DatabaseMetric
            {
                Operation = operation,
                ExecutionTimeMs = executionTimeMs,
                Timestamp = DateTime.UtcNow
            };

            _recentDatabaseOps.Enqueue(metric);
            
            // Keep only recent metrics
            while (_recentDatabaseOps.Count > _options.MaxMetricsRetention)
            {
                _recentDatabaseOps.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Record a cache metric
        /// </summary>
        public void RecordCacheMetric(string operation, bool isHit)
        {
            _cacheMetrics.AddOrUpdate(operation,
                new CacheMetrics
                {
                    Operation = operation,
                    TotalRequests = 1,
                    Hits = isHit ? 1 : 0,
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.TotalRequests++;
                    if (isHit) existing.Hits++;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Record connection pool metrics
        /// </summary>
        public void RecordConnectionPoolMetric(string poolName, int active, int idle, int waitQueue)
        {
            _connectionPoolMetrics.AddOrUpdate(poolName,
                new ConnectionPoolMetrics
                {
                    PoolName = poolName,
                    ActiveConnections = active,
                    IdleConnections = idle,
                    WaitQueueLength = waitQueue,
                    MaxActive = active,
                    LastUpdated = DateTime.UtcNow
                },
                (key, existing) =>
                {
                    existing.ActiveConnections = active;
                    existing.IdleConnections = idle;
                    existing.WaitQueueLength = waitQueue;
                    existing.MaxActive = Math.Max(existing.MaxActive, active);
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        /// <summary>
        /// Get current performance metrics
        /// </summary>
        public async Task<PerformanceMetrics> GetCurrentMetricsAsync()
        {
            await _aggregationSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var recentWindow = now.AddSeconds(-_options.MetricsWindowSeconds);
                
                // Calculate request metrics
                var recentRequestsList = _recentRequests
                    .Where(r => r.Timestamp > recentWindow)
                    .ToList();

                var requestCount = recentRequestsList.Count;
                var successCount = recentRequestsList.Count(r => r.IsSuccess);
                var errorRate = requestCount > 0 ? ((double)(requestCount - successCount) / requestCount) * 100 : 0;
                
                var responseTimes = recentRequestsList
                    .Select(r => r.ResponseTimeMs)
                    .OrderBy(t => t)
                    .ToList();

                var metrics = new PerformanceMetrics
                {
                    RequestsPerSecond = requestCount / _options.MetricsWindowSeconds,
                    ErrorRatePercent = errorRate,
                    ActiveRequests = 0 // Would need to track this separately
                };

                if (responseTimes.Any())
                {
                    metrics.AverageResponseTimeMs = responseTimes.Average();
                    metrics.P95ResponseTimeMs = GetPercentile(responseTimes, 0.95);
                    metrics.P99ResponseTimeMs = GetPercentile(responseTimes, 0.99);
                }

                return metrics;
            }
            finally
            {
                _aggregationSemaphore.Release();
            }
        }

        /// <summary>
        /// Get endpoint-specific metrics
        /// </summary>
        public Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync()
        {
            return Task.FromResult(_endpointMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performance monitoring service started");

            // Start metrics aggregation timer
            _metricsAggregationTimer = new Timer(
                async _ => await AggregateMetricsAsync(),
                null,
                TimeSpan.FromSeconds(_options.AggregationIntervalSeconds),
                TimeSpan.FromSeconds(_options.AggregationIntervalSeconds));

            // Start threshold checking timer
            _thresholdCheckTimer = new Timer(
                async _ => await CheckThresholdsAsync(),
                null,
                TimeSpan.FromSeconds(_options.ThresholdCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.ThresholdCheckIntervalSeconds));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Performance monitoring service stopping");

            _metricsAggregationTimer?.Change(Timeout.Infinite, 0);
            _thresholdCheckTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private async Task AggregateMetricsAsync()
        {
            try
            {
                var metrics = await GetCurrentMetricsAsync();
                
                // Store aggregated metrics in cache for historical tracking
                var key = $"perf_metrics_{DateTime.UtcNow:yyyyMMddHHmmss}";
                _cache.Set(key, metrics, TimeSpan.FromHours(24));
                
                _logger.LogDebug("Aggregated performance metrics: {RequestsPerSecond} req/s, {ErrorRate}% errors, {AvgResponse}ms avg response",
                    metrics.RequestsPerSecond, metrics.ErrorRatePercent, metrics.AverageResponseTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating performance metrics");
            }
        }

        private async Task CheckThresholdsAsync()
        {
            try
            {
                var metrics = await GetCurrentMetricsAsync();
                
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

                // Check database query performance
                await CheckDatabasePerformanceAsync();

                // Check cache performance
                await CheckCachePerformanceAsync();

                // Check connection pools
                await CheckConnectionPoolsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking performance thresholds");
            }
        }

        private async Task CheckDatabasePerformanceAsync()
        {
            var recentWindow = DateTime.UtcNow.AddSeconds(-_options.MetricsWindowSeconds);
            var recentQueries = _recentDatabaseOps
                .Where(q => q.Timestamp > recentWindow)
                .ToList();

            if (!recentQueries.Any()) return;

            var slowQueries = recentQueries
                .Where(q => q.ExecutionTimeMs > _options.DatabaseSlowQueryThresholdMs)
                .ToList();

            if (slowQueries.Count > _options.DatabaseSlowQueryCountThreshold)
            {
                var avgSlowQueryTime = slowQueries.Average(q => q.ExecutionTimeMs);
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.PerformanceDegradation,
                    Component = "Database",
                    Title = "High Number of Slow Queries",
                    Message = $"Detected {slowQueries.Count} slow queries in the last {_options.MetricsWindowSeconds} seconds. Average execution time: {avgSlowQueryTime:F0}ms",
                    Context = new Dictionary<string, object>
                    {
                        ["slowQueryCount"] = slowQueries.Count,
                        ["averageExecutionTime"] = avgSlowQueryTime,
                        ["threshold"] = _options.DatabaseSlowQueryThresholdMs,
                        ["operations"] = slowQueries.GroupBy(q => q.Operation)
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
            foreach (var cacheMetric in _cacheMetrics.Values)
            {
                var hitRate = cacheMetric.TotalRequests > 0 
                    ? (double)cacheMetric.Hits / cacheMetric.TotalRequests * 100 
                    : 100;

                if (hitRate < _options.CacheHitRateLowThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.PerformanceDegradation,
                        Component = "Cache",
                        Title = $"Low Cache Hit Rate for {cacheMetric.Operation}",
                        Message = $"Cache hit rate is {hitRate:F1}% (threshold: {_options.CacheHitRateLowThreshold}%)",
                        Context = new Dictionary<string, object>
                        {
                            ["operation"] = cacheMetric.Operation,
                            ["hitRate"] = hitRate,
                            ["totalRequests"] = cacheMetric.TotalRequests,
                            ["hits"] = cacheMetric.Hits
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
            foreach (var poolMetric in _connectionPoolMetrics.Values)
            {
                var totalConnections = poolMetric.ActiveConnections + poolMetric.IdleConnections;
                var utilizationPercent = totalConnections > 0 
                    ? (double)poolMetric.ActiveConnections / totalConnections * 100 
                    : 0;

                if (utilizationPercent > _options.ConnectionPoolHighUtilizationThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.ResourceExhaustion,
                        Component = $"{poolMetric.PoolName} Connection Pool",
                        Title = $"High Connection Pool Utilization",
                        Message = $"Connection pool utilization is {utilizationPercent:F1}% with {poolMetric.WaitQueueLength} requests waiting",
                        Context = new Dictionary<string, object>
                        {
                            ["poolName"] = poolMetric.PoolName,
                            ["activeConnections"] = poolMetric.ActiveConnections,
                            ["idleConnections"] = poolMetric.IdleConnections,
                            ["waitQueueLength"] = poolMetric.WaitQueueLength,
                            ["utilizationPercent"] = utilizationPercent
                        },
                        SuggestedActions = new List<string>
                        {
                            "Increase connection pool size",
                            "Review connection timeout settings",
                            "Check for connection leaks",
                            "Optimize query performance"
                        }
                    });
                }

                if (poolMetric.WaitQueueLength > _options.ConnectionPoolQueueWarningThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Error,
                        Type = AlertType.ResourceExhaustion,
                        Component = $"{poolMetric.PoolName} Connection Pool",
                        Title = "Connection Pool Queue Buildup",
                        Message = $"Connection pool has {poolMetric.WaitQueueLength} requests waiting in queue",
                        Context = new Dictionary<string, object>
                        {
                            ["poolName"] = poolMetric.PoolName,
                            ["waitQueueLength"] = poolMetric.WaitQueueLength,
                            ["activeConnections"] = poolMetric.ActiveConnections
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
                    ["p99ResponseTime"] = metrics.P99ResponseTimeMs
                }
            });
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        public void Dispose()
        {
            _metricsAggregationTimer?.Dispose();
            _thresholdCheckTimer?.Dispose();
            _aggregationSemaphore?.Dispose();
        }
    }

    // Supporting classes
    public class RequestMetric
    {
        public string Endpoint { get; set; } = string.Empty;
        public double ResponseTimeMs { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DatabaseMetric
    {
        public string Operation { get; set; } = string.Empty;
        public double ExecutionTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CacheMetrics
    {
        public string Operation { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int Hits { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class ConnectionPoolMetrics
    {
        public string PoolName { get; set; } = string.Empty;
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int WaitQueueLength { get; set; }
        public int MaxActive { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class EndpointMetrics
    {
        public string Endpoint { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public double TotalResponseTime { get; set; }
        public double MaxResponseTime { get; set; }
        public double MinResponseTime { get; set; }
        public DateTime LastUpdated { get; set; }

        public double AverageResponseTime => TotalRequests > 0 ? TotalResponseTime / TotalRequests : 0;
        public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests * 100 : 100;
    }

    public class PerformanceMonitoringOptions
    {
        // Metrics collection
        public int MaxMetricsRetention { get; set; } = 10000;
        public int MetricsWindowSeconds { get; set; } = 60;
        public int AggregationIntervalSeconds { get; set; } = 30;
        public int ThresholdCheckIntervalSeconds { get; set; } = 30;

        // Response time thresholds
        public double ResponseTimeP95WarningMs { get; set; } = 1000;
        public double ResponseTimeP99CriticalMs { get; set; } = 5000;

        // Error rate thresholds
        public double ErrorRateWarningPercent { get; set; } = 1;
        public double ErrorRateCriticalPercent { get; set; } = 5;

        // Request rate thresholds
        public double RequestRateHighThreshold { get; set; } = 1000;

        // Database thresholds
        public double DatabaseSlowQueryThresholdMs { get; set; } = 1000;
        public int DatabaseSlowQueryCountThreshold { get; set; } = 10;

        // Cache thresholds
        public double CacheHitRateLowThreshold { get; set; } = 80;

        // Connection pool thresholds
        public double ConnectionPoolHighUtilizationThreshold { get; set; } = 80;
        public int ConnectionPoolQueueWarningThreshold { get; set; } = 10;
    }
}