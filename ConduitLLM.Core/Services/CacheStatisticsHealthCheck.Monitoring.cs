using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Interfaces;
using ComponentHealth = ConduitLLM.Core.Interfaces.ComponentHealth;
using HealthStatus = ConduitLLM.Core.Interfaces.HealthStatus;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache statistics health check - Monitoring and alert functionality
    /// </summary>
    public partial class CacheStatisticsHealthCheck
    {
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
                if (memorySection != null && memorySection.Count() > 0)
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
    }
}