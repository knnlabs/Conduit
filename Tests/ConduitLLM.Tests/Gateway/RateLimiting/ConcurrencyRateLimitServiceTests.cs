using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.RateLimiting;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.RateLimiting;

[Trait("Category", "Unit")]
[Trait("Component", "ConcurrencyRateLimitService")]
public class ConcurrencyRateLimitServiceTests
{
    private const string KeyHash = "concurrency-key-hash";
    private static readonly string SlotKey = RedisKeys.RateLimit.VirtualKeyConcurrency(KeyHash);

    private readonly Mock<ISlidingWindowRateLimiter> _limiter = new();
    private readonly RateLimitOptions _options = new() { ConcurrencySlotTtlSeconds = 900 };
    private readonly ConcurrencyRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public ConcurrencyRateLimitServiceTests()
    {
        _service = new ConcurrencyRateLimitService(
            _limiter.Object, _options, NullLogger<ConcurrencyRateLimitService>.Instance);
    }

    [Fact]
    public async Task TryAcquireAsync_TakesAUnitSlotWithTheConfiguredLifetime()
    {
        SetupAcquire(allowed: true, current: 3, entryId: "slot-7");
        var context = NewContext(maxParallel: 8);

        var decision = await _service.TryAcquireAsync(context);

        decision!.IsAllowed.Should().BeTrue();
        decision.Slot.Should().Be(new ConcurrencySlot(SlotKey, "slot-7"));

        var window = _submitted.Should().ContainSingle().Subject;
        window.Key.Should().Be(SlotKey);
        window.Limit.Should().Be(8);
        window.Weight.Should().Be(1);
        window.UnitWeight.Should().BeTrue();
        window.WindowMs.Should().Be(900_000,
            "the slot lifetime is what reclaims slots leaked by a node that died mid-request");
    }

    [Fact]
    public async Task TryAcquireAsync_AtTheCeiling_DeniesWithoutASlot()
    {
        SetupAcquire(allowed: false, current: 8);

        var decision = await _service.TryAcquireAsync(NewContext(maxParallel: 8));

        decision!.IsAllowed.Should().BeFalse();
        decision.Slot.Should().BeNull("a denied request holds nothing, so there is nothing to release");
        decision.Limit.Should().Be(8);
        decision.InFlight.Should().Be(8);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task TryAcquireAsync_WithoutACap_DoesNothing(int? maxParallel)
    {
        var decision = await _service.TryAcquireAsync(NewContext(maxParallel));

        decision.Should().BeNull();
        _limiter.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task TryAcquireAsync_LowPriorityKey_CapsTheGroupCeilingUnderTheSaturationScope()
    {
        SetupAcquire(allowed: true, current: 0);
        var context = NewContext(maxParallel: null);
        context.Items[RateLimitContextKeys.GroupId] = 9;
        context.Items[RateLimitContextKeys.GroupMaxParallelRequests] = 10;
        context.Items[RateLimitContextKeys.Priority] = RateLimitSaturationPolicy.LowPriority;

        await _service.TryAcquireAsync(context);

        var window = _submitted.Should().ContainSingle().Subject;
        window.Limit.Should().Be(8, "the default saturation threshold admits low priority to 80% of the group ceiling");
        window.Scope.Should().Be("group:concurrency:saturation");
        window.Key.Should().Be(RedisKeys.RateLimit.GroupConcurrency(9),
            "the capped window must count the group's shared in-flight slots");
    }

    [Fact]
    public async Task TryAcquireAsync_NormalPriorityKey_SeesTheFullGroupCeiling()
    {
        SetupAcquire(allowed: true, current: 0);
        var context = NewContext(maxParallel: null);
        context.Items[RateLimitContextKeys.GroupId] = 9;
        context.Items[RateLimitContextKeys.GroupMaxParallelRequests] = 10;

        await _service.TryAcquireAsync(context);

        var window = _submitted.Should().ContainSingle().Subject;
        window.Limit.Should().Be(10);
        window.Scope.Should().Be("group:concurrency");
    }

    [Fact]
    public async Task TryAcquireAsync_WithoutAnAuthenticatedKey_DoesNothing()
    {
        var context = new DefaultHttpContext();
        context.Items[RateLimitContextKeys.MaxParallelRequests] = 4;

        (await _service.TryAcquireAsync(context)).Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_ReturnsExactlyTheSlotItWasGiven()
    {
        await _service.ReleaseAsync(new ConcurrencySlot(SlotKey, "slot-7"));

        // The unique entry id is what stops a double release from freeing another request's slot.
        _limiter.Verify(x => x.ReleaseAsync(SlotKey, "slot-7", 1), Times.Once);
    }

    private static DefaultHttpContext NewContext(int? maxParallel)
    {
        var context = new DefaultHttpContext();
        context.Items[RateLimitContextKeys.KeyHash] = KeyHash;
        if (maxParallel.HasValue)
        {
            context.Items[RateLimitContextKeys.MaxParallelRequests] = maxParallel.Value;
        }

        return context;
    }

    private void SetupAcquire(bool allowed, long current, string entryId = "slot")
    {
        _limiter
            .Setup(x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()))
            .ReturnsAsync((IReadOnlyList<RateLimitWindow> windows, long _, string? _) =>
            {
                _submitted = windows;
                var state = new RateLimitWindowState
                {
                    Scope = "concurrency",
                    Current = current,
                    Limit = windows[0].Limit,
                    ResetsAt = DateTime.UtcNow.AddMinutes(15)
                };

                return new MultiWindowRateLimitResult
                {
                    IsAllowed = allowed,
                    EntryId = allowed ? entryId : null,
                    Windows = new[] { state },
                    DeniedWindow = allowed ? null : state
                };
            });
    }
}
