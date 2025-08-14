using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based distributed cache statistics collector with atomic operations.
    /// </summary>
    public class RedisCacheStatisticsCollector : IDistributedCacheStatisticsCollector
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly ILogger<RedisCacheStatisticsCollector> _logger;
        private readonly string _instanceId;
        private readonly TimeSpan _instanceHeartbeatInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _instanceTimeout = TimeSpan.FromMinutes(2);
        private Timer? _heartbeatTimer;
        private readonly ConcurrentDictionary<CacheRegion, CacheAlertThresholds> _alertThresholds;
        private readonly ConcurrentDictionary<string, DateTime> _activeAlerts;
        
        private const string STATS_HASH_KEY = "conduit:cache:stats:{0}:{1}"; // {region}:{instanceId}
        private const string GLOBAL_STATS_HASH_KEY = "conduit:cache:stats:{0}:global"; // {region}
        private const string RESPONSE_TIMES_KEY = "conduit:cache:response:{0}:{1}:{2}"; // {region}:{operation}:{instanceId}
        private const string INSTANCE_SET_KEY = "conduit:cache:instances";
        private const string INSTANCE_HEARTBEAT_KEY = "conduit:cache:heartbeat:{0}"; // {instanceId}
        private const string ALERTS_HASH_KEY = "conduit:cache:alerts:{0}"; // {region}
        private const string STATS_UPDATE_CHANNEL = "conduit:cache:stats:updates";
        private const string ALERT_CHANNEL = "conduit:cache:alerts";

        public string InstanceId => _instanceId;

        public event EventHandler<CacheStatisticsUpdatedEventArgs>? StatisticsUpdated;
        public event EventHandler<CacheAlertEventArgs>? AlertTriggered;
        public event EventHandler<DistributedCacheStatisticsEventArgs>? DistributedStatisticsUpdated;

        public RedisCacheStatisticsCollector(
            IConnectionMultiplexer redis,
            ILogger<RedisCacheStatisticsCollector> logger,
            string? instanceId = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _db = _redis.GetDatabase();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _instanceId = instanceId ?? $"{Environment.MachineName}-{Guid.NewGuid():N}";
            _alertThresholds = new ConcurrentDictionary<CacheRegion, CacheAlertThresholds>();
            _activeAlerts = new ConcurrentDictionary<string, DateTime>();

            // Subscribe to distributed events
            var subscriber = _redis.GetSubscriber();
            subscriber.Subscribe(RedisChannel.Literal(STATS_UPDATE_CHANNEL), HandleDistributedStatsUpdate);
            subscriber.Subscribe(RedisChannel.Literal(ALERT_CHANNEL), HandleDistributedAlert);

            // Start heartbeat
            _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.Zero, _instanceHeartbeatInterval);
        }

        public async Task RecordOperationAsync(CacheOperation operation, CancellationToken cancellationToken = default)
        {
            try
            {
                var tasks = new List<Task>();
                var region = operation.Region;
                var statsKey = string.Format(STATS_HASH_KEY, region, _instanceId);
                var globalKey = string.Format(GLOBAL_STATS_HASH_KEY, region);

                // Update counters atomically
                switch (operation.OperationType)
                {
                    case CacheOperationType.Hit:
                        tasks.Add(_db.HashIncrementAsync(statsKey, "HitCount"));
                        tasks.Add(_db.HashIncrementAsync(globalKey, "HitCount"));
                        break;
                    case CacheOperationType.Miss:
                        tasks.Add(_db.HashIncrementAsync(statsKey, "MissCount"));
                        tasks.Add(_db.HashIncrementAsync(globalKey, "MissCount"));
                        break;
                    case CacheOperationType.Set:
                        tasks.Add(_db.HashIncrementAsync(statsKey, "SetCount"));
                        tasks.Add(_db.HashIncrementAsync(globalKey, "SetCount"));
                        break;
                    case CacheOperationType.Remove:
                        tasks.Add(_db.HashIncrementAsync(statsKey, "RemoveCount"));
                        tasks.Add(_db.HashIncrementAsync(globalKey, "RemoveCount"));
                        break;
                    case CacheOperationType.Eviction:
                        tasks.Add(_db.HashIncrementAsync(statsKey, "EvictionCount"));
                        tasks.Add(_db.HashIncrementAsync(globalKey, "EvictionCount"));
                        break;
                }

                if (!operation.Success)
                {
                    tasks.Add(_db.HashIncrementAsync(statsKey, "ErrorCount"));
                    tasks.Add(_db.HashIncrementAsync(globalKey, "ErrorCount"));
                }

                // Store response time in sorted set for percentile calculations
                if (operation.OperationType == CacheOperationType.Get || 
                    operation.OperationType == CacheOperationType.Set)
                {
                    var responseKey = string.Format(RESPONSE_TIMES_KEY, region, operation.OperationType, _instanceId);
                    var score = operation.Duration.TotalMilliseconds;
                    var member = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}:{Guid.NewGuid():N}";
                    
                    tasks.Add(_db.SortedSetAddAsync(responseKey, member, score));
                    
                    // Trim old entries (keep last 1000)
                    tasks.Add(_db.SortedSetRemoveRangeByRankAsync(responseKey, 0, -1001));
                }

                // Update data size if provided
                if (operation.DataSizeBytes.HasValue)
                {
                    tasks.Add(_db.HashIncrementAsync(statsKey, "TotalDataBytes", operation.DataSizeBytes.Value));
                    tasks.Add(_db.HashIncrementAsync(globalKey, "TotalDataBytes", operation.DataSizeBytes.Value));
                }

                await Task.WhenAll(tasks);

                // Check alerts
                await CheckAndTriggerAlertsAsync(region, cancellationToken);

                // Publish update event
                var updateMessage = JsonSerializer.Serialize(new
                {
                    InstanceId = _instanceId,
                    Region = region.ToString(),
                    Operation = operation.OperationType.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                
                await _db.PublishAsync(RedisChannel.Literal(STATS_UPDATE_CHANNEL), updateMessage);

                // Raise local event
                var stats = await GetStatisticsAsync(region, cancellationToken);
                StatisticsUpdated?.Invoke(this, new CacheStatisticsUpdatedEventArgs 
                { 
                    Region = region, 
                    Statistics = stats,
                    TriggeringOperation = operation
                });
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

        public async Task<CacheStatistics> GetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var statsKey = string.Format(STATS_HASH_KEY, region, _instanceId);
            var entries = await _db.HashGetAllAsync(statsKey);
            
            return ParseStatistics(region, entries);
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<CacheRegion, CacheStatistics>();
            
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                result[region] = await GetStatisticsAsync(region, cancellationToken);
            }
            
            return result;
        }

        public async Task<CacheStatistics> GetAggregatedStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var globalKey = string.Format(GLOBAL_STATS_HASH_KEY, region);
            var entries = await _db.HashGetAllAsync(globalKey);
            
            var stats = ParseStatistics(region, entries);
            
            // Calculate response time percentiles across all instances
            await CalculateAggregatedResponseTimes(stats, region);
            
            return stats;
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> GetAllAggregatedStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<CacheRegion, CacheStatistics>();
            
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                result[region] = await GetAggregatedStatisticsAsync(region, cancellationToken);
            }
            
            return result;
        }

        public async Task<Dictionary<string, CacheStatistics>> GetPerInstanceStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, CacheStatistics>();
            var instances = await GetActiveInstancesAsync(cancellationToken);
            
            foreach (var instance in instances)
            {
                var statsKey = string.Format(STATS_HASH_KEY, region, instance);
                var entries = await _db.HashGetAllAsync(statsKey);
                
                if (entries.Length > 0)
                {
                    result[instance] = ParseStatistics(region, entries);
                }
            }
            
            return result;
        }

        public async Task<IEnumerable<string>> GetActiveInstancesAsync(CancellationToken cancellationToken = default)
        {
            var members = await _db.SetMembersAsync(INSTANCE_SET_KEY);
            var activeInstances = new List<string>();
            var now = DateTimeOffset.UtcNow;
            
            foreach (var member in members)
            {
                var instanceId = member.ToString();
                var heartbeatKey = string.Format(INSTANCE_HEARTBEAT_KEY, instanceId);
                var lastHeartbeat = await _db.StringGetAsync(heartbeatKey);
                
                if (lastHeartbeat.HasValue && 
                    long.TryParse(lastHeartbeat, out var timestamp) &&
                    now.ToUnixTimeMilliseconds() - timestamp < _instanceTimeout.TotalMilliseconds)
                {
                    activeInstances.Add(instanceId);
                }
            }
            
            return activeInstances;
        }

        public async Task RegisterInstanceAsync(CancellationToken cancellationToken = default)
        {
            await _db.SetAddAsync(INSTANCE_SET_KEY, _instanceId);
            await SendHeartbeatAsync();
            _logger.LogInformation("Registered cache statistics collector instance: {InstanceId}", _instanceId);
        }

        public async Task UnregisterInstanceAsync(CancellationToken cancellationToken = default)
        {
            await _db.SetRemoveAsync(INSTANCE_SET_KEY, _instanceId);
            var heartbeatKey = string.Format(INSTANCE_HEARTBEAT_KEY, _instanceId);
            await _db.KeyDeleteAsync(heartbeatKey);
            _logger.LogInformation("Unregistered cache statistics collector instance: {InstanceId}", _instanceId);
        }

        public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            // This implementation is already synchronized via Redis
            await Task.CompletedTask;
        }

        public async Task<CacheStatistics> GetStatisticsForWindowAsync(CacheRegion region, TimeWindow window, CancellationToken cancellationToken = default)
        {
            // For simplicity, return current aggregated statistics
            // A full implementation would store time-series data and aggregate over the window
            return await GetAggregatedStatisticsAsync(region, cancellationToken);
        }

        public async Task<IEnumerable<TimeSeriesStatistics>> GetHistoricalStatisticsAsync(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            CancellationToken cancellationToken = default)
        {
            // Simplified implementation - would need time-series data storage
            var current = await GetAggregatedStatisticsAsync(region, cancellationToken);
            return new[]
            {
                new TimeSeriesStatistics
                {
                    Timestamp = DateTime.UtcNow,
                    Statistics = current
                }
            };
        }

        public async Task ResetStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            
            // Reset instance stats
            var statsKey = string.Format(STATS_HASH_KEY, region, _instanceId);
            tasks.Add(_db.KeyDeleteAsync(statsKey));
            
            // Reset response times
            foreach (var opType in new[] { CacheOperationType.Get, CacheOperationType.Set })
            {
                var responseKey = string.Format(RESPONSE_TIMES_KEY, region, opType, _instanceId);
                tasks.Add(_db.KeyDeleteAsync(responseKey));
            }
            
            await Task.WhenAll(tasks);
            _logger.LogInformation("Reset statistics for region {Region} on instance {InstanceId}", region, _instanceId);
        }

        public async Task ResetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                await ResetStatisticsAsync(region, cancellationToken);
            }
        }

        public async Task<string> ExportStatisticsAsync(string format, CancellationToken cancellationToken = default)
        {
            var allStats = await GetAllAggregatedStatisticsAsync(cancellationToken);
            
            switch (format.ToLowerInvariant())
            {
                case "prometheus":
                    return ExportPrometheus(allStats);
                case "json":
                    return JsonSerializer.Serialize(allStats, new JsonSerializerOptions { WriteIndented = true });
                default:
                    throw new NotSupportedException($"Export format '{format}' is not supported");
            }
        }

        public async Task ConfigureAlertsAsync(CacheRegion region, CacheAlertThresholds thresholds, CancellationToken cancellationToken = default)
        {
            _alertThresholds[region] = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
            
            // Store in Redis for persistence
            var alertsKey = string.Format(ALERTS_HASH_KEY, region);
            var json = JsonSerializer.Serialize(thresholds);
            await _db.HashSetAsync(alertsKey, "thresholds", json);
        }

        public Task<IEnumerable<CacheAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            var alerts = new List<CacheAlert>();
            
            foreach (var (alertKey, triggeredAt) in _activeAlerts)
            {
                if (DateTime.UtcNow - triggeredAt < TimeSpan.FromMinutes(5))
                {
                    var parts = alertKey.Split(':');
                    if (parts.Length >= 2 && 
                        Enum.TryParse<CacheRegion>(parts[0], out var region) &&
                        Enum.TryParse<CacheAlertType>(parts[1], out var alertType))
                    {
                        alerts.Add(new CacheAlert
                        {
                            Region = region,
                            AlertType = alertType,
                            Severity = AlertSeverity.Warning,
                            Message = $"Alert {alertType} for region {region}",
                            TriggeredAt = triggeredAt,
                            CurrentValue = 0, // Would need to fetch actual value
                            ThresholdValue = 0 // Would need to fetch threshold
                        });
                    }
                }
            }
            
            return Task.FromResult<IEnumerable<CacheAlert>>(alerts);
        }

        private CacheStatistics ParseStatistics(CacheRegion region, HashEntry[] entries)
        {
            var stats = new CacheStatistics { Region = region };
            var values = entries.ToDictionary(e => e.Name.ToString(), e => e.Value);

            stats.HitCount = GetLongValue(values, "HitCount");
            stats.MissCount = GetLongValue(values, "MissCount");
            stats.SetCount = GetLongValue(values, "SetCount");
            stats.RemoveCount = GetLongValue(values, "RemoveCount");
            stats.EvictionCount = GetLongValue(values, "EvictionCount");
            stats.ErrorCount = GetLongValue(values, "ErrorCount");
            stats.EntryCount = GetLongValue(values, "EntryCount");
            stats.MemoryUsageBytes = GetLongValue(values, "TotalDataBytes");

            return stats;
        }

        private long GetLongValue(Dictionary<string, RedisValue> values, string key)
        {
            return values.TryGetValue(key, out var value) && value.TryParse(out long result) ? result : 0;
        }

        private async Task CalculateAggregatedResponseTimes(CacheStatistics stats, CacheRegion region)
        {
            var instances = await GetActiveInstancesAsync();
            var getTimes = new List<double>();
            var setTimes = new List<double>();

            foreach (var instance in instances)
            {
                // Get response times
                var getKey = string.Format(RESPONSE_TIMES_KEY, region, CacheOperationType.Get, instance);
                var getEntries = await _db.SortedSetRangeByRankWithScoresAsync(getKey, 0, -1);
                getTimes.AddRange(getEntries.Select(e => e.Score));

                // Set response times
                var setKey = string.Format(RESPONSE_TIMES_KEY, region, CacheOperationType.Set, instance);
                var setEntries = await _db.SortedSetRangeByRankWithScoresAsync(setKey, 0, -1);
                setTimes.AddRange(setEntries.Select(e => e.Score));
            }

            if (getTimes.Count() > 0)
            {
                getTimes.Sort();
                stats.AverageGetTime = TimeSpan.FromMilliseconds(getTimes.Average());
                stats.P95GetTime = TimeSpan.FromMilliseconds(GetPercentile(getTimes, 0.95));
                stats.P99GetTime = TimeSpan.FromMilliseconds(GetPercentile(getTimes, 0.99));
                stats.MaxResponseTime = TimeSpan.FromMilliseconds(getTimes.Max());
            }

            if (setTimes.Count() > 0)
            {
                setTimes.Sort();
                stats.AverageSetTime = TimeSpan.FromMilliseconds(setTimes.Average());
            }
        }

        private double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count() == 0) return 0;
            
            var index = (int)Math.Ceiling(percentile * sortedValues.Count()) - 1;
            return sortedValues[Math.Max(0, Math.Min(index, sortedValues.Count() - 1))];
        }

        private async Task CheckAndTriggerAlertsAsync(CacheRegion region, CancellationToken cancellationToken)
        {
            if (!_alertThresholds.TryGetValue(region, out var thresholds))
                return;

            var stats = await GetStatisticsAsync(region, cancellationToken);
            
            // Check hit rate
            if (thresholds.MinHitRate.HasValue && stats.HitRate < thresholds.MinHitRate.Value)
            {
                await TriggerAlertAsync(region, CacheAlertType.LowHitRate, stats.HitRate, thresholds.MinHitRate.Value);
            }

            // Check response time
            if (thresholds.MaxResponseTime.HasValue && 
                stats.AverageGetTime > thresholds.MaxResponseTime.Value)
            {
                await TriggerAlertAsync(region, CacheAlertType.SlowResponseTime, 
                    stats.AverageGetTime.TotalMilliseconds, 
                    thresholds.MaxResponseTime.Value.TotalMilliseconds);
            }

            // Check error rate
            var errorRate = stats.TotalRequests > 0 ? (double)stats.ErrorCount / stats.TotalRequests * 100 : 0;
            if (thresholds.MaxErrorRate.HasValue && errorRate > thresholds.MaxErrorRate.Value)
            {
                await TriggerAlertAsync(region, CacheAlertType.HighErrorRate, errorRate, thresholds.MaxErrorRate.Value);
            }
        }

        private async Task TriggerAlertAsync(CacheRegion region, CacheAlertType alertType, double currentValue, double thresholdValue)
        {
            var alertKey = $"{region}:{alertType}";
            var now = DateTime.UtcNow;

            // Check if alert is already active (avoid spam)
            if (_activeAlerts.TryGetValue(alertKey, out var lastTriggered) && 
                now - lastTriggered < TimeSpan.FromMinutes(5))
            {
                return;
            }

            _activeAlerts[alertKey] = now;

            var alert = new CacheAlert
            {
                Region = region,
                AlertType = alertType,
                Severity = AlertSeverity.Warning,
                Message = $"{alertType} alert for cache region {region}: {currentValue:F2} (threshold: {thresholdValue:F2})",
                TriggeredAt = now,
                CurrentValue = currentValue,
                ThresholdValue = thresholdValue
            };

            // Publish alert
            var alertMessage = JsonSerializer.Serialize(alert);
            await _db.PublishAsync(RedisChannel.Literal(ALERT_CHANNEL), alertMessage);

            // Raise local event
            AlertTriggered?.Invoke(this, new CacheAlertEventArgs { Alert = alert, IsNew = true });
            
            _logger.LogWarning("Cache alert triggered: {AlertType} for region {Region}. Current: {CurrentValue}, Threshold: {ThresholdValue}",
                alertType, region, currentValue, thresholdValue);
        }

        private void SendHeartbeat(object? state)
        {
            _ = SendHeartbeatAsync();
        }

        private async Task SendHeartbeatAsync()
        {
            try
            {
                var heartbeatKey = string.Format(INSTANCE_HEARTBEAT_KEY, _instanceId);
                await _db.StringSetAsync(heartbeatKey, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 
                    expiry: _instanceTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat for instance {InstanceId}", _instanceId);
            }
        }

        private void HandleDistributedStatsUpdate(RedisChannel channel, RedisValue message)
        {
            try
            {
                var update = JsonSerializer.Deserialize<Dictionary<string, string>>(message.ToString());
                if (update != null && 
                    update.TryGetValue("InstanceId", out var instanceId) &&
                    update.TryGetValue("Region", out var regionStr) &&
                    Enum.TryParse<CacheRegion>(regionStr, out var region))
                {
                    // Don't process our own updates
                    if (instanceId != _instanceId)
                    {
                        Task.Run(async () =>
                        {
                            var stats = await GetAggregatedStatisticsAsync(region);
                            DistributedStatisticsUpdated?.Invoke(this, 
                                new DistributedCacheStatisticsEventArgs(instanceId, region, stats, DateTime.UtcNow));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling distributed stats update");
            }
        }

        private void HandleDistributedAlert(RedisChannel channel, RedisValue message)
        {
            try
            {
                var alert = JsonSerializer.Deserialize<CacheAlert>(message.ToString());
                if (alert != null)
                {
                    AlertTriggered?.Invoke(this, new CacheAlertEventArgs { Alert = alert, IsNew = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling distributed alert");
            }
        }

        private string ExportPrometheus(Dictionary<CacheRegion, CacheStatistics> allStats)
        {
            var lines = new List<string>
            {
                "# HELP cache_hits_total Total number of cache hits",
                "# TYPE cache_hits_total counter",
                "# HELP cache_misses_total Total number of cache misses",
                "# TYPE cache_misses_total counter",
                "# HELP cache_hit_rate Cache hit rate percentage",
                "# TYPE cache_hit_rate gauge",
                "# HELP cache_response_time_milliseconds Cache response time in milliseconds",
                "# TYPE cache_response_time_milliseconds summary"
            };

            foreach (var (region, stats) in allStats)
            {
                var regionLabel = $"region=\"{region}\"";
                lines.Add($"cache_hits_total{{{regionLabel}}} {stats.HitCount}");
                lines.Add($"cache_misses_total{{{regionLabel}}} {stats.MissCount}");
                lines.Add($"cache_hit_rate{{{regionLabel}}} {stats.HitRate:F2}");
                lines.Add($"cache_response_time_milliseconds{{{regionLabel},quantile=\"0.5\"}} {stats.AverageGetTime.TotalMilliseconds:F2}");
                lines.Add($"cache_response_time_milliseconds{{{regionLabel},quantile=\"0.95\"}} {stats.P95GetTime.TotalMilliseconds:F2}");
                lines.Add($"cache_response_time_milliseconds{{{regionLabel},quantile=\"0.99\"}} {stats.P99GetTime.TotalMilliseconds:F2}");
            }

            return string.Join("\n", lines);
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            UnregisterInstanceAsync().GetAwaiter().GetResult();
        }
    }
}