using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.RateLimiting;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.RateLimiting;

/// <summary>
/// Per-model overrides: resolution of the rule that governs an alias, and its enforcement
/// alongside the key's own ceilings.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "ModelRateLimits")]
public class ModelRateLimitTests
{
    private const string KeyHash = "model-key-hash";

    private readonly Mock<ISlidingWindowRateLimiter> _limiter = new();
    private readonly TokenRateLimitService _service;

    private IReadOnlyList<RateLimitWindow>? _submitted;

    public ModelRateLimitTests()
    {
        _service = new TokenRateLimitService(
            _limiter.Object, new RateLimitOptions(), NullLogger<TokenRateLimitService>.Instance);
    }

    // ---- rule resolution ------------------------------------------------

    [Fact]
    public void Resolve_ExactAliasWinsOverAPrefixRule()
    {
        var rules = ModelRateLimitPolicy.Parse(
            """{"gpt-5*": {"rpm": 500}, "gpt-5-pro": {"rpm": 5}}""");

        ModelRateLimitPolicy.Resolve(rules, "gpt-5-pro")!.Rpm.Should().Be(5);
        ModelRateLimitPolicy.Resolve(rules, "gpt-5-mini")!.Rpm.Should().Be(500);
    }

    [Fact]
    public void Resolve_LongestPrefixWins_RegardlessOfDocumentOrder()
    {
        // Serialisation order must not decide which ceiling applies.
        var rules = ModelRateLimitPolicy.Parse(
            """{"gpt*": {"rpm": 1000}, "gpt-5-mini*": {"rpm": 10}, "gpt-5*": {"rpm": 100}}""");

        ModelRateLimitPolicy.Resolve(rules, "gpt-5-mini-2026")!.Rpm.Should().Be(10);
        ModelRateLimitPolicy.Resolve(rules, "gpt-5-pro")!.Rpm.Should().Be(100);
        ModelRateLimitPolicy.Resolve(rules, "gpt-4o")!.Rpm.Should().Be(1000);
    }

    [Fact]
    public void Resolve_UnknownAlias_YieldsNoOverride()
    {
        // An unmapped model must fall through to the key and group ceilings, not error.
        var rules = ModelRateLimitPolicy.Parse("""{"gpt-5": {"rpm": 5}}""");

        ModelRateLimitPolicy.Resolve(rules, "claude-opus-5").Should().BeNull();
        ModelRateLimitPolicy.Resolve(rules, null).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("{}")]
    public void Parse_AbsentOrMalformedDocument_YieldsNoRules(string? json)
    {
        // A bad document must not make every request to the key fail.
        ModelRateLimitPolicy.Parse(json).Should().BeNull();
    }

    [Fact]
    public void Resolve_RuleWithNeitherCeiling_IsIgnored()
    {
        var rules = ModelRateLimitPolicy.Parse("""{"gpt-5": {}}""");

        ModelRateLimitPolicy.Resolve(rules, "gpt-5").Should().BeNull();
    }

    // ---- enforcement ----------------------------------------------------

    [Fact]
    public async Task ReserveAsync_ModelOverride_AddsPerModelWindowsAlongsideTheKeyCeiling()
    {
        SetupWindows(allowed: true);
        var context = NewContext(keyTpm: 100_000, modelLimits: """{"sora-2": {"rpm": 10, "tpm": 5000}}""");

        await _service.ReserveAsync(context, "sora-2", 900);

        _submitted!.Select(w => w.Key).Should().Equal(
            RedisKeys.RateLimit.VirtualKeyTpm(KeyHash),
            RedisKeys.RateLimit.VirtualKeyModelRpm(KeyHash, "sora-2"),
            RedisKeys.RateLimit.VirtualKeyModelTpm(KeyHash, "sora-2"));

        var modelRpm = _submitted!.Single(w => w.Scope == "model:sora-2:rpm");
        modelRpm.Limit.Should().Be(10);
        modelRpm.Weight.Should().Be(1, "a per-model request ceiling counts requests, not tokens");
        modelRpm.UnitWeight.Should().BeTrue();

        var modelTpm = _submitted!.Single(w => w.Scope == "model:sora-2:tpm");
        modelTpm.Limit.Should().Be(5_000);
        modelTpm.Weight.Should().Be(900);
        modelTpm.UnitWeight.Should().BeFalse();
    }

    [Fact]
    public async Task ReserveAsync_ModelDeniesWhileTheKeyHasRoom_ReportsTheModelScope()
    {
        SetupWindows(allowed: false, deniedScope: "model:sora-2:rpm");
        var context = NewContext(keyTpm: 100_000, modelLimits: """{"sora-2": {"rpm": 10}}""");

        var decision = await _service.ReserveAsync(context, "sora-2", 900);

        decision!.IsAllowed.Should().BeFalse();
        decision.Scope.Should().Be("model:sora-2:rpm");
        decision.Limit.Should().Be(10);
    }

    [Fact]
    public async Task ReserveAsync_ModelWithOnlyARequestCeiling_NeedsNoTokenEstimate()
    {
        // The key has no token ceiling at all, so nothing weighted is submitted.
        SetupWindows(allowed: true);
        var context = NewContext(keyTpm: null, modelLimits: """{"sora-2": {"rpm": 10}}""");

        await _service.ReserveAsync(context, "sora-2", 0);

        var window = _submitted.Should().ContainSingle().Subject;
        window.Scope.Should().Be("model:sora-2:rpm");
        context.Items.Should().NotContainKey(RateLimitContextKeys.TokenReservation,
            "there is no weighted entry to reconcile");
    }

    [Fact]
    public async Task ReserveAsync_ModelNotCoveredByAnyRule_FallsBackToTheKeyCeilingAlone()
    {
        SetupWindows(allowed: true);
        var context = NewContext(keyTpm: 100_000, modelLimits: """{"sora-2": {"rpm": 10}}""");

        await _service.ReserveAsync(context, "gpt-5", 900);

        Assert.Single(_submitted!);
        _submitted!.Single().Scope.Should().Be("TPM");
    }

    [Fact]
    public async Task ReserveAsync_TightestTokenCeilingClampsTheReservation()
    {
        // The model ceiling is tighter than the key's, so it governs the clamp.
        SetupWindows(allowed: true);
        var context = NewContext(keyTpm: 100_000, modelLimits: """{"sora-2": {"tpm": 2000}}""");

        await _service.ReserveAsync(context, "sora-2", 999_999);

        _submitted!.Should().AllSatisfy(w => w.Weight.Should().Be(2_000));
    }

    [Fact]
    public async Task ReconcileAsync_CorrectsEveryWeightedWindowIncludingPerModel()
    {
        SetupWindows(allowed: true, entryId: "entry-9");
        var context = NewContext(keyTpm: 100_000, modelLimits: """{"sora-2": {"tpm": 50000}}""");
        await _service.ReserveAsync(context, "sora-2", 4_000);

        await _service.ReconcileAsync(context, 1_200);

        _limiter.Verify(x => x.ReconcileAsync(
            RedisKeys.RateLimit.VirtualKeyTpm(KeyHash), "entry-9", 4_000, 1_200), Times.Once);
        _limiter.Verify(x => x.ReconcileAsync(
            RedisKeys.RateLimit.VirtualKeyModelTpm(KeyHash, "sora-2"), "entry-9", 4_000, 1_200), Times.Once);
    }

    private static DefaultHttpContext NewContext(int? keyTpm, string? modelLimits)
    {
        var context = new DefaultHttpContext();
        context.Items[RateLimitContextKeys.KeyHash] = KeyHash;
        if (keyTpm.HasValue)
        {
            context.Items[RateLimitContextKeys.Tpm] = keyTpm.Value;
        }

        context.Items[RateLimitContextKeys.ModelRateLimits] = modelLimits;
        return context;
    }

    private void SetupWindows(bool allowed, string? deniedScope = null, string entryId = "entry")
    {
        _limiter
            .Setup(x => x.CheckAsync(It.IsAny<IReadOnlyList<RateLimitWindow>>(), It.IsAny<long>(), It.IsAny<string?>()))
            .ReturnsAsync((IReadOnlyList<RateLimitWindow> windows, long _, string? _) =>
            {
                _submitted = windows;
                var states = windows
                    .Select(w => new RateLimitWindowState
                    {
                        Scope = w.Scope,
                        Current = w.Weight,
                        Limit = w.Limit,
                        ResetsAt = DateTime.UtcNow.AddSeconds(25)
                    })
                    .ToArray();

                return new MultiWindowRateLimitResult
                {
                    IsAllowed = allowed,
                    EntryId = allowed ? entryId : null,
                    Windows = states,
                    DeniedWindow = allowed ? null : states.First(s => s.Scope == deniedScope)
                };
            });

        _limiter
            .Setup(x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<long>()))
            .ReturnsAsync(true);
    }
}
