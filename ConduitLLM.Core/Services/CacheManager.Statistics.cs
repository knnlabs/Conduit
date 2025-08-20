using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache manager implementation - Statistics and Health functionality
    /// </summary>
    public partial class CacheManager
    {
        public async Task<CacheRegionStatistics> GetRegionStatisticsAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            // If we have a statistics collector, prefer its data
            if (_statisticsCollector != null)
            {
                try
                {
                    var collectorStats = await _statisticsCollector.GetStatisticsAsync(region, cancellationToken);
                    return new CacheRegionStatistics
                    {
                        Region = region,
                        HitCount = collectorStats.HitCount,
                        MissCount = collectorStats.MissCount,
                        SetCount = collectorStats.SetCount,
                        EvictionCount = collectorStats.EvictionCount,
                        EntryCount = collectorStats.EntryCount,
                        TotalSizeBytes = collectorStats.MemoryUsageBytes,
                        AverageGetTime = collectorStats.AverageGetTime,
                        AverageSetTime = collectorStats.AverageSetTime,
                        LastResetTime = collectorStats.StartTime
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get statistics from collector for region {Region}", region);
                }
            }

            // Fallback to internal statistics - use GetOrAdd to avoid overwriting accumulated data
            return _statistics.GetOrAdd(region, r => new CacheRegionStatistics 
            { 
                Region = r, 
                LastResetTime = DateTime.UtcNow 
            });
        }

        public async Task<Dictionary<CacheRegion, CacheRegionStatistics>> GetAllStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<CacheRegion, CacheRegionStatistics>();
            
            foreach (var region in Enum.GetValues<CacheRegion>())
            {
                result[region] = await GetRegionStatisticsAsync(region, cancellationToken);
            }

            return result;
        }

        public async Task<CacheHealthStatus> GetHealthStatusAsync()
        {
            var status = new CacheHealthStatus
            {
                CheckedAt = DateTime.UtcNow,
                IsHealthy = true
            };

            // Check memory cache
            try
            {
                var testKey = $"health_check_{Guid.NewGuid()}";
                var stopwatch = Stopwatch.StartNew();
                _memoryCache.Set(testKey, "test", new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
                });
                _memoryCache.Remove(testKey);
                stopwatch.Stop();
                status.MemoryCacheResponseTime = stopwatch.Elapsed;
                status.ComponentStatus["MemoryCache"] = true;
            }
            catch (Exception ex)
            {
                status.ComponentStatus["MemoryCache"] = false;
                status.Issues["MemoryCache"] = ex.Message;
                status.IsHealthy = false;
            }

            // Check distributed cache
            if (_useDistributedCache && _distributedCache != null)
            {
                try
                {
                    var testKey = $"health_check_{Guid.NewGuid()}";
                    var stopwatch = Stopwatch.StartNew();
                    await _distributedCache.SetStringAsync(testKey, "test", new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1)
                    });
                    await _distributedCache.RemoveAsync(testKey);
                    stopwatch.Stop();
                    status.DistributedCacheResponseTime = stopwatch.Elapsed;
                    status.ComponentStatus["DistributedCache"] = true;
                }
                catch (Exception ex)
                {
                    status.ComponentStatus["DistributedCache"] = false;
                    status.Issues["DistributedCache"] = ex.Message;
                    status.IsHealthy = false;
                }
            }

            return status;
        }

        private async Task UpdateStatisticsAsync(CacheRegion region, string operation, TimeSpan duration, bool success)
        {
            await _statisticsLock.WaitAsync();
            try
            {
                if (!_statistics.TryGetValue(region, out var stats))
                {
                    stats = new CacheRegionStatistics { Region = region, LastResetTime = DateTime.UtcNow };
                    _statistics[region] = stats;
                }
                

                // Update operation counts
                var opKey = success ? operation : $"{operation}_Failed";
                stats.OperationCounts.TryGetValue(opKey, out var count);
                stats.OperationCounts[opKey] = count + 1;

                // Update operation timings
                if (stats.OperationTimings.TryGetValue(operation, out var totalTime))
                {
                    stats.OperationTimings[operation] = totalTime + duration;
                }
                else
                {
                    stats.OperationTimings[operation] = duration;
                }

                // Update specific counters
                switch (operation)
                {
                    case "Hit":
                        stats.HitCount++;
                        break;
                    case "Miss":
                        stats.MissCount++;
                        break;
                    case "Set":
                        stats.SetCount++;
                        break;
                    case "Eviction":
                        stats.EvictionCount++;
                        break;
                }

                // Calculate averages
                if (stats.HitCount + stats.MissCount > 0)
                {
                    var totalGetOps = stats.OperationCounts.GetValueOrDefault("Hit", 0) + stats.OperationCounts.GetValueOrDefault("Miss", 0);
                    if (totalGetOps > 0 && stats.OperationTimings.TryGetValue("Hit", out var hitTime) && stats.OperationTimings.TryGetValue("Miss", out var missTime))
                    {
                        stats.AverageGetTime = TimeSpan.FromMilliseconds((hitTime.TotalMilliseconds + missTime.TotalMilliseconds) / totalGetOps);
                    }
                }

                if (stats.SetCount > 0 && stats.OperationTimings.TryGetValue("Set", out var setTime))
                {
                    stats.AverageSetTime = TimeSpan.FromMilliseconds(setTime.TotalMilliseconds / stats.SetCount);
                }

                // Raise event
                StatisticsUpdated?.Invoke(this, new CacheStatisticsEventArgs
                {
                    Region = region,
                    Operation = operation,
                    Duration = duration,
                    Success = success
                });
            }
            finally
            {
                _statisticsLock.Release();
            }
        }

        private void ReportStatistics(object? state)
        {
            try
            {
                foreach (var (region, stats) in _statistics)
                {
                    if (stats.HitCount + stats.MissCount > 0)
                    {
                        _logger.LogInformation(
                            "Cache statistics for {Region}: Hits={HitCount}, Misses={MissCount}, HitRate={HitRate:P}, Sets={SetCount}, Evictions={EvictionCount}",
                            region, stats.HitCount, stats.MissCount, stats.HitRate, stats.SetCount, stats.EvictionCount);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting cache statistics");
            }
        }
    }
}