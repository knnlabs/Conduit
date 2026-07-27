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
[Trait("Component", "TokenRateLimitService")]
public class TokenRateLimitServiceTests
{
    private const string KeyHash = "tpm-key-hash";
    private static readonly string TpmKey = RedisKeys.RateLimit.VirtualKeyTpm(KeyHash);

    private readonly Mock<ISlidingWindowRateLimiter> _limiter = new();
    private readonly TokenRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public TokenRateLimitServiceTests()
    {
        _service = new TokenRateLimitService(
            _limiter.Object, new RateLimitOptions(), NullLogger<TokenRateLimitService>.Instance);
    }

    [Fact]
    public async Task ReserveAsync_ChargesTheEstimateAsAWeightedEntry()
    {
        SetupWindow(allowed: true, current: 4_200, limit: 100_000);
        var context = NewContext(tpm: 100_000);

        var decision = await _service.ReserveAsync(context, "gpt-5", 4_200);

        decision!.IsAllowed.Should().BeTrue();
        decision.Scope.Should().Be("TPM");

        var window = _submitted.Should().ContainSingle().Subject;
        window.Key.Should().Be(TpmKey);
        window.Weight.Should().Be(4_200, "the reservation is the estimated token cost, not one request");
        window.UnitWeight.Should().BeFalse("entries in a token window have differing weights");
        window.WindowMs.Should().Be(60_000);
    }

    [Fact]
    public async Task ReserveAsync_RecordsTheReservationForLaterReconciliation()
    {
        SetupWindow(allowed: true, current: 500, limit: 10_000, entryId: "entry-42");
        var context = NewContext(tpm: 10_000);

        await _service.ReserveAsync(context, "gpt-5", 500);

        var reservation = context.Items[RateLimitContextKeys.TokenReservation].Should().BeOfType<TokenReservation>().Subject;
        reservation.EntryId.Should().Be("entry-42");
        reservation.ReservedTokens.Should().Be(500);
        reservation.SettledTokens.Should().BeNull();
    }

    [Fact]
    public async Task ReserveAsync_OverBudget_DeniesAndLeavesNoReservation()
    {
        SetupWindow(allowed: false, current: 99_000, limit: 100_000);
        var context = NewContext(tpm: 100_000);

        var decision = await _service.ReserveAsync(context, "gpt-5", 8_000);

        decision!.IsAllowed.Should().BeFalse();
        decision.Limit.Should().Be(100_000);
        context.Items.Should().NotContainKey(RateLimitContextKeys.TokenReservation,
            "a denied request was never admitted, so there is nothing to reconcile");
    }

    [Fact]
    public async Task ReserveAsync_EstimateLargerThanTheWholeWindow_IsClampedToTheCeiling()
    {
        // Without the clamp such a request could never be admitted, no matter how long the
        // caller waited — a permanent 429 rather than a rate limit.
        SetupWindow(allowed: true, current: 0, limit: 1_000);
        var context = NewContext(tpm: 1_000);

        await _service.ReserveAsync(context, "gpt-5", 50_000);

        _submitted!.Single().Weight.Should().Be(1_000);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task ReserveAsync_WithoutATokenCeiling_DoesNothing(int? tpm)
    {
        var context = NewContext(tpm);

        var decision = await _service.ReserveAsync(context, "gpt-5", 5_000);

        decision.Should().BeNull();
        _limiter.Verify(
            x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task ReserveAsync_LowPriorityKey_CapsTheGroupCeilingUnderTheSaturationScope()
    {
        SetupWindow(allowed: true, current: 0, limit: 8_000);
        var context = NewContext(tpm: null);
        context.Items[RateLimitContextKeys.GroupId] = 5;
        context.Items[RateLimitContextKeys.GroupTpm] = 10_000;
        context.Items[RateLimitContextKeys.Priority] = RateLimitSaturationPolicy.LowPriority;

        await _service.ReserveAsync(context, "gpt-5", 100);

        var window = _submitted.Should().ContainSingle().Subject;
        window.Limit.Should().Be(8_000, "the default saturation threshold admits low priority to 80% of the group ceiling");
        window.Scope.Should().Be("group:TPM:saturation");
        window.Key.Should().Be(RedisKeys.RateLimit.GroupTpm(5), "the capped window must read the group's shared fill");
    }

    [Fact]
    public async Task ReserveAsync_LowPriorityKey_ClampsOversizedEstimatesToTheCappedCeiling()
    {
        SetupWindow(allowed: true, current: 0, limit: 8_000);
        var context = NewContext(tpm: null);
        context.Items[RateLimitContextKeys.GroupId] = 5;
        context.Items[RateLimitContextKeys.GroupTpm] = 10_000;
        context.Items[RateLimitContextKeys.Priority] = RateLimitSaturationPolicy.LowPriority;

        await _service.ReserveAsync(context, "gpt-5", 50_000);

        _submitted!.Single().Weight.Should().Be(8_000,
            "an estimate above the capped ceiling must clamp to it, or the request could never be admitted");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(RateLimitSaturationPolicy.NormalPriority)]
    [InlineData(RateLimitSaturationPolicy.HighPriority)]
    public async Task ReserveAsync_NormalOrHighPriorityKey_SeesTheFullGroupCeiling(int? priority)
    {
        SetupWindow(allowed: true, current: 0, limit: 10_000);
        var context = NewContext(tpm: null);
        context.Items[RateLimitContextKeys.GroupId] = 5;
        context.Items[RateLimitContextKeys.GroupTpm] = 10_000;
        if (priority.HasValue)
        {
            context.Items[RateLimitContextKeys.Priority] = priority.Value;
        }

        await _service.ReserveAsync(context, "gpt-5", 100);

        var window = _submitted.Should().ContainSingle().Subject;
        window.Limit.Should().Be(10_000);
        window.Scope.Should().Be("group:TPM");
    }

    [Fact]
    public async Task ReserveAsync_WithoutAnAuthenticatedKey_DoesNothing()
    {
        var context = new DefaultHttpContext();
        context.Items[RateLimitContextKeys.Tpm] = 10_000;

        (await _service.ReserveAsync(context, "gpt-5", 100)).Should().BeNull();
    }

    [Fact]
    public async Task ReconcileAsync_CorrectsTheReservationDownToActualUsage()
    {
        SetupWindow(allowed: true, current: 9_000, limit: 100_000, entryId: "entry-1");
        var context = NewContext(tpm: 100_000);
        await _service.ReserveAsync(context, "gpt-5", 9_000);

        // Reserved 8k prompt + 1k completion budget; the model answered in 120 tokens.
        await _service.ReconcileAsync(context, 8_120);

        _limiter.Verify(x => x.ReconcileAsync(TpmKey, "entry-1", 9_000, 8_120), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_CalledTwice_AdjustsOnlyOnce()
    {
        // Streaming and typed paths can both reach the usage pipeline; a double adjustment
        // would silently corrupt the window total.
        SetupWindow(allowed: true, current: 5_000, limit: 100_000, entryId: "entry-1");
        var context = NewContext(tpm: 100_000);
        await _service.ReserveAsync(context, "gpt-5", 5_000);

        await _service.ReconcileAsync(context, 1_000);
        await _service.ReconcileAsync(context, 1_000);

        _limiter.Verify(
            x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_WhenTheEstimateWasExact_SkipsTheRoundTrip()
    {
        SetupWindow(allowed: true, current: 2_000, limit: 100_000, entryId: "entry-1");
        var context = NewContext(tpm: 100_000);
        await _service.ReserveAsync(context, "gpt-5", 2_000);

        await _service.ReconcileAsync(context, 2_000);

        _limiter.Verify(
            x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_WithoutAReservation_DoesNothing()
    {
        await _service.ReconcileAsync(NewContext(tpm: 100_000), 1_234);

        _limiter.Verify(
            x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()),
            Times.Never);
    }

    [Fact]
    public async Task ReconcileAsync_NegativeUsage_NeverDrivesTheWindowBelowZero()
    {
        SetupWindow(allowed: true, current: 3_000, limit: 100_000, entryId: "entry-1");
        var context = NewContext(tpm: 100_000);
        await _service.ReserveAsync(context, "gpt-5", 3_000);

        await _service.ReconcileAsync(context, -5);

        _limiter.Verify(x => x.ReconcileAsync(TpmKey, "entry-1", 3_000, 0), Times.Once);
    }

    private static DefaultHttpContext NewContext(int? tpm)
    {
        var context = new DefaultHttpContext();
        context.Items[RateLimitContextKeys.KeyHash] = KeyHash;
        if (tpm.HasValue)
        {
            context.Items[RateLimitContextKeys.Tpm] = tpm.Value;
        }

        return context;
    }

    private void SetupWindow(bool allowed, long current, long limit, string entryId = "entry")
    {
        _limiter
            .Setup(x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()))
            .ReturnsAsync((IReadOnlyList<RateLimitWindow> windows, long _, string? _) =>
            {
                _submitted = windows;
                var state = new RateLimitWindowState
                {
                    Scope = "TPM",
                    Current = current,
                    Limit = limit,
                    ResetsAt = DateTime.UtcNow.AddSeconds(37)
                };

                return new MultiWindowRateLimitResult
                {
                    IsAllowed = allowed,
                    EntryId = allowed ? entryId : null,
                    Windows = new[] { state },
                    DeniedWindow = allowed ? null : state
                };
            });

        _limiter
            .Setup(x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()))
            .ReturnsAsync(true);
    }
}
