using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Logging;

using Moq;

using StackExchange.Redis;

namespace ConduitLLM.Tests.Core.Services;

/// <summary>
/// Hierarchical enforcement: a key's own ceilings and its group's apply together, and the
/// tighter of the two governs.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "RedisVirtualKeyRateLimitService")]
public class RedisVirtualKeyRateLimitServiceGroupTests
{
    private const string KeyHash = "vk-hash";
    private const int GroupId = 77;

    private readonly Mock<ISlidingWindowRateLimiter> _window = new(MockBehavior.Strict);
    private readonly RedisVirtualKeyRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public RedisVirtualKeyRateLimitServiceGroupTests()
    {
        _service = new RedisVirtualKeyRateLimitService(
            Mock.Of<IConnectionMultiplexer>(),
            Mock.Of<ILogger<RedisVirtualKeyRateLimitService>>(),
            _window.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_KeyAndGroupLimits_SubmitsAllFourWindowsInOneCall()
    {
        // Atomicity across scopes is the point: checking key-then-group with separate calls
        // would let a group-denied request consume the key's quota first.
        SetupWindows(allowed: true);

        await _service.CheckRateLimitAsync(
            KeyHash,
            new RequestRateLimits(600, 10_000),
            GroupId,
            new RequestRateLimits(5_000, 100_000));

        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Once);

        Assert.Equal(
            new[]
            {
                RedisKeys.RateLimit.VirtualKeyRpm(KeyHash),
                RedisKeys.RateLimit.VirtualKeyRpd(KeyHash),
                RedisKeys.RateLimit.GroupRpm(GroupId),
                RedisKeys.RateLimit.GroupRpd(GroupId)
            },
            _submitted!.Select(w => w.Key));
    }

    [Fact]
    public async Task CheckRateLimitAsync_GroupDenies_ReportsTheGroupScope()
    {
        SetupWindows(allowed: false, deniedScope: "group:RPM");

        var result = await _service.CheckRateLimitAsync(
            KeyHash,
            new RequestRateLimits(600, null),
            GroupId,
            new RequestRateLimits(5_000, null));

        Assert.False(result.IsAllowed);
        Assert.Equal("group:RPM", result.LimitType);
    }

    [Fact]
    public async Task CheckRateLimitAsync_KeyDeniesWhileGroupHasRoom_ReportsTheKeyScope()
    {
        SetupWindows(allowed: false, deniedScope: "RPM");

        var result = await _service.CheckRateLimitAsync(
            KeyHash,
            new RequestRateLimits(10, null),
            GroupId,
            new RequestRateLimits(5_000, null));

        Assert.False(result.IsAllowed);
        Assert.Equal("RPM", result.LimitType);
    }

    [Fact]
    public async Task CheckRateLimitAsync_KeyWithoutOwnLimits_StillHonoursTheGroupCeiling()
    {
        SetupWindows(allowed: true);

        await _service.CheckRateLimitAsync(
            KeyHash,
            new RequestRateLimits(null, null),
            GroupId,
            new RequestRateLimits(5_000, null));

        Assert.Equal(RedisKeys.RateLimit.GroupRpm(GroupId), Assert.Single(_submitted!).Key);
    }

    [Fact]
    public async Task CheckRateLimitAsync_GroupWithoutLimits_BehavesExactlyAsBefore()
    {
        SetupWindows(allowed: true);

        await _service.CheckRateLimitAsync(
            KeyHash,
            new RequestRateLimits(600, null),
            GroupId,
            new RequestRateLimits(null, null));

        Assert.Equal(RedisKeys.RateLimit.VirtualKeyRpm(KeyHash), Assert.Single(_submitted!).Key);
    }

    [Fact]
    public async Task CheckRateLimitAsync_KeyInNoGroup_NeverBuildsGroupWindows()
    {
        SetupWindows(allowed: true);

        await _service.CheckRateLimitAsync(KeyHash, new RequestRateLimits(600, null));

        Assert.All(_submitted!, w => Assert.DoesNotContain("vkg", w.Key));
    }

    [Fact]
    public async Task CheckRateLimitAsync_NothingConfiguredAtEitherTier_SkipsRedis()
    {
        var result = await _service.CheckRateLimitAsync(
            KeyHash, new RequestRateLimits(null, null), GroupId, new RequestRateLimits(null, null));

        Assert.True(result.IsAllowed);
        _window.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
    }

    private void SetupWindows(bool allowed, string? deniedScope = null)
    {
        _window
            .Setup(x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()))
            .ReturnsAsync((IReadOnlyList<RateLimitWindow> windows, long _, string? _) =>
            {
                _submitted = windows;
                var states = windows
                    .Select(w => new RateLimitWindowState
                    {
                        Scope = w.Scope,
                        Current = 1,
                        Limit = w.Limit,
                        ResetsAt = DateTime.UtcNow.AddSeconds(20)
                    })
                    .ToArray();

                return new MultiWindowRateLimitResult
                {
                    IsAllowed = allowed,
                    EntryId = allowed ? "entry" : null,
                    Windows = states,
                    DeniedWindow = allowed ? null : states.First(s => s.Scope == deniedScope)
                };
            });
    }
}
