using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Middleware;
using ConduitLLM.Gateway.RateLimiting;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.RateLimiting;

/// <summary>
/// What happens when a limit cannot be evaluated. Failing open is the default and matches the
/// other limiters, but a Redis outage silently disabling all rate limiting is the wrong trade
/// for deployments where a limit is a hard commercial boundary.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "RateLimitFailurePolicy")]
public class RateLimitFailurePolicyTests
{
    [Fact]
    public void ShouldReject_FailOpen_AdmitsTheRequest()
    {
        var policy = NewPolicy(RateLimitFailureMode.Open);

        policy.ShouldReject("RPM", "redis is down").Should().BeFalse();
    }

    [Fact]
    public void ShouldReject_FailClosed_RejectsTheRequest()
    {
        var policy = NewPolicy(RateLimitFailureMode.Closed);

        policy.ShouldReject("RPM", "redis is down").Should().BeTrue();
    }

    [Fact]
    public void ShouldReject_MarksEnforcementDegraded()
    {
        // Degraded enforcement has to be observable; otherwise "the limiter is not running" is
        // something you discover in the invoice.
        var policy = NewPolicy(RateLimitFailureMode.Open);
        policy.IsDegraded.Should().BeFalse();

        policy.ShouldReject("RPM", "redis is down");

        policy.IsDegraded.Should().BeTrue();
    }

    [Fact]
    public void IsDegraded_ClearsOnceTheDebounceWindowPasses()
    {
        var policy = NewPolicy(RateLimitFailureMode.Open, debounceSeconds: 1);
        policy.ShouldReject("RPM", "redis is down");

        Thread.Sleep(1_100);

        policy.IsDegraded.Should().BeFalse("a single blip must not read as an ongoing outage forever");
    }

    private static RateLimitFailurePolicy NewPolicy(RateLimitFailureMode mode, int debounceSeconds = 10) =>
        new(
            new RateLimitOptions { FailureMode = mode, FailureDebounceSeconds = debounceSeconds },
            NullLogger<RateLimitFailurePolicy>.Instance);
}

/// <summary>
/// The failure-mode matrix end to end: store reachable or not, mode open or closed, limits
/// configured or not.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Http")]
public class RateLimitFailureModeMiddlewareTests
{
    private readonly Mock<IVirtualKeyRateLimitService> _rateLimitService = new();
    private readonly Mock<IConcurrencyRateLimitService> _concurrency = new();
    private readonly Mock<IRateLimitFailurePolicy> _failurePolicy = new();
    private bool _nextCalled;

    [Fact]
    public async Task StoreUnreachable_FailOpen_AdmitsTheRequest()
    {
        _failurePolicy.Setup(p => p.ShouldReject(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        _rateLimitService
            .Setup(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<RequestRateLimits>(), It.IsAny<int?>(), It.IsAny<RequestRateLimits>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));

        var ctx = await InvokeAsync(rpm: 10);

        _nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task StoreUnreachable_FailClosed_Returns503NotA429()
    {
        // 503, not 429: the caller has not exceeded anything, the gateway simply cannot tell.
        _failurePolicy.Setup(p => p.ShouldReject(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _rateLimitService
            .Setup(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<RequestRateLimits>(), It.IsAny<int?>(), It.IsAny<RequestRateLimits>()))
            .ThrowsAsync(new InvalidOperationException("redis is down"));

        var ctx = await InvokeAsync(rpm: 10);

        _nextCalled.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        ctx.Response.Headers.ContainsKey("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task DegradedVerdict_FailClosed_Returns503()
    {
        // The store answered but could not be trusted — same policy applies.
        _failurePolicy.Setup(p => p.ShouldReject(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _rateLimitService
            .Setup(s => s.CheckRateLimitAsync(It.IsAny<string>(), It.IsAny<RequestRateLimits>(), It.IsAny<int?>(), It.IsAny<RequestRateLimits>()))
            .ReturnsAsync(new RateLimitCheckResult { IsAllowed = true, Degraded = true, LimitType = "RPM" });

        var ctx = await InvokeAsync(rpm: 10);

        _nextCalled.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task KeyWithNoLimits_FailClosed_IsUnaffected()
    {
        // Fail-closed must not reject traffic that was never limited: an unlimited key takes no
        // round-trip, so there is nothing that could have failed.
        _failurePolicy.Setup(p => p.ShouldReject(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var ctx = await InvokeAsync(rpm: null);

        _nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        _failurePolicy.Verify(p => p.ShouldReject(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private async Task<DefaultHttpContext> InvokeAsync(int? rpm)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/v1/chat/completions";
        ctx.Response.Body = new MemoryStream();
        ctx.Items["VirtualKey.KeyHash"] = "hash-abc";
        if (rpm.HasValue)
        {
            ctx.Items["VirtualKey.RateLimitRpm"] = rpm.Value;
        }

        var middleware = new VirtualKeyRateLimitMiddleware(
            next: _ => { _nextCalled = true; return Task.CompletedTask; },
            rateLimitService: _rateLimitService.Object,
            concurrencyService: _concurrency.Object,
            failurePolicy: _failurePolicy.Object,
            options: new RateLimitOptions(),
            logger: NullLogger<VirtualKeyRateLimitMiddleware>.Instance);

        await middleware.InvokeAsync(ctx);
        return ctx;
    }
}
