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

    private readonly Mock<ISlidingWindowRateLimiter> _window = new(MockBehavior.Strict);
    private readonly RedisSignalRRateLimitService _service;

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
        SetupWindow(RedisKeys.SignalRRateLimit.Rpm(KeyHash), allowed: true, current: 5, limit: 600);
        SetupWindow(RedisKeys.SignalRRateLimit.Rpd(KeyHash), allowed: true, current: 900, limit: 1000);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        VerifyWindowChecked(RedisKeys.SignalRRateLimit.Rpd(KeyHash), 86_400_000, 1000);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_RpmAllowsRpdDenies_DeniesWithDailyLimitType()
    {
        SetupWindow(RedisKeys.SignalRRateLimit.Rpm(KeyHash), allowed: true, current: 1, limit: 600);
        SetupWindow(RedisKeys.SignalRRateLimit.Rpd(KeyHash), allowed: false, current: 1000, limit: 1000);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(1000, result.Limit);
        Assert.Equal(0, result.RequestsRemaining);
        Assert.Equal("Daily rate limit exceeded. Please try again tomorrow.", result.DenialReason);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_RpmDenies_DoesNotConsumeDailyQuota()
    {
        SetupWindow(RedisKeys.SignalRRateLimit.Rpm(KeyHash), allowed: false, current: 600, limit: 600);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPM", result.LimitType);
        _window.Verify(
            x => x.CheckAsync(RedisKeys.SignalRRateLimit.Rpd(KeyHash), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_BothAllow_ReportsMinuteWindowWhenItIsTighter()
    {
        SetupWindow(RedisKeys.SignalRRateLimit.Rpm(KeyHash), allowed: true, current: 598, limit: 600);
        SetupWindow(RedisKeys.SignalRRateLimit.Rpd(KeyHash), allowed: true, current: 10, limit: 1000);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        Assert.Equal("RPM", result.LimitType);
        Assert.Equal(600, result.Limit);
        Assert.Equal(2, result.RequestsRemaining);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_BothAllow_ReportsDailyWindowWhenItIsTighter()
    {
        SetupWindow(RedisKeys.SignalRRateLimit.Rpm(KeyHash), allowed: true, current: 1, limit: 600);
        SetupWindow(RedisKeys.SignalRRateLimit.Rpd(KeyHash), allowed: true, current: 997, limit: 1000);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, 600, 1000);

        Assert.True(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        Assert.Equal(1000, result.Limit);
        Assert.Equal(3, result.RequestsRemaining);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_OnlyRpdConfigured_StillEnforcesDailyLimit()
    {
        SetupWindow(RedisKeys.SignalRRateLimit.Rpd(KeyHash), allowed: false, current: 50, limit: 50);

        var result = await _service.CheckMethodInvocationAsync(KeyHash, null, 50);

        Assert.False(result.IsAllowed);
        Assert.Equal("RPD", result.LimitType);
        _window.Verify(
            x => x.CheckAsync(RedisKeys.SignalRRateLimit.Rpm(KeyHash), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, 0)]
    public async Task CheckMethodInvocationAsync_NoLimitsConfigured_AllowsWithoutTouchingRedis(int? rpm, int? rpd)
    {
        var result = await _service.CheckMethodInvocationAsync(KeyHash, rpm, rpd);

        Assert.True(result.IsAllowed);
        Assert.Equal(string.Empty, result.LimitType);
        _window.Verify(
            x => x.CheckAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckMethodInvocationAsync_EmptyKeyHash_Allows()
    {
        var result = await _service.CheckMethodInvocationAsync(string.Empty, 1, 1);

        Assert.True(result.IsAllowed);
        _window.Verify(
            x => x.CheckAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    private void SetupWindow(string key, bool allowed, int current, int limit)
    {
        _window
            .Setup(x => x.CheckAsync(key, It.IsAny<long>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new SlidingWindowResult { IsAllowed = allowed, Current = current, Limit = limit });
    }

    private void VerifyWindowChecked(string key, int windowMs, int limit)
    {
        _window.Verify(x => x.CheckAsync(key, It.IsAny<long>(), windowMs, limit), Times.Once);
    }
}
