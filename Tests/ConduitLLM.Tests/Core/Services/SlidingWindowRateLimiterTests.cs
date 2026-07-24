using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Redis-free coverage of the limiter's shaping logic. The Lua script itself is exercised
/// against a real server by the Redis-backed limiter tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "SlidingWindowRateLimiter")]
public sealed class SlidingWindowRateLimiterTests
{
    [Fact]
    public async Task CheckAsync_NoWindows_AllowsWithoutTouchingRedis()
    {
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var limiter = new SlidingWindowRateLimiter(redis.Object, NullLogger.Instance);

        var result = await limiter.CheckAsync(Array.Empty<RateLimitWindow>(), 1_000);

        Assert.True(result.IsAllowed);
        Assert.False(result.Degraded);
        Assert.Empty(result.Windows);
        redis.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_RedisThrows_AllowsAndFlagsDegraded()
    {
        var limiter = new SlidingWindowRateLimiter(ThrowingRedis(), NullLogger.Instance);

        var result = await limiter.CheckAsync(
            new[] { new RateLimitWindow("rate:vk:abc:rpm", "RPM", 60_000, 600) },
            1_700_000_000_000);

        // Fail-open is the default; the caller decides whether to honour it (#1215).
        Assert.True(result.IsAllowed);
        Assert.True(result.Degraded);
        Assert.Null(result.EntryId);

        var window = Assert.Single(result.Windows);
        Assert.Equal("RPM", window.Scope);
        Assert.Equal(600, window.Limit);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_060_000).UtcDateTime,
            window.ResetsAt);
    }

    [Fact]
    public async Task ReconcileAsync_UnchangedWeight_IsANoOp()
    {
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var limiter = new SlidingWindowRateLimiter(redis.Object, NullLogger.Instance);

        Assert.False(await limiter.ReconcileAsync("rate:vk:abc:tpm", "entry-1", 500, 500));
        redis.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_RedisThrows_ReportsFailureWithoutBubbling()
    {
        var limiter = new SlidingWindowRateLimiter(ThrowingRedis(), NullLogger.Instance);

        Assert.False(await limiter.ReconcileAsync("rate:vk:abc:tpm", "entry-1", 500, 120));
    }

    [Fact]
    public async Task ReleaseAsync_RedisThrows_ReportsFailureWithoutBubbling()
    {
        var limiter = new SlidingWindowRateLimiter(ThrowingRedis(), NullLogger.Instance);

        Assert.False(await limiter.ReleaseAsync("rate:vk:abc:concurrency", "slot-1", 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ReleaseAsync_WithoutEntryId_DoesNothing(string? entryId)
    {
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        var limiter = new SlidingWindowRateLimiter(redis.Object, NullLogger.Instance);

        Assert.False(await limiter.ReleaseAsync("rate:vk:abc:concurrency", entryId!, 1));
        redis.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void TightestWindow_PicksTheLeastHeadroom()
    {
        var result = new MultiWindowRateLimitResult
        {
            IsAllowed = true,
            Windows = new[]
            {
                new RateLimitWindowState { Scope = "RPM", Current = 10, Limit = 600 },
                new RateLimitWindowState { Scope = "group:RPM", Current = 4_995, Limit = 5_000 },
                new RateLimitWindowState { Scope = "RPD", Current = 100, Limit = 10_000 }
            }
        };

        Assert.Equal("group:RPM", result.TightestWindow!.Scope);
        Assert.Equal(5, result.TightestWindow.Remaining);
    }

    [Fact]
    public void Remaining_NeverGoesNegative()
    {
        // A weighted window can be admitted at exactly its ceiling, and reconciliation can
        // briefly leave the total above it — clients must never see a negative remaining.
        var state = new RateLimitWindowState { Scope = "TPM", Current = 120_000, Limit = 100_000 };

        Assert.Equal(0, state.Remaining);
    }

    private static IConnectionMultiplexer ThrowingRedis()
    {
        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "redis is down"));
        return redis.Object;
    }
}
