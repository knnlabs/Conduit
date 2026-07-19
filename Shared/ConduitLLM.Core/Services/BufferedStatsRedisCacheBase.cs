using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using ConduitLLM.Configuration.Constants;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Redis cache base that buffers hit/miss/invalidation counters locally and flushes
/// them to Redis in batches every 5 seconds, reducing per-operation Redis round-trips.
/// Extends <see cref="RedisCacheServiceBase"/> and overrides its tracking methods.
/// </summary>
/// <remarks>
/// Subclasses that need additional custom counters (e.g., pattern match counts) can
/// override <see cref="OnFlush"/> and <see cref="OnFinalFlush"/> to include them
/// in the periodic and dispose flush cycles.
/// </remarks>
public abstract class BufferedStatsRedisCacheBase : RedisCacheServiceBase, IDisposable
{
    private long _bufferedHits;
    private long _bufferedMisses;
    private long _bufferedInvalidations;

    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// The service name used for stats keys in Redis.
    /// </summary>
    protected abstract string ServiceName { get; }

    /// <summary>
    /// Pending hits not yet flushed to Redis. Use in GetStatsAsync() implementations.
    /// </summary>
    protected long PendingHits => Interlocked.Read(ref _bufferedHits);

    /// <summary>
    /// Pending misses not yet flushed to Redis. Use in GetStatsAsync() implementations.
    /// </summary>
    protected long PendingMisses => Interlocked.Read(ref _bufferedMisses);

    /// <summary>
    /// Pending invalidations not yet flushed to Redis. Use in GetStatsAsync() implementations.
    /// </summary>
    protected long PendingInvalidations => Interlocked.Read(ref _bufferedInvalidations);

    /// <summary>
    /// Whether this instance has been disposed.
    /// </summary>
    protected bool IsDisposed => _disposed;

    protected BufferedStatsRedisCacheBase(
        IConnectionMultiplexer redis,
        ILogger logger,
        TimeSpan defaultExpiry,
        JsonSerializerOptions? jsonOptions = null)
        : base(redis, logger, defaultExpiry, jsonOptions)
    {
        InitializeStatsResetTime(ServiceName);
        _flushTimer = new Timer(
            _ => _ = FlushStatisticsAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));
    }

    #region Buffered Stats Overrides

    protected sealed override Task TrackHitAsync(string serviceName)
    {
        Interlocked.Increment(ref _bufferedHits);
        return Task.CompletedTask;
    }

    protected sealed override Task TrackMissAsync(string serviceName)
    {
        Interlocked.Increment(ref _bufferedMisses);
        return Task.CompletedTask;
    }

    protected sealed override Task TrackInvalidationAsync(string serviceName, long count = 1)
    {
        Interlocked.Add(ref _bufferedInvalidations, count);
        return Task.CompletedTask;
    }

    #endregion

    #region Flush

    /// <summary>
    /// Override to add custom counters to the periodic flush batch.
    /// Called while the flush lock is held. Use <see cref="IBatch"/> to queue Redis commands.
    /// </summary>
    protected virtual void OnFlush(IBatch batch, List<Task> tasks) { }

    /// <summary>
    /// Override to add custom counters to the final dispose flush.
    /// Called while the flush lock is held. Add tasks to the list for <see cref="Task.WaitAll"/>.
    /// </summary>
    protected virtual void OnFinalFlush(List<Task> tasks) { }

    /// <summary>
    /// Override to return true if custom counters have pending data.
    /// Prevents early-exit from flush when standard counters are all zero.
    /// </summary>
    protected virtual bool HasPendingCustomStats() => false;

    private async Task FlushStatisticsAsync()
    {
        if (_disposed) return;
        if (!await _flushLock.WaitAsync(0)) return;

        try
        {
            var hits = Interlocked.Exchange(ref _bufferedHits, 0);
            var misses = Interlocked.Exchange(ref _bufferedMisses, 0);
            var invalidations = Interlocked.Exchange(ref _bufferedInvalidations, 0);

            if (hits == 0 && misses == 0 && invalidations == 0 && !HasPendingCustomStats()) return;

            var batch = Database.CreateBatch();
            var tasks = new List<Task>();

            if (hits > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
            if (misses > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
            if (invalidations > 0) tasks.Add(batch.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));

            OnFlush(batch, tasks);

            batch.Execute();
            await Task.WhenAll(tasks);

            Logger.LogDebug("Flushed {ServiceName} cache stats: Hits={Hits}, Misses={Misses}, Invalidations={Invalidations}",
                ServiceName, hits, misses, invalidations);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error flushing {ServiceName} cache statistics", ServiceName);
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

        try
        {
            _flushLock.Wait(TimeSpan.FromSeconds(5));
            try
            {
                var hits = Interlocked.Exchange(ref _bufferedHits, 0);
                var misses = Interlocked.Exchange(ref _bufferedMisses, 0);
                var invalidations = Interlocked.Exchange(ref _bufferedInvalidations, 0);

                if (hits > 0 || misses > 0 || invalidations > 0 || HasPendingCustomStats())
                {
                    var tasks = new List<Task>();
                    if (hits > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Hits(ServiceName), hits));
                    if (misses > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Misses(ServiceName), misses));
                    if (invalidations > 0) tasks.Add(Database.StringIncrementAsync(CacheKeys.Stats.Invalidations(ServiceName), invalidations));

                    OnFinalFlush(tasks);

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
            Logger.LogWarning(ex, "Error during final {ServiceName} cache statistics flush on dispose", ServiceName);
        }

        _flushLock.Dispose();
    }

    #endregion
}
