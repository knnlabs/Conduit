using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
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
    public partial class CacheManager : ICacheManager, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache? _distributedCache;
        private readonly ILogger<CacheManager> _logger;
        private readonly ICacheStatisticsCollector? _statisticsCollector;
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionConfig> _regionConfigs;
        private readonly ConcurrentDictionary<CacheRegion, CacheRegionStatistics> _statistics;
        private readonly ConcurrentDictionary<CacheRegion, ConcurrentDictionary<string, byte>> _regionKeys;
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
            _regionKeys = new ConcurrentDictionary<CacheRegion, ConcurrentDictionary<string, byte>>();

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
                    
                    // Track the key for this region
                    var regionKeys = _regionKeys.GetOrAdd(region, _ => new ConcurrentDictionary<string, byte>());
                    regionKeys.TryAdd(key, 0);
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
                    // Remove the key from tracking
                    if (_regionKeys.TryGetValue(region, out var regionKeys))
                    {
                        regionKeys.TryRemove(key, out _);
                    }
                    
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







        public void Dispose()
        {
            _statisticsTimer?.Dispose();
            _statisticsLock?.Dispose();
        }
    }
}