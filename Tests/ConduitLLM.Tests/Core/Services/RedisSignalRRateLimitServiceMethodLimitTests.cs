using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Service-level coverage for <see cref="RedisSignalRRateLimitService.CheckMethodInvocationAsync"/>.
/// The existing filter tests mock the whole service, which is why the unreachable RPD branch
/// (#1205) went unnoticed — these drive the real service against a fake sliding window instead.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "RedisSignalRRateLimitService")]
public class RedisSignalRRateLimitServiceMethodLimitTests
{
    private const string KeyHash = "signalr-key-hash";
    private static readonly string RpmKey = RedisKeys.SignalRRateLimit.Rpm(KeyHash);
    private static readonly string RpdKey = RedisKeys.SignalRRateLimit.Rpd(KeyHash);

    private readonly Mock<ISlidingWindowRateLimiter> _window = new(MockBehavior.Strict);
    private readonly RedisSignalRRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public RedisSignalRRateLimitServiceMethodLimitTests()
    {
        var database = new Mock<IDatabase>();
        database
            .Setup(x => x.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var redis = new Mock<IConnectionMultiplexer>();
        redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);

        _service = new RedisSignalRRateLimitService(
            redis.Object,
            Mock.Of<ILogger<RedisSignalRRateLimitService>>(),
            _window.Object);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_RpmConfigured_StillEvaluatesDailyWindow()
    {
        // Regression for #1205: the RPM branch used to return before the RPD check ran,
        // so a key under its per-minute ceiling had an unlimited daily allowance.
        SetupWindows(Allow(("RPM", 5, 600), ("RPD", 900, 1000)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        Assert.NotNull(_submitted);
        Assert.Collection(
            _submitted!,
            rpm =>
            {
                Assert.Equal(RpmKey, rpm.Key);
                Assert.Equal(60_000, rpm.WindowMs);
                Assert.Equal(600, rpm.Limit);
            },
            rpd =>
            {
                Assert.Equal(RpdKey, rpd.Key);
                Assert.Equal(86_400_000, rpd.WindowMs);
                Assert.Equal(1000, rpd.Limit);
            });
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_BothWindowsConfigured_EvaluatedInOneAtomicCall()
    {
        // All-or-nothing admission: a daily denial must not have already burned a minute slot,
        // which is only guaranteed when both windows go to Redis in a single script call.
        SetupWindows(Allow(("RPM", 5, 600), ("RPD", 900, 1000)));

        await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_RpmAllowsRpdDenies_DeniesWithDailyLimitType()
    {
        SetupWindows(Deny("RPD", ("RPM", 1, 600), ("RPD", 1000, 1000)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(1000, result.Limit);
        Assert.Equal(0, result.RequestsRemaining);
        Assert.Equal("Daily rate limit exceeded. Please try again tomorrow.", result.DenialReason);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_RpmDenies_ReportsMinuteLimitType()
    {
        SetupWindows(Deny("RPM", ("RPM", 600, 600), ("RPD", 10, 1000)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPM", result.LimitType);
        Assert.Equal("Rate limit exceeded. Please try again later.", result.DenialReason);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_Denied_ReportsWindowExpiryNotCalendarMidnight()
    {
        // #1206: enforcement is a rolling 24h window, so the reset must come from the window,
        // not from DateTime.UtcNow.Date.AddDays(1).
        var windowExpiry = new DateTime(2026, 7, 24, 11, 30, 0, DateTimeKind.Utc);
        SetupWindows(_ => new MultiWindowRateLimitResult
        {
            IsAllowed = false,
            Windows = new[]
            {
                State("RPD", 1000, 1000, windowExpiry)
            },
            DeniedWindow = State("RPD", 1000, 1000, windowExpiry)
        });

        var result = await _service.CheckMethodInvocationAsync(KeyHash, null, 1000);

        Assert.Equal(windowExpiry, result.ResetsAt);
        Assert.NotEqual(DateTime.UtcNow.Date.AddDays(1), result.ResetsAt);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_BothAllow_ReportsMinuteWindowWhenItIsTighter()
    {
        SetupWindows(Allow(("RPM", 598, 600), ("RPD", 10, 1000)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        Assert.Equal("RPM", result.LimitType);
        Assert.Equal(600, result.Limit);
        Assert.Equal(2, result.RequestsRemaining);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_BothAllow_ReportsDailyWindowWhenItIsTighter()
    {
        SetupWindows(Allow(("RPM", 1, 600), ("RPD", 997, 1000)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(1000, result.Limit);
        Assert.Equal(3, result.RequestsRemaining);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_OnlyRpdConfigured_StillEnforcesDailyLimit()
    {
        SetupWindows(Deny("RPD", ("RPD", 50, 50)));

        var result = await _service.CheckMethodInvocationAsync(KeyHash, null, 50);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.NotNull(_submitted);
        Assert.Equal(RpdKey, Assert.Single(_submitted!).Key);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    public async Task CheckMethodInvocationAsync_NoLimitsConfigured_AllowsWithoutTouchingRedis(int? rpm, int? rpd)
    {
        var result = await _service.CheckMethodInvocationAsync(KeyHash, rpm, rpd);

        Assert.True(result.IsAllowed);
        Assert.Equal(string.Empty, result.LimitType);
        VerifyNoWindowCheck();
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_EmptyKeyHash_Allows()
    {
        var result = await _service.CheckMethodInvocationAsync(string.Empty, 1, 1);

        Assert.True(result.IsAllowed);
        VerifyNoWindowCheck();
    }

    private void VerifyNoWindowCheck()
    {
        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
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

    private static Func<IReadOnlyList<RateLimitWindow>, MultiWindowRateLimitResult> Deny(
        string deniedScope,
        params (string Scope, long Current, long Limit)[] states)
    {
        var built = states.Select(s => State(s.Scope, s.Current, s.Limit)).ToArray();
        return _ => new MultiWindowRateLimitResult
        {
            IsAllowed = false,
            Windows = built,
            DeniedWindow = built.First(s => s.Scope == deniedScope)
        };
    }

    private static RateLimitWindowState State(string scope, long current, long limit, DateTime? resetsAt = null) =>
        new()
        {
            Scope = scope,
            Current = current,
            Limit = limit,
            ResetsAt = resetsAt ?? DateTime.UtcNow.AddSeconds(30)
        };
}
