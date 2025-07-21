using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Health check service for monitoring distributed cache statistics accuracy and performance.
    /// </summary>
    public class CacheStatisticsHealthCheck : BackgroundService, IStatisticsHealthCheck
    {
        private readonly IDistributedCacheStatisticsCollector _statisticsCollector;
        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<CacheStatisticsHealthCheck> _logger;
        private readonly ConcurrentDictionary<string, StatisticsMonitoringAlert> _activeAlerts;
        private readonly ConcurrentDictionary<string, DateTime> _lastInstanceHeartbeat;
        private readonly ConcurrentDictionary<string, PerformanceTracker> _performanceTrackers;
        private StatisticsAlertThresholds _alertThresholds;
        private readonly SemaphoreSlim _checkLock = new(1, 1);
        private DateTime _lastHealthCheck = DateTime.UtcNow;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public event EventHandler<StatisticsMonitoringAlertEventArgs>? AlertTriggered;

        public CacheStatisticsHealthCheck(
            ICacheStatisticsCollector statisticsCollector,
            IConnectionMultiplexer? redis,
            ILogger<CacheStatisticsHealthCheck> logger,
            IOptions<StatisticsAlertThresholds>? alertThresholds = null)
        {
            _statisticsCollector = statisticsCollector as IDistributedCacheStatisticsCollector 
                ?? throw new ArgumentException("Statistics collector must be distributed for health monitoring", nameof(statisticsCollector));
            _redis = redis;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _alertThresholds = alertThresholds?.Value ?? new StatisticsAlertThresholds();
            
            _activeAlerts = new ConcurrentDictionary<string, StatisticsMonitoringAlert>();
            _lastInstanceHeartbeat = new ConcurrentDictionary<string, DateTime>();
            _performanceTrackers = new ConcurrentDictionary<string, PerformanceTracker>();

            // Subscribe to distributed statistics events
            if (_statisticsCollector is IDistributedCacheStatisticsCollector distributed)
            {
                distributed.DistributedStatisticsUpdated += OnDistributedStatisticsUpdated;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cache statistics health monitoring started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _checkLock.WaitAsync(stoppingToken);
                    try
                    {
                        // Perform health checks
                        var healthResult = await PerformHealthCheckAsync(stoppingToken);
                        await ProcessHealthResult(healthResult);

                        // Perform accuracy validation
                        var accuracyReport = await PerformAccuracyValidationAsync(stoppingToken);
                        await ProcessAccuracyReport(accuracyReport);

                        // Update performance metrics
                        await UpdatePerformanceMetricsAsync(stoppingToken);

                        _lastHealthCheck = DateTime.UtcNow;
                    }
                    finally
                    {
                        _checkLock.Release();
                    }

                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in statistics health monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Back off on error
                }
            }

            _logger.LogInformation("Cache statistics health monitoring stopped");
        }

        public async Task<StatisticsHealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return await PerformHealthCheckAsync(cancellationToken);
        }

        private async Task<StatisticsHealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
        {
            var result = new StatisticsHealthCheckResult
            {
                Status = HealthStatus.Healthy,
                ComponentHealth = new Dictionary<string, ComponentHealth>()
            };

            try
            {
                // Check Redis connectivity
                if (_redis != null)
                {
                    var redisHealth = await CheckRedisHealthAsync();
                    result.RedisConnected = redisHealth.Status == HealthStatus.Healthy;
                    result.ComponentHealth["Redis"] = redisHealth;
                    
                    if (!result.RedisConnected)
                    {
                        result.Status = HealthStatus.Unhealthy;
                        result.Messages.Add("Redis connection is unhealthy");
                    }
                }
                else
                {
                    result.RedisConnected = false;
                    result.ComponentHealth["Redis"] = new ComponentHealth
                    {
                        Name = "Redis",
                        Status = HealthStatus.Unhealthy,
                        Message = "Redis not configured",
                        LastCheck = DateTime.UtcNow
                    };
                }

                // Check active instances
                var activeInstances = await _statisticsCollector.GetActiveInstancesAsync(cancellationToken);
                result.ActiveInstances = activeInstances.Count();
                
                // Update heartbeats
                foreach (var instance in activeInstances)
                {
                    _lastInstanceHeartbeat[instance] = DateTime.UtcNow;
                }

                // Check for missing instances
                var now = DateTime.UtcNow;
                var missingInstances = _lastInstanceHeartbeat
                    .Where(kvp => now - kvp.Value > _alertThresholds.MaxInstanceMissingTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                result.MissingInstances = missingInstances.Count;
                
                if (missingInstances.Any())
                {
                    result.Status = HealthStatus.Degraded;
                    result.Messages.Add($"{missingInstances.Count} instances not reporting");
                }

                // Check aggregation performance
                var aggregationHealth = await CheckAggregationPerformanceAsync(cancellationToken);
                result.ComponentHealth["Aggregation"] = aggregationHealth;
                result.AggregationLatencyMs = aggregationHealth.Status == HealthStatus.Healthy 
                    ? GetLatestAggregationLatency() 
                    : -1;

                if (aggregationHealth.Status == HealthStatus.Unhealthy)
                {
                    result.Status = HealthStatus.Degraded;
                }

                // Check Redis memory usage
                if (_redis != null)
                {
                    result.RedisMemoryUsageBytes = await GetRedisMemoryUsageAsync();
                    
                    if (result.RedisMemoryUsageBytes > _alertThresholds.MaxRedisMemoryBytes)
                    {
                        result.Status = HealthStatus.Degraded;
                        result.Messages.Add($"Redis memory usage ({result.RedisMemoryUsageBytes / (1024 * 1024)}MB) exceeds threshold");
                    }
                }

                result.LastSuccessfulAggregation = GetLastSuccessfulAggregation();

                // Check minimum instances
                if (result.ActiveInstances < _alertThresholds.MinActiveInstances)
                {
                    result.Status = HealthStatus.Degraded;
                    result.Messages.Add($"Active instances ({result.ActiveInstances}) below minimum threshold ({_alertThresholds.MinActiveInstances})");
                }

                // Process the health check result to trigger any necessary alerts
                await ProcessHealthResult(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing health check");
                result.Status = HealthStatus.Unhealthy;
                result.Messages.Add($"Health check error: {ex.Message}");
                return result;
            }
        }

        public async Task<StatisticsAccuracyReport> ValidateAccuracyAsync(CancellationToken cancellationToken = default)
        {
            return await PerformAccuracyValidationAsync(cancellationToken);
        }

        private async Task<StatisticsAccuracyReport> PerformAccuracyValidationAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var report = new StatisticsAccuracyReport
            {
                CheckTimestamp = DateTime.UtcNow,
                IsAccurate = true
            };

            try
            {
                foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
                {
                    // Get aggregated statistics
                    var aggregated = await _statisticsCollector.GetAggregatedStatisticsAsync(region, cancellationToken);
                    
                    // Get per-instance statistics
                    var perInstance = await _statisticsCollector.GetPerInstanceStatisticsAsync(region, cancellationToken);
                    
                    if (!perInstance.Any()) continue;

                    // Validate hit count
                    var sumHitCount = perInstance.Sum(kvp => kvp.Value.HitCount);
                    if (Math.Abs(sumHitCount - aggregated.HitCount) > 0)
                    {
                        var drift = CalculateDriftPercentage(aggregated.HitCount, sumHitCount);
                        if (drift > _alertThresholds.MaxDriftPercentage)
                        {
                            report.IsAccurate = false;
                            report.Discrepancies.Add(new RegionDiscrepancy
                            {
                                Region = region,
                                Type = DiscrepancyType.CountMismatch,
                                ExpectedValue = sumHitCount,
                                ActualValue = aggregated.HitCount,
                                DriftPercentage = drift,
                                AffectedInstances = perInstance.Keys.ToList()
                            });
                        }
                        report.MaxDriftPercentage = Math.Max(report.MaxDriftPercentage, drift);
                    }

                    // Validate miss count
                    var sumMissCount = perInstance.Sum(kvp => kvp.Value.MissCount);
                    if (Math.Abs(sumMissCount - aggregated.MissCount) > 0)
                    {
                        var drift = CalculateDriftPercentage(aggregated.MissCount, sumMissCount);
                        if (drift > _alertThresholds.MaxDriftPercentage)
                        {
                            report.IsAccurate = false;
                            report.Discrepancies.Add(new RegionDiscrepancy
                            {
                                Region = region,
                                Type = DiscrepancyType.CountMismatch,
                                ExpectedValue = sumMissCount,
                                ActualValue = aggregated.MissCount,
                                DriftPercentage = drift,
                                AffectedInstances = perInstance.Keys.ToList()
                            });
                        }
                        report.MaxDriftPercentage = Math.Max(report.MaxDriftPercentage, drift);
                    }

                    // Check for instances with suspiciously high variance
                    var avgHitCount = perInstance.Any() ? perInstance.Average(kvp => kvp.Value.HitCount) : 0;
                    var outliers = perInstance
                        .Where(kvp => Math.Abs(kvp.Value.HitCount - avgHitCount) > avgHitCount * 0.5) // 50% variance
                        .Select(kvp => kvp.Key)
                        .ToList();

                    if (outliers.Any())
                    {
                        report.InconsistentInstances.AddRange(outliers);
                    }
                }

                report.CheckDuration = stopwatch.Elapsed;
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating statistics accuracy");
                report.IsAccurate = false;
                report.CheckDuration = stopwatch.Elapsed;
                return report;
            }
        }

        public async Task<StatisticsPerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
        {
            var metrics = new StatisticsPerformanceMetrics
            {
                Timestamp = DateTime.UtcNow
            };

            try
            {
                // Get active instances
                var activeInstances = await _statisticsCollector.GetActiveInstancesAsync(cancellationToken);
                metrics.ActiveInstances = activeInstances.Count();

                // Calculate recording latencies from performance trackers
                var recordingLatencies = _performanceTrackers
                    .Where(kvp => kvp.Key.StartsWith("record:"))
                    .SelectMany(kvp => kvp.Value.GetLatencies())
                    .OrderBy(l => l)
                    .ToList();

                if (recordingLatencies.Any())
                {
                    metrics.AvgRecordingLatencyMs = recordingLatencies.Average();
                    metrics.P95RecordingLatencyMs = GetPercentile(recordingLatencies, 0.95);
                    metrics.P99RecordingLatencyMs = GetPercentile(recordingLatencies, 0.99);
                }

                // Calculate aggregation latencies
                var aggregationLatencies = _performanceTrackers
                    .Where(kvp => kvp.Key.StartsWith("aggregate:"))
                    .SelectMany(kvp => kvp.Value.GetLatencies())
                    .OrderBy(l => l)
                    .ToList();

                if (aggregationLatencies.Any())
                {
                    metrics.AvgAggregationLatencyMs = aggregationLatencies.Average();
                }

                // Calculate operations per second
                var opsTracker = _performanceTrackers.GetOrAdd("operations", _ => new PerformanceTracker());
                metrics.OperationsPerSecond = opsTracker.GetOperationsPerSecond();

                // Get Redis metrics
                if (_redis != null)
                {
                    metrics.RedisMemoryBytes = await GetRedisMemoryUsageAsync();
                    var redisOpsTracker = _performanceTrackers.GetOrAdd("redis_ops", _ => new PerformanceTracker());
                    metrics.RedisOpsPerSecond = redisOpsTracker.GetOperationsPerSecond();
                }

                // Get per-region metrics
                foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
                {
                    var regionTracker = _performanceTrackers.GetOrAdd($"region:{region}", _ => new PerformanceTracker());
                    var stats = await _statisticsCollector.GetAggregatedStatisticsAsync(region, cancellationToken);
                    
                    metrics.RegionMetrics[region] = new RegionPerformanceMetrics
                    {
                        OperationsPerSecond = regionTracker.GetOperationsPerSecond(),
                        AvgLatencyMs = stats.AverageGetTime.TotalMilliseconds,
                        DataVolumeBytes = stats.MemoryUsageBytes
                    };
                }

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                return metrics;
            }
        }

        public Task ConfigureAlertingAsync(StatisticsAlertThresholds thresholds)
        {
            _alertThresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            _logger.LogInformation("Statistics alert thresholds updated");
            return Task.CompletedTask;
        }

        public Task<IEnumerable<StatisticsMonitoringAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            var activeAlerts = _activeAlerts.Values
                .Where(a => !a.IsAcknowledged)
                .OrderByDescending(a => a.TriggeredAt)
                .ToList();

            return Task.FromResult<IEnumerable<StatisticsMonitoringAlert>>(activeAlerts);
        }

        private async Task<ComponentHealth> CheckRedisHealthAsync()
        {
            var health = new ComponentHealth
            {
                Name = "Redis",
                LastCheck = DateTime.UtcNow
            };

            try
            {
                if (_redis == null || !_redis.IsConnected)
                {
                    health.Status = HealthStatus.Unhealthy;
                    health.Message = "Redis not connected";
                    return health;
                }

                var db = _redis.GetDatabase();
                var stopwatch = Stopwatch.StartNew();
                await db.PingAsync();
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 100)
                {
                    health.Status = HealthStatus.Degraded;
                    health.Message = $"Redis latency high: {stopwatch.ElapsedMilliseconds}ms";
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Message = $"Redis responding normally: {stopwatch.ElapsedMilliseconds}ms";
                }

                return health;
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.Message = $"Redis health check failed: {ex.Message}";
                return health;
            }
        }

        private async Task<ComponentHealth> CheckAggregationPerformanceAsync(CancellationToken cancellationToken)
        {
            var health = new ComponentHealth
            {
                Name = "Aggregation",
                LastCheck = DateTime.UtcNow
            };

            try
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Test aggregation for a sample region
                await _statisticsCollector.GetAggregatedStatisticsAsync(CacheRegion.ProviderResponses, cancellationToken);
                
                stopwatch.Stop();

                var latency = stopwatch.ElapsedMilliseconds;
                RecordAggregationLatency(latency);

                if (latency > _alertThresholds.MaxAggregationLatency.TotalMilliseconds)
                {
                    health.Status = HealthStatus.Degraded;
                    health.Message = $"Aggregation latency high: {latency}ms";
                }
                else
                {
                    health.Status = HealthStatus.Healthy;
                    health.Message = $"Aggregation performing well: {latency}ms";
                }

                return health;
            }
            catch (Exception ex)
            {
                health.Status = HealthStatus.Unhealthy;
                health.Message = $"Aggregation check failed: {ex.Message}";
                return health;
            }
        }

        private async Task<long> GetRedisMemoryUsageAsync()
        {
            if (_redis == null) return 0;

            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints()[0]);
                var info = await server.InfoAsync("memory");
                
                var memorySection = info.FirstOrDefault(s => s.Key == "Memory");
                if (memorySection != null && memorySection.Any())
                {
                    var usedMemory = memorySection.FirstOrDefault(kvp => kvp.Key == "used_memory");
                    if (usedMemory.Value != null && long.TryParse(usedMemory.Value, out var bytes))
                    {
                        return bytes;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis memory usage");
                return 0;
            }
        }

        private void OnDistributedStatisticsUpdated(object? sender, DistributedCacheStatisticsEventArgs e)
        {
            // Track that this instance is active
            _lastInstanceHeartbeat[e.InstanceId] = e.Timestamp;
            
            // Track operation
            var opsTracker = _performanceTrackers.GetOrAdd("operations", _ => new PerformanceTracker());
            opsTracker.RecordOperation();
            
            // Track region-specific operation
            var regionTracker = _performanceTrackers.GetOrAdd($"region:{e.Region}", _ => new PerformanceTracker());
            regionTracker.RecordOperation();
        }

        private async Task ProcessHealthResult(StatisticsHealthCheckResult result)
        {
            // Check for missing instances
            if (result.MissingInstances > 0)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.InstanceNotReporting,
                    Severity = AlertSeverity.Warning,
                    Message = $"{result.MissingInstances} instance(s) not reporting statistics",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = result.MissingInstances,
                    ThresholdValue = 0
                });
            }

            // Check aggregation latency
            if (result.AggregationLatencyMs > _alertThresholds.MaxAggregationLatency.TotalMilliseconds)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.HighAggregationLatency,
                    Severity = AlertSeverity.Warning,
                    Message = $"Aggregation latency ({result.AggregationLatencyMs:F1}ms) exceeds threshold",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = result.AggregationLatencyMs,
                    ThresholdValue = _alertThresholds.MaxAggregationLatency.TotalMilliseconds
                });
            }

            // Check Redis memory
            if (result.RedisMemoryUsageBytes > _alertThresholds.MaxRedisMemoryBytes)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.HighRedisMemory,
                    Severity = AlertSeverity.Warning,
                    Message = $"Redis memory usage ({result.RedisMemoryUsageBytes / (1024 * 1024)}MB) exceeds threshold",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = result.RedisMemoryUsageBytes,
                    ThresholdValue = _alertThresholds.MaxRedisMemoryBytes
                });
            }

            // Check minimum instances
            if (result.ActiveInstances < _alertThresholds.MinActiveInstances)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.LowActiveInstances,
                    Severity = AlertSeverity.Critical,
                    Message = $"Active instances ({result.ActiveInstances}) below minimum threshold",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = result.ActiveInstances,
                    ThresholdValue = _alertThresholds.MinActiveInstances
                });
            }

            // Check Redis connectivity
            if (!result.RedisConnected)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.RedisConnectionFailure,
                    Severity = AlertSeverity.Critical,
                    Message = "Redis connection failure detected",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = 0,
                    ThresholdValue = 1
                });
            }
        }

        private async Task ProcessAccuracyReport(StatisticsAccuracyReport report)
        {
            if (!report.IsAccurate)
            {
                foreach (var discrepancy in report.Discrepancies)
                {
                    await TriggerAlertAsync(new StatisticsMonitoringAlert
                    {
                        Type = StatisticsAlertType.StatisticsDrift,
                        Severity = AlertSeverity.Warning,
                        Message = $"Statistics drift detected in {discrepancy.Region}: {discrepancy.DriftPercentage:F1}%",
                        TriggeredAt = DateTime.UtcNow,
                        CurrentValue = discrepancy.DriftPercentage,
                        ThresholdValue = _alertThresholds.MaxDriftPercentage,
                        Context = new Dictionary<string, object>
                        {
                            ["Region"] = discrepancy.Region.ToString(),
                            ["Expected"] = discrepancy.ExpectedValue,
                            ["Actual"] = discrepancy.ActualValue,
                            ["AffectedInstances"] = string.Join(", ", discrepancy.AffectedInstances)
                        }
                    });
                }
            }
        }

        private async Task UpdatePerformanceMetricsAsync(CancellationToken cancellationToken)
        {
            var metrics = await GetPerformanceMetricsAsync(cancellationToken);

            // Check recording latency
            if (metrics.P99RecordingLatencyMs > _alertThresholds.MaxRecordingLatencyP99Ms)
            {
                await TriggerAlertAsync(new StatisticsMonitoringAlert
                {
                    Type = StatisticsAlertType.HighRecordingLatency,
                    Severity = AlertSeverity.Warning,
                    Message = $"Recording latency P99 ({metrics.P99RecordingLatencyMs:F1}ms) exceeds threshold",
                    TriggeredAt = DateTime.UtcNow,
                    CurrentValue = metrics.P99RecordingLatencyMs,
                    ThresholdValue = _alertThresholds.MaxRecordingLatencyP99Ms
                });
            }
        }

        private Task TriggerAlertAsync(StatisticsMonitoringAlert alert)
        {
            var alertKey = $"{alert.Type}:{alert.Context.GetValueOrDefault("Region", "global")}";
            
            // Check if this alert is already active
            if (_activeAlerts.TryGetValue(alertKey, out var existingAlert))
            {
                // Update existing alert if values have changed significantly
                if (Math.Abs(existingAlert.CurrentValue - alert.CurrentValue) > 0.01)
                {
                    existingAlert.CurrentValue = alert.CurrentValue;
                    existingAlert.TriggeredAt = alert.TriggeredAt;
                    
                    AlertTriggered?.Invoke(this, new StatisticsMonitoringAlertEventArgs
                    {
                        Alert = existingAlert,
                        IsNew = false
                    });
                }
            }
            else
            {
                // New alert
                _activeAlerts[alertKey] = alert;
                
                AlertTriggered?.Invoke(this, new StatisticsMonitoringAlertEventArgs
                {
                    Alert = alert,
                    IsNew = true
                });
                
                _logger.LogWarning("Statistics monitoring alert triggered: {Type} - {Message}", 
                    alert.Type, alert.Message);
            }
            
            return Task.CompletedTask;
        }

        private double CalculateDriftPercentage(long expected, long actual)
        {
            if (expected == 0) return actual > 0 ? 100.0 : 0.0;
            return Math.Abs((double)(actual - expected) / expected) * 100.0;
        }

        private void RecordAggregationLatency(double latencyMs)
        {
            var tracker = _performanceTrackers.GetOrAdd("aggregate:overall", _ => new PerformanceTracker());
            tracker.RecordLatency(latencyMs);
        }

        private double GetLatestAggregationLatency()
        {
            if (_performanceTrackers.TryGetValue("aggregate:overall", out var tracker))
            {
                var latencies = tracker.GetLatencies();
                return latencies.Any() ? latencies.Last() : 0;
            }
            return 0;
        }

        private DateTime? GetLastSuccessfulAggregation()
        {
            if (_performanceTrackers.TryGetValue("aggregate:overall", out var tracker))
            {
                return tracker.LastOperationTime;
            }
            return null;
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (!sortedValues.Any()) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count - 1))];
        }

        public override void Dispose()
        {
            _checkLock?.Dispose();
            base.Dispose();
        }

        /// <summary>
        /// Internal performance tracking helper.
        /// </summary>
        private class PerformanceTracker
        {
            private readonly Queue<double> _latencies = new();
            private readonly Queue<DateTime> _operationTimes = new();
            private readonly object _lock = new();
            private const int MaxSamples = 1000;
            private const int MaxTimeWindowSeconds = 60;

            public DateTime? LastOperationTime { get; private set; }

            public void RecordLatency(double latencyMs)
            {
                lock (_lock)
                {
                    _latencies.Enqueue(latencyMs);
                    if (_latencies.Count > MaxSamples)
                        _latencies.Dequeue();
                    
                    LastOperationTime = DateTime.UtcNow;
                }
            }

            public void RecordOperation()
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    _operationTimes.Enqueue(now);
                    
                    // Remove old operations outside time window
                    var cutoff = now.AddSeconds(-MaxTimeWindowSeconds);
                    while (_operationTimes.Count > 0 && _operationTimes.Peek() < cutoff)
                    {
                        _operationTimes.Dequeue();
                    }
                    
                    LastOperationTime = now;
                }
            }

            public List<double> GetLatencies()
            {
                lock (_lock)
                {
                    return _latencies.ToList();
                }
            }

            public double GetOperationsPerSecond()
            {
                lock (_lock)
                {
                    if (_operationTimes.Count < 2) return 0;
                    
                    var timeSpan = DateTime.UtcNow - _operationTimes.Peek();
                    return timeSpan.TotalSeconds > 0 ? _operationTimes.Count / timeSpan.TotalSeconds : 0;
                }
            }
        }
    }
}