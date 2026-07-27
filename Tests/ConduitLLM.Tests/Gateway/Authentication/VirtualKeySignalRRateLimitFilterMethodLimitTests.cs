using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Authentication;

namespace ConduitLLM.Tests.Gateway.Authentication;

[Trait("Category", "Unit")]
[Trait("Component", "VirtualKeySignalRRateLimitFilter")]
public class VirtualKeySignalRRateLimitFilterMethodLimitTests
{
    [Fact]
    public async Task InvokeMethodAsync_WithConfiguredLimits_ThrottlesRepeatedInvocation()
    {
        const string virtualKeyHash = "rate-limited-key";
        var items = new Dictionary<object, object?>
        {
            ["VirtualKeyId"] = 42,
            ["VirtualKeyHash"] = virtualKeyHash,
            ["VirtualKey.RateLimitRpm"] = 1,
            ["VirtualKey.RateLimitRpd"] = 100
        };
        var callerContext = new Mock<HubCallerContext>();
        callerContext.Setup(x => x.Items).Returns(items);
        callerContext.Setup(x => x.Features).Returns(new FeatureCollection());

        var rateLimitService = new Mock<ISignalRRateLimitService>();
        rateLimitService
            .SetupSequence(x => x.CheckMethodInvocationAsync(virtualKeyHash, 1, 100))
            .ReturnsAsync(new SignalRRateLimitResult { IsAllowed = true })
            .ReturnsAsync(new SignalRRateLimitResult
            {
                IsAllowed = false,
                DenialReason = "Rate limit exceeded. Please try again later.",
                Limit = 1,
                LimitType = "RPM",
                ResetsAt = DateTime.UtcNow.AddMinutes(1)
            });

        var filter = new VirtualKeySignalRRateLimitFilter(
            rateLimitService.Object,
            Mock.Of<ILogger<VirtualKeySignalRRateLimitFilter>>(),
            Options.Create(new SignalRConnectionOptions()));
        var invocation = CreateInvocation(callerContext.Object);
        var nextCallCount = 0;

        await filter.InvokeMethodAsync(invocation, _ =>
        {
            nextCallCount++;
            return ValueTask.FromResult<object?>(null);
        });
        var exception = await Assert.ThrowsAsync<HubException>(async () =>
            await filter.InvokeMethodAsync(invocation, _ =>
            {
                nextCallCount++;
                return ValueTask.FromResult<object?>(null);
            }));

        Assert.Equal("Rate limit exceeded. Please try again later.", exception.Message);
        Assert.Equal(1, nextCallCount);
        rateLimitService.Verify(
            x => x.CheckMethodInvocationAsync(virtualKeyHash, 1, 100),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InvokeMethodAsync_WithoutConfiguredLimits_DoesNotCallLimiter()
    {
        var items = new Dictionary<object, object?>
        {
            ["VirtualKeyHash"] = "unlimited-key"
        };
        var callerContext = new Mock<HubCallerContext>();
        callerContext.Setup(x => x.Items).Returns(items);
        callerContext.Setup(x => x.Features).Returns(new FeatureCollection());
        var rateLimitService = new Mock<ISignalRRateLimitService>();
        var filter = new VirtualKeySignalRRateLimitFilter(
            rateLimitService.Object,
            Mock.Of<ILogger<VirtualKeySignalRRateLimitFilter>>(),
            Options.Create(new SignalRConnectionOptions()));

        var nextCalled = false;
        await filter.InvokeMethodAsync(CreateInvocation(callerContext.Object), _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(null);
        });

        Assert.True(nextCalled);
        rateLimitService.Verify(
            x => x.CheckMethodInvocationAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()),
            Times.Never);
    }

    private static HubInvocationContext CreateInvocation(HubCallerContext callerContext)
    {
        var hub = new TestHub();
        var method = typeof(TestHub).GetMethod(nameof(TestHub.Ping))!;
        return new HubInvocationContext(
            callerContext,
            Mock.Of<IServiceProvider>(),
            hub,
            method,
            Array.Empty<object?>());
    }

    private sealed class TestHub : Hub
    {
        public Task Ping() => Task.CompletedTask;
    }
}
