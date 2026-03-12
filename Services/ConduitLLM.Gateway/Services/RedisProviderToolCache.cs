using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services;

/// <summary>
/// Redis-backed cache for provider tool lookups in the billing pipeline.
/// Caches active tools per provider type to eliminate per-request database queries.
/// </summary>
public class RedisProviderToolCache : RedisCacheServiceBase, IProviderToolCache, IDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly StatisticsBuffer _statsBuffer = new();
    private readonly Timer _flushTimer;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _disposed;

    private static readonly string ServiceName = CacheKeys.Stats.ProviderToolService;

    public RedisProviderToolCache(
        IConnectionMultiplexer redis,
        ILogger<RedisProviderToolCache> logger)
        : base(redis, logger, TimeSpan.FromHours(1), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        })
    {
        _subscriber = redis.GetSubscriber();

        // Subscribe to invalidation channel for cross-instance cache consistency
        _subscriber.Subscribe(
            RedisChannel.Literal(CacheKeys.ProviderTool.InvalidationChannel),
            OnToolInvalidated);

        _flushTimer = new Timer(FlushStatisticsCallback, null, _flushInterval, _flushInterval);

        Logger.LogInformation("RedisProviderToolCache initialized with {Expiry} TTL", DefaultExpiry);
    }

    /// <inheritdoc/>
    public async Task<List<ProviderTool>> GetActiveToolsForProviderAsync(
        ProviderType providerType,
        Func<ProviderType, Task<List<ProviderTool>>> databaseFallback)
    {
        var cacheKey = CacheKeys.ProviderTool.ByProvider(providerType.ToString());

        var result = await GetOrFallbackAsync<List<ProviderTool>>(
            cacheKey,
            ServiceName,
            async () => await databaseFallback(providerType) as List<ProviderTool>,
            debugLabel: $"Provider tools for {providerType}");

        return result ?? new List<ProviderTool>();
    }

    /// <inheritdoc/>
    public async Task InvalidateProviderAsync(ProviderType providerType)
    {
        try
        {
            var cacheKey = CacheKeys.ProviderTool.ByProvider(providerType.ToString());
            await Database.KeyDeleteAsync(cacheKey);
            Interlocked.Increment(ref _statsBuffer.Invalidations);
            Logger.LogInformation("Provider tool cache invalidated for {ProviderType}", providerType);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error invalidating provider tool cache for {ProviderType}", providerType);
        }
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync()
    {
        await ClearAllByPatternAsync(CacheKeys.ProviderTool.Prefix + "*");
        Logger.LogWarning("All provider tool cache entries cleared");
    }

    /// <inheritdoc/>
    public async Task<ProviderToolCacheStats> GetStatsAsync()
    {
        try
        {
            var (hits, misses, invalidations, resetTime) = await GetBaseStatsAsync(ServiceName);

            // Include pending buffered stats
            var pendingHits = Interlocked.Read(ref _statsBuffer.Hits);
            var pendingMisses = Interlocked.Read(ref _statsBuffer.Misses);
            var pendingInvalidations = Interlocked.Read(ref _statsBuffer.Invalidations);

            return new ProviderToolCacheStats
            {
                HitCount = hits + pendingHits,
                MissCount = misses + pendingMisses,
                InvalidationCount = invalidations + pendingInvalidations,
                LastResetTime = resetTime,
                EntryCount = CountEntries(CacheKeys.ProviderTool.Prefix + "*")
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting provider tool cache statistics");
            return new ProviderToolCacheStats { LastResetTime = DateTime.UtcNow };
        }
    }

    #region Buffered Stats Override

    protected override Task TrackHitAsync(string serviceName)
    {
        Interlocked.Increment(ref _statsBuffer.Hits);
        return Task.CompletedTask;
    }

    protected override Task TrackMissAsync(string serviceName)
    {
        Interlocked.Increment(ref _statsBuffer.Misses);
        return Task.CompletedTask;
    }

    protected override Task TrackInvalidationAsync(string serviceName, long count = 1)
    {
        Interlocked.Add(ref _statsBuffer.Invalidations, count);
        return Task.CompletedTask;
    }

    #endregion

    #region Pub/Sub

    private void OnToolInvalidated(RedisChannel channel, RedisValue message)
    {
        _ = OnToolInvalidatedAsync(message);
    }

    private async Task OnToolInvalidatedAsync(RedisValue message)
    {
        try
        {
            var providerTypeStr = message.ToString();
            if (providerTypeStr == "*")
            {
                await ClearAllAsync();
            }
            else if (Enum.TryParse<ProviderType>(providerTypeStr, true, out var providerType))
            {
                await InvalidateProviderAsync(providerType);
                Logger.LogDebug("Invalidated provider tool cache from pub/sub: {ProviderType}", providerType);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling provider tool cache invalidation from pub/sub: {Message}",
                message.ToString());
        }
    }

    #endregion

    #region Statistics Flush

    private void FlushStatisticsCallback(object? state) => _ = FlushStatisticsAsync();

    private async Task FlushStatisticsAsync()
    {
        if (_disposed) return;
        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var (hits, misses, invalidations) = _statsBuffer.GetAndReset();
            if (hits == 0 && misses == 0 && invalidations == 0) return;

            var batch = Database.CreateBatch();
            var tasks = new List<Task>();

            if (hits > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
            if (misses > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
            if (invalidations > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));

            batch.Execute();
            await Task.WhenAll(tasks);

            Logger.LogDebug("Flushed provider tool cache stats: Hits={Hits}, Misses={Misses}, Invalidations={Invalidations}",
                hits, misses, invalidations);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error flushing provider tool cache statistics");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _flushTimer.Change(Timeout.Infinite, 0);
        _flushTimer.Dispose();

        // Final synchronous flush
        try
        {
            _flushLock.Wait(TimeSpan.FromSeconds(5));
            try
            {
                var (hits, misses, invalidations) = _statsBuffer.GetAndReset();
                if (hits > 0 || misses > 0 || invalidations > 0)
                {
                    var tasks = new List<Task>();
                    if (hits > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
                    if (misses > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
                    if (invalidations > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));
                    Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during final provider tool cache statistics flush on dispose");
        }

        _flushLock.Dispose();
    }

    #endregion

    private class StatisticsBuffer
    {
        public long Hits;
        public long Misses;
        public long Invalidations;

        public (long hits, long misses, long invalidations) GetAndReset()
        {
            var hits = Interlocked.Exchange(ref Hits, 0);
            var misses = Interlocked.Exchange(ref Misses, 0);
            var invalidations = Interlocked.Exchange(ref Invalidations, 0);
            return (hits, misses, invalidations);
        }
    }
}
