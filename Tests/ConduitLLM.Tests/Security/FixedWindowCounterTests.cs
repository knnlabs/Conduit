using ConduitLLM.Security.Services;
using ConduitLLM.Tests.Core.Services;

using FluentAssertions;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Xunit;

namespace ConduitLLM.Tests.Security;

/// <summary>
/// The atomic counter behind the IP, discovery and model-capability limiters. Read-then-write
/// counting undercounts under exactly the concurrency a rate limiter exists to handle.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "FixedWindowCounter")]
public class FixedWindowCounterTests : IAsyncLifetime
{
    private readonly string _prefix = RedisTestServer.NewKeyPrefix("fixed-window");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => RedisTestServer.CleanupAsync(_prefix);

    [SkippableFact]
    public async Task IncrementAsync_ConcurrentBurst_CountsEveryRequestExactlyOnce()
    {
        // 200 simultaneous increments. Read-then-write loses most of them; the whole point of
        // the Lua path is that none go missing.
        var counter = NewRedisCounter();
        var key = _prefix + "burst";

        var results = await Task.WhenAll(
            Enumerable.Range(0, 200).Select(_ => counter.IncrementAsync(key, 60)));

        results.Should().OnlyHaveUniqueItems("each increment must observe its own distinct total");
        results.Max().Should().Be(200);
        (await counter.ReadAsync(key)).Should().Be(200);
    }

    [SkippableFact]
    public async Task IncrementAsync_DoesNotExtendTheWindowOnLaterIncrements()
    {
        // A window whose TTL is renewed per request would let sustained abuse postpone its own
        // recovery indefinitely.
        var counter = NewRedisCounter();
        var key = _prefix + "ttl";
        var db = RedisTestServer.Require().GetDatabase();

        await counter.IncrementAsync(key, 100);
        var initialTtl = await db.KeyTimeToLiveAsync(key);

        for (var i = 0; i < 5; i++)
        {
            await counter.IncrementAsync(key, 100);
        }

        var laterTtl = await db.KeyTimeToLiveAsync(key);

        initialTtl.Should().NotBeNull();
        laterTtl.Should().NotBeNull();
        laterTtl!.Value.Should().BeLessThanOrEqualTo(initialTtl!.Value);
    }

    [SkippableFact]
    public async Task IncrementAsync_SharesStateAcrossInstances()
    {
        // Two service instances against one Redis must see one counter, not two.
        var key = _prefix + "shared";
        var first = NewRedisCounter();
        var second = NewRedisCounter();

        await first.IncrementAsync(key, 60);
        await second.IncrementAsync(key, 60);

        (await first.ReadAsync(key)).Should().Be(2);
    }

    [Fact]
    public async Task RedisCounter_WhenTheServerIsUnreachable_FailsOpen()
    {
        // Returning zero means "no evidence of a breach", so the caller admits the request —
        // a cache outage must not become a data-plane outage.
        var options = ConfigurationOptions.Parse("127.0.0.1:1");
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 200;
        options.SyncTimeout = 200;

        using var dead = await ConnectionMultiplexer.ConnectAsync(options);
        var counter = new RedisFixedWindowCounter(dead, NullLogger.Instance);

        (await counter.IncrementAsync("unreachable", 60)).Should().Be(0);
    }

    [Fact]
    public async Task MemoryCounter_CountsWithinOneProcess()
    {
        var counter = new MemoryFixedWindowCounter(new MemoryCache(new MemoryCacheOptions()));

        (await counter.IncrementAsync("k", 60)).Should().Be(1);
        (await counter.IncrementAsync("k", 60)).Should().Be(2);
        (await counter.ReadAsync("k")).Should().Be(2);
        (await counter.ReadAsync("other")).Should().Be(0);
    }

    [Fact]
    public async Task MemoryCounter_ConcurrentIncrements_LoseNothing()
    {
        var counter = new MemoryFixedWindowCounter(new MemoryCache(new MemoryCacheOptions()));

        await Task.WhenAll(Enumerable.Range(0, 500)
            .Select(_ => Task.Run(() => counter.IncrementAsync("k", 60))));

        (await counter.ReadAsync("k")).Should().Be(500);
    }

    private RedisFixedWindowCounter NewRedisCounter() =>
        new(RedisTestServer.Require(), NullLogger.Instance);
}
