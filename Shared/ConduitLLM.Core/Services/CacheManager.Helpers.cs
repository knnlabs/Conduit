using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Cache manager implementation - Helper methods and support classes
    /// </summary>
    public partial class CacheManager
    {
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
                { CacheRegion.Providers, (TimeSpan.FromHours(4), 80, true) },
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
                _regionKeys[region] = new ConcurrentDictionary<string, byte>();
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

                // Remove from key tracking
                if (_regionKeys.TryGetValue(region, out var regionKeys))
                {
                    regionKeys.TryRemove(originalKey, out _);
                }

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

        private async Task<IDisposable> AcquireLockAsync(string lockKey, CancellationToken cancellationToken)
        {
            // Simple in-memory lock implementation. In production, use distributed locks for distributed cache
            var semaphore = new SemaphoreSlim(1, 1);
            await semaphore.WaitAsync(cancellationToken);
            return new DisposableLock(semaphore);
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