using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Tracks analytics cache entries and invalidates them without affecting other memory caches.
/// </summary>
public sealed class AnalyticsCacheInvalidator
{
    private readonly object _sync = new();
    private CacheGeneration _currentGeneration = new();

    /// <summary>
    /// Adds the current analytics invalidation token to a cache entry.
    /// </summary>
    public void TrackEntry(ICacheEntry entry, string cacheKey)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);

        CacheGeneration generation;
        lock (_sync)
        {
            generation = _currentGeneration;
            generation.Add(cacheKey);
            entry.AddExpirationToken(new CancellationChangeToken(generation.TokenSource.Token));
            entry.RegisterPostEvictionCallback(
                CacheEntryEvicted,
                new CacheEntryRegistration(this, generation, cacheKey));
        }
    }

    /// <summary>
    /// Expires every cache entry tracked in the current generation.
    /// </summary>
    /// <returns>The number of distinct analytics cache keys invalidated.</returns>
    public int Invalidate()
    {
        CacheGeneration invalidatedGeneration;
        int keysInvalidated;

        lock (_sync)
        {
            invalidatedGeneration = _currentGeneration;
            _currentGeneration = new CacheGeneration();
            keysInvalidated = invalidatedGeneration.KeyCount;
        }

        invalidatedGeneration.TokenSource.Cancel();
        invalidatedGeneration.TokenSource.Dispose();
        return keysInvalidated;
    }

    private static void CacheEntryEvicted(
        object cacheKey,
        object? value,
        EvictionReason reason,
        object? state)
    {
        if (state is not CacheEntryRegistration registration)
        {
            return;
        }

        lock (registration.Owner._sync)
        {
            registration.Generation.Remove(registration.CacheKey);
        }
    }

    private sealed class CacheGeneration
    {
        private readonly Dictionary<string, int> _entryCounts = new(StringComparer.Ordinal);

        public CancellationTokenSource TokenSource { get; } = new();

        public int KeyCount => _entryCounts.Count;

        public void Add(string cacheKey)
        {
            _entryCounts[cacheKey] = _entryCounts.GetValueOrDefault(cacheKey) + 1;
        }

        public void Remove(string cacheKey)
        {
            if (!_entryCounts.TryGetValue(cacheKey, out var count))
            {
                return;
            }

            if (count == 1)
            {
                _entryCounts.Remove(cacheKey);
            }
            else
            {
                _entryCounts[cacheKey] = count - 1;
            }
        }
    }

    private sealed record CacheEntryRegistration(
        AnalyticsCacheInvalidator Owner,
        CacheGeneration Generation,
        string CacheKey);
}
