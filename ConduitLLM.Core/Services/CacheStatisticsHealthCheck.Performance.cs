using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache statistics health check - Performance tracking functionality
    /// </summary>
    public partial class CacheStatisticsHealthCheck
    {
        private async Task<StatisticsPerformanceMetrics> GetPerformanceMetricsAsyncImpl(CancellationToken cancellationToken = default)
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

                if (recordingLatencies.Count() > 0)
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

                if (aggregationLatencies.Count() > 0)
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

        private Task ConfigureAlertingAsyncImpl(StatisticsAlertThresholds thresholds)
        {
            _alertThresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            _logger.LogInformation("Statistics alert thresholds updated");
            return Task.CompletedTask;
        }

        private Task<IEnumerable<StatisticsMonitoringAlert>> GetActiveAlertsAsyncImpl(CancellationToken cancellationToken = default)
        {
            var activeAlerts = _activeAlerts.Values
                .Where(a => !a.IsAcknowledged)
                .OrderByDescending(a => a.TriggeredAt)
                .ToList();

            return Task.FromResult<IEnumerable<StatisticsMonitoringAlert>>(activeAlerts);
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
                return latencies.Count() > 0 ? latencies.Last() : 0;
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
            if (sortedValues.Count() == 0) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count()) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count() - 1))];
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
                    if (_latencies.Count() > MaxSamples)
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
                    while (_operationTimes.Count() > 0 && _operationTimes.Peek() < cutoff)
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
                    if (_operationTimes.Count() < 2) return 0;
                    
                    var timeSpan = DateTime.UtcNow - _operationTimes.Peek();
                    return timeSpan.TotalSeconds > 0 ? _operationTimes.Count() / timeSpan.TotalSeconds : 0;
                }
            }
        }
    }
}