namespace ConduitLLM.Core.Models;

/// <summary>
/// Canonical statistics reported by Conduit caches.
/// </summary>
public sealed class CacheStats
{
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public long InvalidationCount { get; set; }

    /// <summary>
    /// Cache hit rate as a fraction from 0 through 1.
    /// </summary>
    public double HitRate =>
        HitCount + MissCount > 0
            ? (double)HitCount / (HitCount + MissCount)
            : 0;

    public TimeSpan AverageGetTime { get; set; }
    public DateTime LastResetTime { get; set; }
    public DateTime? LastInvalidationTime { get; set; }
    public long EntryCount { get; set; }
    public long PatternMatchCount { get; set; }
    public bool IsEnabled { get; set; } = true;
    public IReadOnlyList<string> CachedKeys { get; set; } = Array.Empty<string>();
}
