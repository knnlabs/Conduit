using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Service-level coverage for the HTTP data-plane limiter: which window denies, what the
/// allowed response reports, and — the point of #1206 — that the reported reset instant comes
/// from the rolling window rather than a calendar boundary.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "RedisVirtualKeyRateLimitService")]
public class RedisVirtualKeyRateLimitServiceTests
{
    private const string KeyHash = "vk-hash";
    private static readonly string RpmKey = RedisKeys.RateLimit.VirtualKeyRpm(KeyHash);
    private static readonly string RpdKey = RedisKeys.RateLimit.VirtualKeyRpd(KeyHash);

    private readonly Mock<ISlidingWindowRateLimiter> _window = new(MockBehavior.Strict);
    private readonly RedisVirtualKeyRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public RedisVirtualKeyRateLimitServiceTests()
    {
        _service = new RedisVirtualKeyRateLimitService(
            Mock.Of<IConnectionMultiplexer>(),
            Mock.Of<ILogger<RedisVirtualKeyRateLimitService>>(),
            _window.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_BothLimitsConfigured_SubmitsBothWindowsAtomically()
    {
        SetupWindows(Allow(("RPM", 3, 600), ("RPD", 40, 1000)));

        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(600, 1000));

        Assert.True(result.IsAllowed);
        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Once);
        Assert.NotNull(_submitted);
        Assert.Equal(new[] { RpmKey, RpdKey }, _submitted!.Select(w => w.Key));
        Assert.All(_submitted!, w => Assert.True(w.UnitWeight));
        Assert.All(_submitted!, w => Assert.Equal(1, w.Weight));
    }

    [Fact]
    public async Task CheckRateLimitAsync_DailyWindowDenies_ReportsRollingWindowExpiry()
    {
        // A key exhausted at 23:50 UTC used to be told to retry at midnight — 10 minutes —
        // when the rolling window would not free capacity for another ~24 hours.
        var freesAt = new DateTime(2026, 7, 25, 9, 15, 42, DateTimeKind.Utc);
        SetupWindows(_ => new MultiWindowRateLimitResult
        {
            IsAllowed = false,
            Windows = new[] { State("RPD", 1000, 1000, freesAt) },
            DeniedWindow = State("RPD", 1000, 1000, freesAt)
        });

        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(null, 1000));

        Assert.False(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(freesAt, result.ResetsAt);
    }

    [Fact]
    public async Task CheckRateLimitAsync_MinuteWindowDenies_ReportsOldestEntryExpiry()
    {
        // Not "now + 60s": the oldest of the 600 in-window requests may age out in 3 seconds.
        var freesAt = DateTime.UtcNow.AddSeconds(3);
        SetupWindows(_ => new MultiWindowRateLimitResult
        {
            IsAllowed = false,
            Windows = new[] { State("RPM", 600, 600, freesAt) },
            DeniedWindow = State("RPM", 600, 600, freesAt)
        });

        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(600, null));

        Assert.Equal(freesAt, result.ResetsAt);
        Assert.True((result.ResetsAt - DateTime.UtcNow).TotalSeconds < 30);
    }

    [Fact]
    public async Task CheckRateLimitAsync_Allowed_ReportsTightestWindow()
    {
        SetupWindows(Allow(("RPM", 2, 600), ("RPD", 998, 1000)));

        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(600, 1000));

        Assert.True(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(1000, result.Limit);
        Assert.Equal(2, result.RequestsRemaining);
    }

    [Fact]
    public async Task CheckRateLimitAsync_RedisUnavailable_AllowsAndFlagsDegraded()
    {
        SetupWindows(windows => new MultiWindowRateLimitResult
        {
            IsAllowed = true,
            Degraded = true,
            Windows = windows.Select(w => State(w.Scope, 0, w.Limit)).ToArray()
        });

        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(600, null));

        Assert.True(result.IsAllowed);
        Assert.True(result.Degraded);
    }

    [Fact]
    public async Task CheckRateLimitAsync_NoLimitsConfigured_AllowsWithoutTouchingRedis()
    {
        var result = await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(null, null));

        Assert.True(result.IsAllowed);
        Assert.Equal(int.MaxValue, result.RequestsRemaining);
        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckRateLimitAsync_MissingKeyHash_Throws(string? keyHash)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CheckRateLimitAsync(keyHash!, new RequestRateLimits(10, null)));
    }

    private void SetupWindows(Func<IReadOnlyList<RateLimitWindow>, MultiWindowRateLimitResult> factory)
    {
        _window
            .Setup(x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()))
            .ReturnsAsync((IReadOnlyList<RateLimitWindow> windows, long _, string? _) =>
            {
                _submitted = windows;
                return factory(windows);
            });
    }

    private static Func<IReadOnlyList<RateLimitWindow>, MultiWindowRateLimitResult> Allow(
        params (string Scope, long Current, long Limit)[] states) =>
        _ => new MultiWindowRateLimitResult
        {
            IsAllowed = true,
            EntryId = "entry",
            Windows = states.Select(s => State(s.Scope, s.Current, s.Limit)).ToArray()
        };

    private static RateLimitWindowState State(string scope, long current, long limit, DateTime? resetsAt = null) =>
        new()
        {
            Scope = scope,
            Current = current,
            Limit = limit,
            ResetsAt = resetsAt ?? DateTime.UtcNow.AddSeconds(45)
        };
}
