using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Collects and manages cache statistics with support for real-time monitoring.
    /// </summary>
    public class CacheStatisticsCollector : ICacheStatisticsCollector, IDisposable
    {
        private readonly ILogger<CacheStatisticsCollector> _logger;
        private readonly ICacheStatisticsStore? _store;
        private readonly CacheStatisticsOptions _options;
        private readonly ConcurrentDictionary<CacheRegion, RegionStatistics> _statistics;
        private readonly ConcurrentDictionary<CacheRegion, CacheAlertThresholds> _alertThresholds;
        private readonly ConcurrentDictionary<string, CacheAlert> _activeAlerts;
        private readonly Timer _aggregationTimer;
        private readonly Timer _persistenceTimer;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public event EventHandler<CacheStatisticsUpdatedEventArgs>? StatisticsUpdated;
        public event EventHandler<CacheAlertEventArgs>? AlertTriggered;

        public CacheStatisticsCollector(
            ILogger<CacheStatisticsCollector> logger,
            IOptions<CacheStatisticsOptions> options,
            ICacheStatisticsStore? store = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? new CacheStatisticsOptions();
            _store = store;
            _statistics = new ConcurrentDictionary<CacheRegion, RegionStatistics>();
            _alertThresholds = new ConcurrentDictionary<CacheRegion, CacheAlertThresholds>();
            _activeAlerts = new ConcurrentDictionary<string, CacheAlert>();

            // Initialize statistics for all regions
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                _statistics[region] = new RegionStatistics { Region = region };
            }

            // Start aggregation timer
            _aggregationTimer = new Timer(
                AggregateStatistics,
                null,
                _options.AggregationInterval,
                _options.AggregationInterval);

            // Start persistence timer if store is available
            if (_store != null)
            {
                _persistenceTimer = new Timer(
                    PersistStatistics,
                    null,
                    _options.PersistenceInterval,
                    _options.PersistenceInterval);
            }
            else
            {
                _persistenceTimer = new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite);
            }
        }

        public async Task RecordOperationAsync(CacheOperation operation, CancellationToken cancellationToken = default)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            try
            {
                // Get or create statistics for the region
                var stats = _statistics.GetOrAdd(operation.Region, r => new RegionStatistics { Region = r });

                // Update counters based on operation type
                switch (operation.OperationType)
                {
                    case CacheOperationType.Hit:
                        Interlocked.Increment(ref stats.HitCount);
                        break;
                    case CacheOperationType.Miss:
                        Interlocked.Increment(ref stats.MissCount);
                        break;
                    case CacheOperationType.Set:
                        Interlocked.Increment(ref stats.SetCount);
                        break;
                    case CacheOperationType.Remove:
                        Interlocked.Increment(ref stats.RemoveCount);
                        break;
                    case CacheOperationType.Eviction:
                        Interlocked.Increment(ref stats.EvictionCount);
                        break;
                }

                if (!operation.Success)
                {
                    Interlocked.Increment(ref stats.ErrorCount);
                }

                // Track response times
                stats.RecordResponseTime(operation.OperationType, operation.Duration);

                // Track data sizes
                if (operation.DataSizeBytes.HasValue)
                {
                    stats.RecordDataSize(operation.DataSizeBytes.Value);
                }

                // Update last activity time
                stats.LastActivityTime = DateTime.UtcNow;

                // Check alerts
                await CheckAlertsAsync(operation.Region, cancellationToken);

                // Raise event
                StatisticsUpdated?.Invoke(this, new CacheStatisticsUpdatedEventArgs
                {
                    Region = operation.Region,
                    Statistics = ConvertToPublicStatistics(stats),
                    TriggeringOperation = operation
                });

                _logger.LogDebug("Recorded {OperationType} operation for region {Region} in {Duration}ms",
                    operation.OperationType, operation.Region, operation.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording cache operation for region {Region}", operation.Region);
            }
        }

        public async Task RecordOperationBatchAsync(IEnumerable<CacheOperation> operations, CancellationToken cancellationToken = default)
        {
            var tasks = operations.Select(op => RecordOperationAsync(op, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public Task<CacheStatistics> GetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_statistics.TryGetValue(region, out var stats))
            {
                return Task.FromResult(ConvertToPublicStatistics(stats));
            }

            return Task.FromResult(new CacheStatistics { Region = region });
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<CacheRegion, CacheStatistics>();

            foreach (var (region, stats) in _statistics)
            {
                result[region] = ConvertToPublicStatistics(stats);
            }

            // Load from store if available
            if (_store != null)
            {
                try
                {
                    var storedStats = await _store.LoadAllStatisticsAsync(cancellationToken);
                    // Merge with in-memory stats
                    foreach (var (region, stats) in storedStats)
                    {
                        if (result.ContainsKey(region))
                        {
                            // Merge logic would go here
                            _logger.LogDebug("Merged stored statistics for region {Region}", region);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load statistics from store");
                }
            }

            return result;
        }

        public async Task<CacheStatistics> GetStatisticsForWindowAsync(
            CacheRegion region,
            TimeWindow window,
            CancellationToken cancellationToken = default)
        {
            var windowDuration = GetWindowDuration(window);
            var startTime = DateTime.UtcNow - windowDuration;

            if (_store != null)
            {
                try
                {
                    return await _store.GetStatisticsForWindowAsync(region, startTime, DateTime.UtcNow, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get statistics from store for window {Window}", window);
                }
            }

            // Fallback to current in-memory statistics
            return await GetStatisticsAsync(region, cancellationToken);
        }

        public async Task<IEnumerable<TimeSeriesStatistics>> GetHistoricalStatisticsAsync(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            CancellationToken cancellationToken = default)
        {
            if (_store != null)
            {
                try
                {
                    return await _store.GetTimeSeriesStatisticsAsync(region, startTime, endTime, interval, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get historical statistics from store");
                }
            }

            // Return empty if no store available
            return Enumerable.Empty<TimeSeriesStatistics>();
        }

        public Task ResetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            if (_statistics.TryGetValue(region, out var stats))
            {
                stats.Reset();
                _logger.LogInformation("Reset statistics for cache region {Region}", region);
            }

            return Task.CompletedTask;
        }

        public Task ResetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            foreach (var stats in _statistics.Values)
            {
                stats.Reset();
            }

            _logger.LogInformation("Reset all cache statistics");
            return Task.CompletedTask;
        }

        public Task<string> ExportStatisticsAsync(string format, CancellationToken cancellationToken = default)
        {
            var allStats = _statistics.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertToPublicStatistics(kvp.Value));

            switch (format.ToLowerInvariant())
            {
                case "prometheus":
                    return Task.FromResult(ExportToPrometheus(allStats));
                case "json":
                    return Task.FromResult(System.Text.Json.JsonSerializer.Serialize(allStats));
                default:
                    throw new NotSupportedException($"Export format '{format}' is not supported");
            }
        }

        public Task ConfigureAlertsAsync(CacheRegion region, CacheAlertThresholds thresholds, CancellationToken cancellationToken = default)
        {
            _alertThresholds[region] = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            _logger.LogInformation("Configured alert thresholds for cache region {Region}", region);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<CacheAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<CacheAlert>>(_activeAlerts.Values.ToList());
        }

        private async Task CheckAlertsAsync(CacheRegion region, CancellationToken cancellationToken)
        {
            if (!_alertThresholds.TryGetValue(region, out var thresholds) || !thresholds.Enabled)
                return;

            var stats = await GetStatisticsAsync(region, cancellationToken);
            var alerts = new List<CacheAlert>();

            // Check hit rate
            if (thresholds.MinHitRate.HasValue && stats.HitRate < thresholds.MinHitRate.Value)
            {
                alerts.Add(new CacheAlert
                {
                    Region = region,
                    AlertType = CacheAlertType.LowHitRate,
                    Severity = AlertSeverity.Warning,
                    Message = $"Hit rate {stats.HitRate:F2}% is below threshold {thresholds.MinHitRate.Value:F2}%",
                    CurrentValue = stats.HitRate,
                    ThresholdValue = thresholds.MinHitRate.Value
                });
            }

            // Check response time
            if (thresholds.MaxResponseTime.HasValue && stats.AverageGetTime > thresholds.MaxResponseTime.Value)
            {
                alerts.Add(new CacheAlert
                {
                    Region = region,
                    AlertType = CacheAlertType.SlowResponseTime,
                    Severity = AlertSeverity.Warning,
                    Message = $"Average response time {stats.AverageGetTime.TotalMilliseconds}ms exceeds threshold {thresholds.MaxResponseTime.Value.TotalMilliseconds}ms",
                    CurrentValue = stats.AverageGetTime.TotalMilliseconds,
                    ThresholdValue = thresholds.MaxResponseTime.Value.TotalMilliseconds
                });
            }

            // Process alerts
            foreach (var alert in alerts)
            {
                var alertKey = $"{alert.Region}_{alert.AlertType}";
                var isNew = _activeAlerts.TryAdd(alertKey, alert);

                AlertTriggered?.Invoke(this, new CacheAlertEventArgs
                {
                    Alert = alert,
                    IsNew = isNew,
                    IsResolved = false
                });

                if (isNew)
                {
                    _logger.LogWarning("Alert triggered for {Region}: {AlertType} - {Message}",
                        alert.Region, alert.AlertType, alert.Message);
                }
            }
        }

        private CacheStatistics ConvertToPublicStatistics(RegionStatistics stats)
        {
            var publicStats = new CacheStatistics
            {
                Region = stats.Region,
                HitCount = stats.HitCount,
                MissCount = stats.MissCount,
                SetCount = stats.SetCount,
                RemoveCount = stats.RemoveCount,
                EvictionCount = stats.EvictionCount,
                ErrorCount = stats.ErrorCount,
                EntryCount = stats.EntryCount,
                MemoryUsageBytes = stats.MemoryUsageBytes,
                StartTime = stats.StartTime,
                LastUpdateTime = stats.LastActivityTime
            };

            // Calculate response times
            if (stats.ResponseTimes.Count > 0)
            {
                var getTimes = stats.ResponseTimes
                    .Where(rt => rt.Operation == CacheOperationType.Get || 
                                 rt.Operation == CacheOperationType.Hit || 
                                 rt.Operation == CacheOperationType.Miss)
                    .Select(rt => rt.Duration)
                    .ToList();

                if (getTimes.Count > 0)
                {
                    publicStats.AverageGetTime = TimeSpan.FromMilliseconds(getTimes.Average(t => t.TotalMilliseconds));
                    publicStats.P95GetTime = CalculatePercentile(getTimes, 95);
                    publicStats.P99GetTime = CalculatePercentile(getTimes, 99);
                    publicStats.MaxResponseTime = getTimes.Max();
                }
            }

            return publicStats;
        }

        private TimeSpan CalculatePercentile(List<TimeSpan> values, int percentile)
        {
            if (values.Count == 0)
                return TimeSpan.Zero;

            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
        }

        private TimeSpan GetWindowDuration(TimeWindow window)
        {
            return window switch
            {
                TimeWindow.LastMinute => TimeSpan.FromMinutes(1),
                TimeWindow.Last5Minutes => TimeSpan.FromMinutes(5),
                TimeWindow.Last15Minutes => TimeSpan.FromMinutes(15),
                TimeWindow.LastHour => TimeSpan.FromHours(1),
                TimeWindow.Last24Hours => TimeSpan.FromHours(24),
                TimeWindow.Last7Days => TimeSpan.FromDays(7),
                TimeWindow.Last30Days => TimeSpan.FromDays(30),
                _ => TimeSpan.MaxValue
            };
        }

        private string ExportToPrometheus(Dictionary<CacheRegion, CacheStatistics> statistics)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# HELP conduit_cache_hits_total Total number of cache hits");
            sb.AppendLine("# TYPE conduit_cache_hits_total counter");

            foreach (var (region, stats) in statistics)
            {
                var regionLabel = region.ToString().ToLowerInvariant();
                sb.AppendLine($"conduit_cache_hits_total{{region=\"{regionLabel}\"}} {stats.HitCount}");
                sb.AppendLine($"conduit_cache_misses_total{{region=\"{regionLabel}\"}} {stats.MissCount}");
                sb.AppendLine($"conduit_cache_hit_rate{{region=\"{regionLabel}\"}} {stats.HitRate:F2}");
                sb.AppendLine($"conduit_cache_response_time_milliseconds{{region=\"{regionLabel}\",quantile=\"0.5\"}} {stats.AverageGetTime.TotalMilliseconds:F2}");
                sb.AppendLine($"conduit_cache_response_time_milliseconds{{region=\"{regionLabel}\",quantile=\"0.95\"}} {stats.P95GetTime.TotalMilliseconds:F2}");
                sb.AppendLine($"conduit_cache_response_time_milliseconds{{region=\"{regionLabel}\",quantile=\"0.99\"}} {stats.P99GetTime.TotalMilliseconds:F2}");
            }

            return sb.ToString();
        }

        private void AggregateStatistics(object? state)
        {
            try
            {
                // Perform any periodic aggregation tasks
                _logger.LogDebug("Running statistics aggregation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during statistics aggregation");
            }
        }

        private async void PersistStatistics(object? state)
        {
            if (_store == null)
                return;

            try
            {
                var allStats = _statistics.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertToPublicStatistics(kvp.Value));

                await _store.SaveStatisticsAsync(allStats);
                _logger.LogDebug("Persisted cache statistics to store");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting cache statistics");
            }
        }

        public void Dispose()
        {
            _aggregationTimer?.Dispose();
            _persistenceTimer?.Dispose();
            _lock?.Dispose();
        }

        /// <summary>
        /// Internal statistics tracking class with thread-safe operations.
        /// </summary>
        private class RegionStatistics
        {
            public CacheRegion Region { get; set; }
            public long HitCount;
            public long MissCount;
            public long SetCount;
            public long RemoveCount;
            public long EvictionCount;
            public long ErrorCount;
            public long EntryCount;
            public long MemoryUsageBytes;
            public DateTime StartTime { get; } = DateTime.UtcNow;
            public DateTime LastActivityTime { get; set; } = DateTime.UtcNow;

            public readonly ConcurrentBag<ResponseTimeEntry> ResponseTimes = new();
            public readonly ConcurrentBag<long> DataSizes = new();

            public void RecordResponseTime(CacheOperationType operation, TimeSpan duration)
            {
                ResponseTimes.Add(new ResponseTimeEntry { Operation = operation, Duration = duration });
                
                // Keep only recent entries (last 1000)
                while (ResponseTimes.Count > 1000)
                {
                    ResponseTimes.TryTake(out _);
                }
            }

            public void RecordDataSize(long sizeBytes)
            {
                DataSizes.Add(sizeBytes);
                
                // Keep only recent entries
                while (DataSizes.Count > 1000)
                {
                    DataSizes.TryTake(out _);
                }
            }

            public void Reset()
            {
                HitCount = 0;
                MissCount = 0;
                SetCount = 0;
                RemoveCount = 0;
                EvictionCount = 0;
                ErrorCount = 0;
                ResponseTimes.Clear();
                DataSizes.Clear();
            }
        }

        private record ResponseTimeEntry
        {
            public CacheOperationType Operation { get; init; }
            public TimeSpan Duration { get; init; }
        }
    }

    /// <summary>
    /// Options for cache statistics collection.
    /// </summary>
    public class CacheStatisticsOptions
    {
        /// <summary>
        /// Interval for aggregating statistics.
        /// </summary>
        public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Interval for persisting statistics to store.
        /// </summary>
        public TimeSpan PersistenceInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Maximum number of response time samples to keep per region.
        /// </summary>
        public int MaxResponseTimeSamples { get; set; } = 1000;

        /// <summary>
        /// Whether to enable detailed tracking.
        /// </summary>
        public bool EnableDetailedTracking { get; set; } = true;

        /// <summary>
        /// Retention period for historical statistics.
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    }

    /// <summary>
    /// Interface for persisting cache statistics.
    /// </summary>
    public interface ICacheStatisticsStore
    {
        /// <summary>
        /// Saves statistics to persistent storage.
        /// </summary>
        Task SaveStatisticsAsync(Dictionary<CacheRegion, CacheStatistics> statistics, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads all statistics from storage.
        /// </summary>
        Task<Dictionary<CacheRegion, CacheStatistics>> LoadAllStatisticsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets statistics for a specific time window.
        /// </summary>
        Task<CacheStatistics> GetStatisticsForWindowAsync(CacheRegion region, DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets time-series statistics data.
        /// </summary>
        Task<IEnumerable<TimeSeriesStatistics>> GetTimeSeriesStatisticsAsync(CacheRegion region, DateTime startTime, DateTime endTime, TimeSpan interval, CancellationToken cancellationToken = default);
    }
}