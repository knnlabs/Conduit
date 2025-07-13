using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of cache statistics storage.
    /// </summary>
    public class RedisCacheStatisticsStore : ICacheStatisticsStore
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<RedisCacheStatisticsStore> _logger;
        private const string STATS_KEY_PREFIX = "cache:stats:";
        private const string TIMESERIES_KEY_PREFIX = "cache:stats:ts:";
        private const string SNAPSHOT_KEY_PREFIX = "cache:stats:snapshot:";

        public RedisCacheStatisticsStore(
            IDistributedCache cache,
            ILogger<RedisCacheStatisticsStore> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveStatisticsAsync(
            Dictionary<CacheRegion, CacheStatistics> statistics, 
            CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>();
            var timestamp = DateTime.UtcNow;

            foreach (var (region, stats) in statistics)
            {
                // Save current statistics
                var currentKey = $"{STATS_KEY_PREFIX}{region}:current";
                var json = JsonSerializer.Serialize(stats);
                
                tasks.Add(_cache.SetStringAsync(
                    currentKey, 
                    json, 
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
                    }, 
                    cancellationToken));

                // Save time-series data point
                var tsKey = $"{TIMESERIES_KEY_PREFIX}{region}:{timestamp:yyyyMMddHHmm}";
                tasks.Add(_cache.SetStringAsync(
                    tsKey,
                    json,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30)
                    },
                    cancellationToken));

                // Save hourly snapshot
                if (timestamp.Minute == 0)
                {
                    var snapshotKey = $"{SNAPSHOT_KEY_PREFIX}{region}:{timestamp:yyyyMMddHH}";
                    tasks.Add(_cache.SetStringAsync(
                        snapshotKey,
                        json,
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(90)
                        },
                        cancellationToken));
                }
            }

            try
            {
                await Task.WhenAll(tasks);
                _logger.LogDebug("Saved statistics for {RegionCount} regions to Redis", statistics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving statistics to Redis");
                throw;
            }
        }

        public async Task<Dictionary<CacheRegion, CacheStatistics>> LoadAllStatisticsAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<CacheRegion, CacheStatistics>();

            foreach (CacheRegion region in Enum.GetValues<CacheRegion>())
            {
                try
                {
                    var key = $"{STATS_KEY_PREFIX}{region}:current";
                    var json = await _cache.GetStringAsync(key, cancellationToken);

                    if (!string.IsNullOrEmpty(json))
                    {
                        var stats = JsonSerializer.Deserialize<CacheStatistics>(json);
                        if (stats != null)
                        {
                            result[region] = stats;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load statistics for region {Region}", region);
                }
            }

            _logger.LogDebug("Loaded statistics for {RegionCount} regions from Redis", result.Count);
            return result;
        }

        public async Task<CacheStatistics> GetStatisticsForWindowAsync(
            CacheRegion region, 
            DateTime startTime, 
            DateTime endTime, 
            CancellationToken cancellationToken = default)
        {
            var aggregated = new CacheStatistics
            {
                Region = region,
                StartTime = startTime,
                LastUpdateTime = endTime
            };

            // Get all time-series keys in the range
            var current = startTime;
            var tasks = new List<Task<string?>>();

            while (current <= endTime)
            {
                var tsKey = $"{TIMESERIES_KEY_PREFIX}{region}:{current:yyyyMMddHHmm}";
                tasks.Add(_cache.GetStringAsync(tsKey, cancellationToken));
                current = current.AddMinutes(1);
            }

            try
            {
                var results = await Task.WhenAll(tasks);
                var validStats = results
                    .Where(json => !string.IsNullOrEmpty(json))
                    .Select(json => JsonSerializer.Deserialize<CacheStatistics>(json!))
                    .Where(stats => stats != null)
                    .ToList();

                if (validStats.Count > 0)
                {
                    // Aggregate statistics
                    aggregated.HitCount = validStats.Sum(s => s!.HitCount);
                    aggregated.MissCount = validStats.Sum(s => s!.MissCount);
                    aggregated.SetCount = validStats.Sum(s => s!.SetCount);
                    aggregated.RemoveCount = validStats.Sum(s => s!.RemoveCount);
                    aggregated.EvictionCount = validStats.Sum(s => s!.EvictionCount);
                    aggregated.ErrorCount = validStats.Sum(s => s!.ErrorCount);

                    // Average response times
                    var avgGetTimes = validStats
                        .Where(s => s!.AverageGetTime > TimeSpan.Zero)
                        .Select(s => s!.AverageGetTime.TotalMilliseconds)
                        .ToList();

                    if (avgGetTimes.Count > 0)
                    {
                        aggregated.AverageGetTime = TimeSpan.FromMilliseconds(avgGetTimes.Average());
                    }

                    // Take the latest values for current state
                    var latest = validStats.OrderByDescending(s => s!.LastUpdateTime).First();
                    aggregated.EntryCount = latest!.EntryCount;
                    aggregated.MemoryUsageBytes = latest.MemoryUsageBytes;
                }

                _logger.LogDebug("Aggregated {DataPoints} data points for region {Region} window {StartTime} to {EndTime}",
                    validStats.Count, region, startTime, endTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for window");
            }

            return aggregated;
        }

        public async Task<IEnumerable<TimeSeriesStatistics>> GetTimeSeriesStatisticsAsync(
            CacheRegion region, 
            DateTime startTime, 
            DateTime endTime, 
            TimeSpan interval, 
            CancellationToken cancellationToken = default)
        {
            var result = new List<TimeSeriesStatistics>();

            // Determine appropriate data source based on interval
            if (interval >= TimeSpan.FromHours(1))
            {
                // Use hourly snapshots
                await LoadFromSnapshots(region, startTime, endTime, interval, result, cancellationToken);
            }
            else
            {
                // Use minute-level data
                await LoadFromTimeSeries(region, startTime, endTime, interval, result, cancellationToken);
            }

            _logger.LogDebug("Retrieved {DataPoints} time-series data points for region {Region}",
                result.Count, region);

            return result;
        }

        private async Task LoadFromSnapshots(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            List<TimeSeriesStatistics> result,
            CancellationToken cancellationToken)
        {
            var current = startTime;
            
            while (current <= endTime)
            {
                var snapshotKey = $"{SNAPSHOT_KEY_PREFIX}{region}:{current:yyyyMMddHH}";
                
                try
                {
                    var json = await _cache.GetStringAsync(snapshotKey, cancellationToken);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var stats = JsonSerializer.Deserialize<CacheStatistics>(json);
                        if (stats != null)
                        {
                            result.Add(new TimeSeriesStatistics
                            {
                                Timestamp = current,
                                Statistics = stats,
                                Interval = interval
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load snapshot for {Timestamp}", current);
                }

                current = current.Add(interval);
            }
        }

        private async Task LoadFromTimeSeries(
            CacheRegion region,
            DateTime startTime,
            DateTime endTime,
            TimeSpan interval,
            List<TimeSeriesStatistics> result,
            CancellationToken cancellationToken)
        {
            var current = startTime;

            while (current <= endTime)
            {
                var windowEnd = current.Add(interval);
                var windowStats = await GetStatisticsForWindowAsync(region, current, windowEnd, cancellationToken);

                if (windowStats.TotalRequests > 0 || windowStats.SetCount > 0)
                {
                    result.Add(new TimeSeriesStatistics
                    {
                        Timestamp = current,
                        Statistics = windowStats,
                        Interval = interval
                    });
                }

                current = windowEnd;
            }
        }
    }
}