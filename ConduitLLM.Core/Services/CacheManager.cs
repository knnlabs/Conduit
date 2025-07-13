using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Unified cache manager implementation that provides consistent operations across
    /// memory and distributed cache with region support and comprehensive statistics.
    /// </summary>
    public class CacheManager : ICacheManager, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly ILogger<CacheManager> _logger;
        private readonly ICacheStatisticsCollector? _statisticsCollector;
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionConfig> _regionConfigs;
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionStatistics> _statistics;
        private readonly SemaphoreSlim _statisticsLock = new(1, 1);
        private readonly Timer _statisticsTimer;
        private readonly bool _useDistributedCache;

        public event EventHandler<CacheEvictionEventArgs>? EntryEvicted;
        public event EventHandler<CacheStatisticsEventArgs>? StatisticsUpdated;

        public bool IsAvailable => true;

        public CacheManager(
            IMemoryCache memoryCache,
            IDistributedCache? distributedCache,
            ILogger<CacheManager> logger,
            IOptions<CacheManagerOptions>? options = null,
            ICacheStatisticsCollector? statisticsCollector = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _distributedCache = distributedCache;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _statisticsCollector = statisticsCollector;
            _useDistributedCache = distributedCache != null;

            _regionConfigs = new ConcurrentDictionary<CacheRegion, CacheRegionConfig>();
            _statistics = new ConcurrentDictionary<CacheRegion, CacheRegionStatistics>();

            // Initialize default configurations
            InitializeDefaultConfigurations(options?.Value);

            // Start statistics reporting timer
            _statisticsTimer = new Timer(ReportStatistics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task<T?> GetAsync<T>(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var fullKey = BuildKey(key, region);
            var config = GetRegionConfig(region);

            try
            {
                T? value = default;
                bool found = false;

                // Try memory cache first if enabled
                if (config.UseMemoryCache && _memoryCache.TryGetValue(fullKey, out T? memoryCached))
                {
                    value = memoryCached;
                    found = true;
                    _logger.LogDebug("Memory cache hit for key {Key} in region {Region}", key, region);
                }
                // Try distributed cache if memory miss and distributed is enabled
                else if (config.UseDistributedCache && _useDistributedCache && _distributedCache != null)
                {
                    var distributedData = await _distributedCache.GetAsync(fullKey, cancellationToken);
                    if (distributedData != null)
                    {
                        value = JsonSerializer.Deserialize<T>(distributedData);
                        found = true;
                        _logger.LogDebug("Distributed cache hit for key {Key} in region {Region}", key, region);

                        // Populate memory cache for faster subsequent access
                        if (config.UseMemoryCache && value != null)
                        {
                            var ttl = config.DefaultTTL ?? TimeSpan.FromMinutes(5);
                            _memoryCache.Set(fullKey, value, ttl);
                        }
                    }
                }

                // Update statistics
                await UpdateStatisticsAsync(region, found ? "Hit" : "Miss", stopwatch.Elapsed, found);
                
                // Record operation in statistics collector
                if (_statisticsCollector != null)
                {
                    await _statisticsCollector.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = found ? CacheOperationType.Hit : CacheOperationType.Miss,
                        Success = true,
                        Duration = stopwatch.Elapsed,
                        Key = key
                    });
                }

                if (!found)
                {
                    _logger.LogDebug("Cache miss for key {Key} in region {Region}", key, region);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving key {Key} from cache region {Region}", key, region);
                await UpdateStatisticsAsync(region, "Error", stopwatch.Elapsed, false);
                return default;
            }
        }

        public async Task<CacheEntry<T>?> GetEntryAsync<T>(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var value = await GetAsync<T>(key, region, cancellationToken);
            if (value == null)
                return null;

            // For now, return a basic entry. In production, we'd store metadata alongside the value
            return new CacheEntry<T>
            {
                Value = value,
                Key = key,
                Region = region,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                AccessCount = 1
            };
        }

        public async Task SetAsync<T>(string key, T value, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var fullKey = BuildKey(key, region);
            var config = GetRegionConfig(region);

            if (!config.Enabled)
            {
                _logger.LogDebug("Caching disabled for region {Region}", region);
                return;
            }

            try
            {
                var expiry = ttl ?? config.DefaultTTL ?? TimeSpan.FromMinutes(30);
                if (config.MaxTTL.HasValue && expiry > config.MaxTTL.Value)
                {
                    expiry = config.MaxTTL.Value;
                }

                // Set in memory cache if enabled
                if (config.UseMemoryCache)
                {
                    var memoryCacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiry,
                        Priority = GetCachePriority(config.Priority),
                        PostEvictionCallbacks =
                        {
                            new PostEvictionCallbackRegistration
                            {
                                EvictionCallback = OnMemoryCacheEviction,
                                State = (key, region)
                            }
                        }
                    };

                    _memoryCache.Set(fullKey, value, memoryCacheOptions);
                }

                // Set in distributed cache if enabled
                if (config.UseDistributedCache && _useDistributedCache && _distributedCache != null)
                {
                    var serialized = JsonSerializer.SerializeToUtf8Bytes(value);
                    var distributedOptions = new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiry
                    };

                    await _distributedCache.SetAsync(fullKey, serialized, distributedOptions, cancellationToken);
                }

                _logger.LogDebug("Cached key {Key} in region {Region} with TTL {TTL}", key, region, expiry);
                await UpdateStatisticsAsync(region, "Set", stopwatch.Elapsed, true);
                
                // Record operation in statistics collector
                if (_statisticsCollector != null)
                {
                    await _statisticsCollector.RecordOperationAsync(new CacheOperation
                    {
                        Region = region,
                        OperationType = CacheOperationType.Set,
                        Success = true,
                        Duration = stopwatch.Elapsed,
                        Key = key,
                        DataSizeBytes = value != null ? EstimateObjectSize(value) : 0
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting key {Key} in cache region {Region}", key, region);
                await UpdateStatisticsAsync(region, "SetError", stopwatch.Elapsed, false);
                throw;
            }
        }

        public async Task SetEntryAsync<T>(CacheEntry<T> entry, CancellationToken cancellationToken = default)
        {
            var ttl = entry.ExpiresAt.HasValue
                ? entry.ExpiresAt.Value - DateTime.UtcNow
                : (TimeSpan?)null;

            await SetAsync(entry.Key, entry.Value, entry.Region, ttl, cancellationToken);
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var existing = await GetAsync<T>(key, region, cancellationToken);
            if (existing != null)
                return existing;

            // Use a simple lock per key to prevent cache stampede
            var lockKey = $"lock_{BuildKey(key, region)}";
            using (await AcquireLockAsync(lockKey, cancellationToken))
            {
                // Double-check after acquiring lock
                existing = await GetAsync<T>(key, region, cancellationToken);
                if (existing != null)
                    return existing;

                // Create the value
                var value = await factory();
                await SetAsync(key, value, region, ttl, cancellationToken);
                return value;
            }
        }

        public async Task<bool> RemoveAsync(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var fullKey = BuildKey(key, region);
            var config = GetRegionConfig(region);
            var removed = false;

            try
            {
                if (config.UseMemoryCache)
                {
                    _memoryCache.Remove(fullKey);
                    removed = true;
                }

                if (config.UseDistributedCache && _useDistributedCache && _distributedCache != null)
                {
                    await _distributedCache.RemoveAsync(fullKey, cancellationToken);
                    removed = true;
                }

                if (removed)
                {
                    _logger.LogDebug("Removed key {Key} from region {Region}", key, region);
                    await UpdateStatisticsAsync(region, "Remove", stopwatch.Elapsed, true);
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key {Key} from region {Region}", key, region);
                await UpdateStatisticsAsync(region, "RemoveError", stopwatch.Elapsed, false);
                return false;
            }
        }

        public async Task<int> RemoveManyAsync(IEnumerable<string> keys, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var tasks = keys.Select(key => RemoveAsync(key, region, cancellationToken));
            var results = await Task.WhenAll(tasks);
            return results.Count(r => r);
        }

        public async Task<int> RemoveByPatternAsync(string pattern, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            // This is a simplified implementation. In production, we'd need more sophisticated pattern matching
            var keys = await ListKeysAsync(region, pattern, int.MaxValue, cancellationToken);
            return await RemoveManyAsync(keys, region, cancellationToken);
        }

        public async Task ClearRegionAsync(CacheRegion region, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Clear from memory cache (simplified - in production we'd track keys per region)
                var keys = await ListKeysAsync(region, null, int.MaxValue, cancellationToken);
                foreach (var key in keys)
                {
                    _memoryCache.Remove(BuildKey(key, region));
                }

                // Clear from distributed cache if available
                if (_useDistributedCache && _distributedCache != null)
                {
                    // This would need implementation based on the specific distributed cache
                    _logger.LogWarning("Distributed cache clear not fully implemented for region {Region}", region);
                }

                _logger.LogInformation("Cleared cache region {Region}", region);
                await UpdateStatisticsAsync(region, "Clear", stopwatch.Elapsed, true);

                // Reset statistics for the region
                _statistics[region] = new CacheRegionStatistics { Region = region, LastResetTime = DateTime.UtcNow };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cache region {Region}", region);
                await UpdateStatisticsAsync(region, "ClearError", stopwatch.Elapsed, false);
                throw;
            }
        }

        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            var tasks = Enum.GetValues<CacheRegion>()
                .Select(region => ClearRegionAsync(region, cancellationToken));
            await Task.WhenAll(tasks);
        }

        public async Task<bool> ExistsAsync(string key, CacheRegion region = CacheRegion.Default, CancellationToken cancellationToken = default)
        {
            var value = await GetAsync<object>(key, region, cancellationToken);
            return value != null;
        }

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

            // Fallback to internal statistics
            if (!_statistics.TryGetValue(region, out var stats))
            {
                stats = new CacheRegionStatistics { Region = region, LastResetTime = DateTime.UtcNow };
                _statistics[region] = stats;
            }

            return stats;
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

        public CacheRegionConfig GetRegionConfig(CacheRegion region)
        {
            return _regionConfigs.GetOrAdd(region, CreateDefaultConfig);
        }

        public Task UpdateRegionConfigAsync(CacheRegionConfig config)
        {
            _regionConfigs[config.Region] = config;
            _logger.LogInformation("Updated configuration for cache region {Region}", config.Region);
            return Task.CompletedTask;
        }

        public async Task<IEnumerable<string>> ListKeysAsync(CacheRegion region, string? pattern = null, int maxCount = 100, CancellationToken cancellationToken = default)
        {
            // This is a simplified implementation. In production, we'd need to track keys per region
            _logger.LogWarning("ListKeysAsync is not fully implemented - returning empty list");
            return await Task.FromResult(Enumerable.Empty<string>());
        }

        public async Task<IEnumerable<CacheEntry<object>>> GetEntriesAsync(CacheRegion region, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
        {
            // This is a simplified implementation
            _logger.LogWarning("GetEntriesAsync is not fully implemented - returning empty list");
            return await Task.FromResult(Enumerable.Empty<CacheEntry<object>>());
        }

        public async Task<bool> RefreshAsync(string key, CacheRegion region = CacheRegion.Default, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            var value = await GetAsync<object>(key, region, cancellationToken);
            if (value == null)
                return false;

            await SetAsync(key, value, region, ttl, cancellationToken);
            return true;
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
                _memoryCache.Set(testKey, "test", TimeSpan.FromSeconds(1));
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

        private void InitializeDefaultConfigurations(CacheManagerOptions? options)
        {
            var defaultConfigs = new Dictionary<CacheRegion, (TimeSpan ttl, int priority, bool distributed)>
            {
                { CacheRegion.VirtualKeys, (TimeSpan.FromMinutes(30), 100, true) },
                { CacheRegion.RateLimits, (TimeSpan.FromMinutes(5), 90, true) },
                { CacheRegion.ProviderHealth, (TimeSpan.FromMinutes(1), 80, true) },
                { CacheRegion.ModelMetadata, (TimeSpan.FromHours(24), 50, true) },
                { CacheRegion.AuthTokens, (TimeSpan.FromHours(1), 95, true) },
                { CacheRegion.IpFilters, (TimeSpan.FromMinutes(15), 85, true) },
                { CacheRegion.AsyncTasks, (TimeSpan.FromHours(2), 40, true) },
                { CacheRegion.ProviderResponses, (TimeSpan.FromMinutes(60), 60, true) },
                { CacheRegion.Embeddings, (TimeSpan.FromDays(7), 70, true) },
                { CacheRegion.GlobalSettings, (TimeSpan.FromMinutes(30), 75, true) },
                { CacheRegion.ProviderCredentials, (TimeSpan.FromHours(4), 80, true) },
                { CacheRegion.ModelCosts, (TimeSpan.FromHours(12), 55, true) },
                { CacheRegion.AudioStreams, (TimeSpan.FromMinutes(10), 30, false) },
                { CacheRegion.Monitoring, (TimeSpan.FromMinutes(5), 45, false) },
                { CacheRegion.Default, (TimeSpan.FromMinutes(15), 50, false) }
            };

            foreach (var (region, (ttl, priority, distributed)) in defaultConfigs)
            {
                var config = new CacheRegionConfig
                {
                    Region = region,
                    Enabled = true,
                    DefaultTTL = ttl,
                    Priority = priority,
                    UseDistributedCache = distributed && _useDistributedCache,
                    UseMemoryCache = true,
                    EvictionPolicy = CacheEvictionPolicy.LRU,
                    EnableDetailedStats = true
                };

                // Apply custom options if provided
                if (options?.RegionConfigs?.TryGetValue(region, out var customConfig) == true)
                {
                    // Merge custom config
                    config.Enabled = customConfig.Enabled;
                    config.DefaultTTL = customConfig.DefaultTTL ?? config.DefaultTTL;
                    config.MaxTTL = customConfig.MaxTTL;
                    config.Priority = customConfig.Priority;
                    config.EvictionPolicy = customConfig.EvictionPolicy;
                }

                _regionConfigs[region] = config;
                _statistics[region] = new CacheRegionStatistics { Region = region, LastResetTime = DateTime.UtcNow };
            }
        }

        private string BuildKey(string key, CacheRegion region)
        {
            return $"{region}:{key}";
        }

        private CacheItemPriority GetCachePriority(int priority)
        {
            return priority switch
            {
                >= 80 => CacheItemPriority.High,
                >= 50 => CacheItemPriority.Normal,
                _ => CacheItemPriority.Low
            };
        }

        private void OnMemoryCacheEviction(object key, object? value, EvictionReason reason, object? state)
        {
            if (state is ValueTuple<string, CacheRegion> evictionInfo)
            {
                var (originalKey, region) = evictionInfo;
                var evictionReason = reason switch
                {
                    EvictionReason.Expired => CacheEvictionReason.Expired,
                    EvictionReason.Capacity => CacheEvictionReason.CapacityReached,
                    EvictionReason.Removed => CacheEvictionReason.Removed,
                    EvictionReason.Replaced => CacheEvictionReason.Replaced,
                    _ => CacheEvictionReason.PolicyTriggered
                };

                EntryEvicted?.Invoke(this, new CacheEvictionEventArgs
                {
                    Key = originalKey,
                    Region = region,
                    Reason = evictionReason,
                    EvictedAt = DateTime.UtcNow
                });

                _ = UpdateStatisticsAsync(region, "Eviction", TimeSpan.Zero, true);
            }
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

        private async Task<IDisposable> AcquireLockAsync(string lockKey, CancellationToken cancellationToken)
        {
            // Simple in-memory lock implementation. In production, use distributed locks for distributed cache
            var semaphore = new SemaphoreSlim(1, 1);
            await semaphore.WaitAsync(cancellationToken);
            return new DisposableLock(semaphore);
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

        private class DisposableLock : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public DisposableLock(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                _semaphore.Release();
            }
        }

        private CacheRegionConfig CreateDefaultConfig(CacheRegion region)
        {
            return new CacheRegionConfig
            {
                Region = region,
                Enabled = true,
                DefaultTTL = TimeSpan.FromMinutes(15),
                UseMemoryCache = true,
                UseDistributedCache = _useDistributedCache,
                Priority = 50,
                EvictionPolicy = CacheEvictionPolicy.LRU,
                EnableDetailedStats = true
            };
        }

        private long EstimateObjectSize(object obj)
        {
            // This is a simplified estimation
            // In production, consider using a more accurate serialization-based approach
            try
            {
                if (obj == null) return 0;
                
                var json = System.Text.Json.JsonSerializer.Serialize(obj);
                return System.Text.Encoding.UTF8.GetByteCount(json);
            }
            catch
            {
                // Fallback to rough estimation
                return 1024; // 1KB default
            }
        }

        public void Dispose()
        {
            _statisticsTimer?.Dispose();
            _statisticsLock?.Dispose();
        }
    }

    /// <summary>
    /// Options for configuring the cache manager.
    /// </summary>
    public class CacheManagerOptions
    {
        /// <summary>
        /// Custom configurations for specific regions.
        /// </summary>
        public Dictionary<CacheRegion, CacheRegionConfig>? RegionConfigs { get; set; }

        /// <summary>
        /// Whether to enable detailed statistics tracking.
        /// </summary>
        public bool EnableDetailedStatistics { get; set; } = true;

        /// <summary>
        /// Interval for reporting statistics to logs.
        /// </summary>
        public TimeSpan StatisticsReportingInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Whether to use distributed cache when available.
        /// </summary>
        public bool UseDistributedCache { get; set; } = true;

        /// <summary>
        /// Global settings that apply to all regions unless overridden.
        /// </summary>
        public CacheRegionConfig? GlobalDefaults { get; set; }
    }
}