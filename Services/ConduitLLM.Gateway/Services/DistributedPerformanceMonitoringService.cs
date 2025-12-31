using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Configuration.DTOs.HealthMonitoring;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Gateway.Interfaces;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Distributed performance monitoring service that stores metrics in Redis for multi-instance consistency
    /// </summary>
    public class DistributedPerformanceMonitoringService : IDistributedPerformanceMonitoringService, IHostedService, IDisposable
    {
        private readonly IDatabase _database;
        private readonly IDistributedAlertManagementService _alertManagementService;
        private readonly ILogger<DistributedPerformanceMonitoringService> _logger;
        private readonly PerformanceMonitoringOptions _options;
        
        public string InstanceId { get; }
        
        // Redis keys
        private const string MetricsPrefix = "perf_metrics";
        private const string EndpointMetricsPrefix = "endpoint_metrics";
        private const string CacheMetricsPrefix = "cache_metrics";
        private const string ConnectionPoolMetricsPrefix = "pool_metrics";
        private const string InstancesSetKey = "perf_monitoring_instances";
        private const string RequestsStreamKey = "request_metrics_stream";
        private const string DatabaseOpsStreamKey = "database_ops_stream";
        
        private Timer? _metricsAggregationTimer;
        private Timer? _thresholdCheckTimer;
        private Timer? _heartbeatTimer;
        private readonly SemaphoreSlim _aggregationSemaphore;

        public DistributedPerformanceMonitoringService(
            IConnectionMultiplexer redis,
            IDistributedAlertManagementService alertManagementService,
            ILogger<DistributedPerformanceMonitoringService> logger,
            IOptions<PerformanceMonitoringOptions> options)
        {
            _database = redis.GetDatabase();
            _alertManagementService = alertManagementService;
            _logger = logger;
            _options = options.Value;
            InstanceId = Environment.MachineName + "_" + Environment.ProcessId + "_" + Guid.NewGuid().ToString("N")[..8];
            _aggregationSemaphore = new SemaphoreSlim(1, 1);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RegisterInstanceAsync();
            
            _logger.LogInformation("Distributed performance monitoring service started with instance ID: {InstanceId}", InstanceId);

            // Start heartbeat timer (every 30 seconds)
            _heartbeatTimer = new Timer(
                async _ => await UpdateHeartbeatAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));

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
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Distributed performance monitoring service stopping...");

            _heartbeatTimer?.Change(Timeout.Infinite, 0);
            _metricsAggregationTimer?.Change(Timeout.Infinite, 0);
            _thresholdCheckTimer?.Change(Timeout.Infinite, 0);

            await UnregisterInstanceAsync();
        }

        public async Task RegisterInstanceAsync()
        {
            var instanceData = new
            {
                InstanceId,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "data", JsonSerializer.Serialize(instanceData));
            await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
        }

        public async Task UnregisterInstanceAsync()
        {
            await _database.KeyDeleteAsync($"{InstancesSetKey}:{InstanceId}");
        }

        public async Task UpdateHeartbeatAsync()
        {
            try
            {
                await _database.HashSetAsync($"{InstancesSetKey}:{InstanceId}", "last_heartbeat", DateTime.UtcNow.Ticks);
                await _database.KeyExpireAsync($"{InstancesSetKey}:{InstanceId}", TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update heartbeat for instance {InstanceId}", InstanceId);
            }
        }

        public async Task RecordRequestMetricAsync(string endpoint, double responseTimeMs, bool isSuccess)
        {
            try
            {
                var metric = new
                {
                    InstanceId,
                    Endpoint = endpoint,
                    ResponseTimeMs = responseTimeMs,
                    IsSuccess = isSuccess,
                    Timestamp = DateTime.UtcNow.Ticks
                };

                // Add to Redis stream for time-series data
                await _database.StreamAddAsync(RequestsStreamKey, "data", JsonSerializer.Serialize(metric));
                
                // Trim stream to keep only recent data (last hour)
                await _database.StreamTrimAsync(RequestsStreamKey, _options.MaxMetricsRetention, false);

            // Update endpoint-specific aggregated metrics atomically
            var endpointKey = $"{EndpointMetricsPrefix}:{endpoint}";
            var script = @"
                local key = KEYS[1]
                local responseTime = tonumber(ARGV[1])
                local isSuccess = ARGV[2] == 'true'
                
                local current = redis.call('HMGET', key, 'total_requests', 'successful_requests', 'total_response_time', 'max_response_time', 'min_response_time')
                
                local totalRequests = tonumber(current[1]) or 0
                local successfulRequests = tonumber(current[2]) or 0
                local totalResponseTime = tonumber(current[3]) or 0
                local maxResponseTime = tonumber(current[4]) or responseTime
                local minResponseTime = tonumber(current[5]) or responseTime
                
                totalRequests = totalRequests + 1
                if isSuccess then
                    successfulRequests = successfulRequests + 1
                end
                totalResponseTime = totalResponseTime + responseTime
                maxResponseTime = math.max(maxResponseTime, responseTime)
                minResponseTime = math.min(minResponseTime, responseTime)
                
                redis.call('HMSET', key,
                    'endpoint', ARGV[3],
                    'total_requests', totalRequests,
                    'successful_requests', successfulRequests,
                    'total_response_time', totalResponseTime,
                    'max_response_time', maxResponseTime,
                    'min_response_time', minResponseTime,
                    'last_updated', ARGV[4]
                )
                
                redis.call('EXPIRE', key, 3600)
                
                return totalRequests
            ";

                await _database.ScriptEvaluateAsync(script, new RedisKey[] { endpointKey }, 
                    new RedisValue[] { responseTimeMs, isSuccess.ToString(), endpoint, DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record request metric for endpoint {Endpoint}", endpoint);
            }
        }

        public async Task RecordDatabaseQueryMetricAsync(string operation, double executionTimeMs)
        {
            try
            {
                var metric = new
                {
                    InstanceId,
                    Operation = operation,
                    ExecutionTimeMs = executionTimeMs,
                    Timestamp = DateTime.UtcNow.Ticks
                };

                await _database.StreamAddAsync(DatabaseOpsStreamKey, "data", JsonSerializer.Serialize(metric));
                await _database.StreamTrimAsync(DatabaseOpsStreamKey, _options.MaxMetricsRetention, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record database query metric for operation {Operation}", operation);
            }
        }

        public async Task RecordCacheMetricAsync(string operation, bool isHit)
        {
            var cacheKey = $"{CacheMetricsPrefix}:{operation}";
            var script = @"
                local key = KEYS[1]
                local isHit = ARGV[1] == 'true'
                
                local current = redis.call('HMGET', key, 'total_requests', 'hits')
                local totalRequests = tonumber(current[1]) or 0
                local hits = tonumber(current[2]) or 0
                
                totalRequests = totalRequests + 1
                if isHit then
                    hits = hits + 1
                end
                
                redis.call('HMSET', key,
                    'operation', ARGV[2],
                    'total_requests', totalRequests,
                    'hits', hits,
                    'last_updated', ARGV[3]
                )
                
                redis.call('EXPIRE', key, 3600)
                return totalRequests
            ";

            try
            {
                await _database.ScriptEvaluateAsync(script, new RedisKey[] { cacheKey }, 
                    new RedisValue[] { isHit.ToString(), operation, DateTime.UtcNow.Ticks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record cache metric for operation {Operation}", operation);
            }
        }

        public async Task RecordConnectionPoolMetricAsync(string poolName, int active, int idle, int waitQueue)
        {
            var poolKey = $"{ConnectionPoolMetricsPrefix}:{poolName}";
            var data = new Dictionary<string, RedisValue>
            {
                ["pool_name"] = poolName,
                ["active_connections"] = active,
                ["idle_connections"] = idle,
                ["wait_queue_length"] = waitQueue,
                ["last_updated"] = DateTime.UtcNow.Ticks,
                ["instance_id"] = InstanceId
            };

            try
            {
                // Get current max_active to update it if needed
                var currentMaxActive = await _database.HashGetAsync(poolKey, "max_active");
                var maxActive = Math.Max(active, (int)(currentMaxActive.HasValue ? currentMaxActive : 0));
                data["max_active"] = maxActive;

                await _database.HashSetAsync(poolKey, data.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray());
                await _database.KeyExpireAsync(poolKey, TimeSpan.FromMinutes(10));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record connection pool metric for pool {PoolName}", poolName);
            }
        }

        public async Task<PerformanceMetrics> GetAggregatedMetricsAsync()
        {
            await _aggregationSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-_options.MetricsWindowSeconds);
                
                // Get recent requests from stream
                var requestEntries = await _database.StreamRangeAsync(RequestsStreamKey, 
                    $"{windowStart.Ticks}", "+", _options.MaxMetricsRetention);
                
                var recentRequests = requestEntries
                    .Select(entry => JsonSerializer.Deserialize<Dictionary<string, object>>(entry.Values[0].Value.ToString()))
                    .Where(r => r != null && long.Parse(r.GetValueOrDefault("Timestamp", "0")?.ToString() ?? "0") > windowStart.Ticks)
                    .ToList();

                var requestCount = recentRequests.Count;
                var successCount = recentRequests.Count(r => r != null && bool.Parse(r.GetValueOrDefault("IsSuccess", "false")?.ToString() ?? "false"));
                var errorRate = requestCount > 0 ? ((double)(requestCount - successCount) / requestCount) * 100 : 0;
                
                var responseTimes = recentRequests
                    .Where(r => r != null)
                    .Select(r => double.Parse(r!.GetValueOrDefault("ResponseTimeMs", "0")?.ToString() ?? "0"))
                    .OrderBy(t => t)
                    .ToList();

                var metrics = new PerformanceMetrics
                {
                    RequestsPerSecond = requestCount / (double)_options.MetricsWindowSeconds,
                    ErrorRatePercent = errorRate,
                    ActiveRequests = 0 // Would need separate tracking
                };

                if (responseTimes.Count > 0)
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

        public async Task<Dictionary<string, ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics>> GetAggregatedEndpointMetricsAsync()
        {
            var pattern = $"{EndpointMetricsPrefix}:*";
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            
            var endpointMetrics = new Dictionary<string, ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics>();
            
            foreach (var key in keys)
            {
                var hash = await _database.HashGetAllAsync(key);
                if (hash.Length == 0) continue;
                
                var hashDict = hash.ToDictionary(x => x.Name, x => x.Value);
                
                var endpoint = hashDict.GetValueOrDefault("endpoint", "").ToString();
                var totalRequests = (int)hashDict.GetValueOrDefault("total_requests", 0);
                var successfulRequests = (int)hashDict.GetValueOrDefault("successful_requests", 0);
                var totalResponseTime = (double)hashDict.GetValueOrDefault("total_response_time", 0);
                var maxResponseTime = (double)hashDict.GetValueOrDefault("max_response_time", 0);
                var minResponseTime = (double)hashDict.GetValueOrDefault("min_response_time", 0);
                var lastUpdatedTicks = (long)hashDict.GetValueOrDefault("last_updated", 0);
                
                endpointMetrics[endpoint ?? ""] = new ConduitLLM.Configuration.DTOs.HealthMonitoring.EndpointMetrics
                {
                    Endpoint = endpoint ?? "",
                    TotalRequests = totalRequests,
                    SuccessfulRequests = successfulRequests,
                    TotalResponseTime = totalResponseTime,
                    MaxResponseTime = maxResponseTime,
                    MinResponseTime = minResponseTime,
                    LastUpdated = new DateTime(lastUpdatedTicks)
                };
            }
            
            return endpointMetrics;
        }

        public async Task<List<string>> GetActiveInstancesAsync()
        {
            var pattern = $"{InstancesSetKey}:*";
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern);
            
            var instances = new List<string>();
            var cutoffTime = DateTime.UtcNow.AddMinutes(-1); // Consider instances active if heartbeat within last minute
            
            foreach (var key in keys)
            {
                var lastHeartbeat = await _database.HashGetAsync(key, "last_heartbeat");
                if (lastHeartbeat.HasValue)
                {
                    var heartbeatTime = new DateTime((long)lastHeartbeat);
                    if (heartbeatTime > cutoffTime)
                    {
                        var instanceId = key.ToString().Split(':').Last();
                        instances.Add(instanceId);
                    }
                }
            }
            
            return instances;
        }

        private async Task AggregateMetricsAsync()
        {
            try
            {
                var metrics = await GetAggregatedMetricsAsync();
                
                // Store aggregated metrics snapshot
                var key = $"{MetricsPrefix}_snapshot_{DateTime.UtcNow:yyyyMMddHHmmss}";
                await _database.StringSetAsync(key, JsonSerializer.Serialize(metrics), TimeSpan.FromHours(24));
                
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
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
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
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints().First());
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

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        public void RecordRequestMetric(string endpoint, double responseTimeMs, bool isSuccess)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordRequestMetricAsync(endpoint, responseTimeMs, isSuccess));
        }

        public void RecordDatabaseQueryMetric(string operation, double executionTimeMs)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordDatabaseQueryMetricAsync(operation, executionTimeMs));
        }

        public void RecordCacheMetric(string operation, bool isHit)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordCacheMetricAsync(operation, isHit));
        }

        public void RecordConnectionPoolMetric(string poolName, int active, int idle, int waitQueue)
        {
            // Use fire-and-forget async call to maintain interface compatibility
            _ = Task.Run(() => RecordConnectionPoolMetricAsync(poolName, active, idle, waitQueue));
        }

        public async Task<PerformanceMetrics> GetCurrentMetricsAsync()
        {
            return await GetAggregatedMetricsAsync();
        }

        public async Task<Dictionary<string, EndpointMetrics>> GetEndpointMetricsAsync()
        {
            var distributedMetrics = await GetAggregatedEndpointMetricsAsync();
            
            // Convert to the base EndpointMetrics type
            var result = new Dictionary<string, EndpointMetrics>();
            foreach (var kvp in distributedMetrics)
            {
                var dist = kvp.Value;
                result[kvp.Key] = new EndpointMetrics
                {
                    Endpoint = dist.Endpoint,
                    TotalRequests = dist.TotalRequests,
                    SuccessfulRequests = dist.SuccessfulRequests,
                    TotalResponseTime = dist.TotalResponseTime,
                    MaxResponseTime = dist.MaxResponseTime,
                    MinResponseTime = dist.MinResponseTime,
                    LastUpdated = dist.LastUpdated
                };
            }
            return result;
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _metricsAggregationTimer?.Dispose();
            _thresholdCheckTimer?.Dispose();
            _aggregationSemaphore?.Dispose();
        }
    }
}