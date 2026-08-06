using ConduitLLM.Admin.Services;

using Microsoft.Extensions.Caching.Memory;

namespace ConduitLLM.Tests.Admin.Services;

public sealed class AnalyticsCacheInvalidatorTests
{
    [Fact]
    public void Invalidate_ExpiresTrackedEntriesWithoutClearingUnrelatedCacheEntries()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var invalidator = new AnalyticsCacheInvalidator();

        SetTrackedEntry(cache, invalidator, "analytics:summary:first", "first");
        SetTrackedEntry(cache, invalidator, "analytics:cost:trend:second", "second");
        cache.Set("security:unrelated", "preserved");

        var keysInvalidated = invalidator.Invalidate();

        Assert.Equal(2, keysInvalidated);
        Assert.False(cache.TryGetValue("analytics:summary:first", out _));
        Assert.False(cache.TryGetValue("analytics:cost:trend:second", out _));
        Assert.Equal("preserved", cache.Get<string>("security:unrelated"));
    }

    private static void SetTrackedEntry(
        IMemoryCache cache,
        AnalyticsCacheInvalidator invalidator,
        string cacheKey,
        object value)
    {
        using var entry = cache.CreateEntry(cacheKey);
        invalidator.TrackEntry(entry, cacheKey);
        entry.Value = value;
    }
}
