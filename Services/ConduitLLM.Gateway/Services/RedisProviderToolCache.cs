using System.Text.Json;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Constants;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services;

/// <summary>
/// Redis-backed cache for provider tool lookups in the billing pipeline.
/// Caches active tools per provider type to eliminate per-request database queries.
/// </summary>
public class RedisProviderToolCache : IProviderToolCache, IDisposable
{
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisProviderToolCache> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);
    private readonly StatisticsBuffer _statsBuffer = new();
    private readonly Timer _flushTimer;
    private readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisProviderToolCache(
        IConnectionMultiplexer redis,
        ILogger<RedisProviderToolCache> logger)
    {
        _database = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _logger = logger;

        // Subscribe to invalidation channel for cross-instance cache consistency
        _subscriber.Subscribe(
            RedisChannel.Literal(CacheKeys.ProviderTool.InvalidationChannel),
            OnToolInvalidated);

        _flushTimer = new Timer(FlushStatisticsCallback, null, _flushInterval, _flushInterval);

        _logger.LogInformation("RedisProviderToolCache initialized with {Expiry} TTL", _defaultExpiry);
    }

    /// <inheritdoc/>
    public async Task<List<ProviderTool>> GetActiveToolsForProviderAsync(
        ProviderType providerType,
        Func<ProviderType, Task<List<ProviderTool>>> databaseFallback)
    {
        var cacheKey = CacheKeys.ProviderTool.ByProvider(providerType.ToString());

        try
        {
            // Check cache
            var cachedValue = await _database.StringGetAsync(cacheKey);
            if (cachedValue.HasValue)
            {
                var jsonString = (string?)cachedValue;
                if (jsonString is not null)
                {
                    var tools = JsonSerializer.Deserialize<List<ProviderTool>>(jsonString, _jsonOptions);
                    if (tools != null)
                    {
                        Interlocked.Increment(ref _statsBuffer.Hits);
                        _logger.LogDebug("Provider tool cache hit for {ProviderType}: {Count} tools",
                            providerType, tools.Count);
                        return tools;
                    }
                }
            }

            // Cache miss — load from database
            Interlocked.Increment(ref _statsBuffer.Misses);
            _logger.LogDebug("Provider tool cache miss for {ProviderType}, loading from database", providerType);

            var dbTools = await databaseFallback(providerType);

            // Cache the result
            var json = JsonSerializer.Serialize(dbTools, _jsonOptions);
            await _database.StringSetAsync(cacheKey, json, _defaultExpiry);

            return dbTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accessing provider tool cache for {ProviderType}, falling back to database",
                providerType);
            Interlocked.Increment(ref _statsBuffer.Misses);
            return await databaseFallback(providerType);
        }
    }

    /// <inheritdoc/>
    public async Task InvalidateProviderAsync(ProviderType providerType)
    {
        try
        {
            var cacheKey = CacheKeys.ProviderTool.ByProvider(providerType.ToString());
            await _database.KeyDeleteAsync(cacheKey);
            Interlocked.Increment(ref _statsBuffer.Invalidations);
            _logger.LogInformation("Provider tool cache invalidated for {ProviderType}", providerType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating provider tool cache for {ProviderType}", providerType);
        }
    }

    /// <inheritdoc/>
    public async Task ClearAllAsync()
    {
        try
        {
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
            var keys = server.Keys(pattern: CacheKeys.ProviderTool.Prefix + "*");

            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }

            _logger.LogWarning("All provider tool cache entries cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all provider tool cache entries");
        }
    }

    /// <inheritdoc/>
    public async Task<ProviderToolCacheStats> GetStatsAsync()
    {
        try
        {
            var serviceName = CacheKeys.Stats.ProviderToolService;
            var hits = await _database.StringGetAsync(CacheKeys.Stats.Hits(serviceName));
            var misses = await _database.StringGetAsync(CacheKeys.Stats.Misses(serviceName));
            var invalidations = await _database.StringGetAsync(CacheKeys.Stats.Invalidations(serviceName));
            var resetTime = await _database.StringGetAsync(CacheKeys.Stats.ResetTime(serviceName));

            // Include pending buffered stats
            var pendingHits = Interlocked.Read(ref _statsBuffer.Hits);
            var pendingMisses = Interlocked.Read(ref _statsBuffer.Misses);
            var pendingInvalidations = Interlocked.Read(ref _statsBuffer.Invalidations);

            // Count entries
            var server = _database.Multiplexer.GetServer(_database.Multiplexer.GetEndPoints()[0]);
            var entryCount = 0L;
            foreach (var _ in server.Keys(pattern: CacheKeys.ProviderTool.Prefix + "*"))
            {
                entryCount++;
            }

            return new ProviderToolCacheStats
            {
                HitCount = (hits.HasValue ? (long)hits : 0) + pendingHits,
                MissCount = (misses.HasValue ? (long)misses : 0) + pendingMisses,
                InvalidationCount = (invalidations.HasValue ? (long)invalidations : 0) + pendingInvalidations,
                LastResetTime = resetTime.HasValue && DateTime.TryParse(resetTime, out var time)
                    ? time
                    : DateTime.UtcNow,
                EntryCount = entryCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting provider tool cache statistics");
            return new ProviderToolCacheStats { LastResetTime = DateTime.UtcNow };
        }
    }

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
                _logger.LogDebug("Invalidated provider tool cache from pub/sub: {ProviderType}", providerType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling provider tool cache invalidation from pub/sub: {Message}",
                message.ToString());
        }
    }

    private void FlushStatisticsCallback(object? state) => _ = FlushStatisticsAsync();

    private async Task FlushStatisticsAsync()
    {
        if (_disposed) return;
        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var (hits, misses, invalidations) = _statsBuffer.GetAndReset();
            if (hits == 0 && misses == 0 && invalidations == 0) return;

            var serviceName = CacheKeys.Stats.ProviderToolService;
            var batch = _database.CreateBatch();
            var tasks = new List<Task>();

            if (hits > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Hits(serviceName), hits));
            if (misses > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Misses(serviceName), misses));
            if (invalidations > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Invalidations(serviceName), invalidations));

            batch.Execute();
            await Task.WhenAll(tasks);

            _logger.LogDebug("Flushed provider tool cache stats: Hits={Hits}, Misses={Misses}, Invalidations={Invalidations}",
                hits, misses, invalidations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing provider tool cache statistics");
        }
        finally
        {
            _flushLock.Release();
        }
    }

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
                    var serviceName = CacheKeys.Stats.ProviderToolService;
                    var tasks = new List<Task>();
                    if (hits > 0) tasks.Add(_database.StringIncrementAsync(CacheKeys.Stats.Hits(serviceName), hits));
                    if (misses > 0) tasks.Add(_database.StringIncrementAsync(CacheKeys.Stats.Misses(serviceName), misses));
                    if (invalidations > 0) tasks.Add(_database.StringIncrementAsync(CacheKeys.Stats.Invalidations(serviceName), invalidations));
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
            _logger.LogWarning(ex, "Error during final provider tool cache statistics flush on dispose");
        }

        _flushLock.Dispose();
    }

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
