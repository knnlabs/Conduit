using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Metrics;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Gateway.Hubs;

public sealed class WebhookDeliveryHubTests : HubTestBase
{
    private readonly Mock<IWebhookConnectionTracker> _tracker = new();

    public WebhookDeliveryHubTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async Task UnsubscribeFromWebhooks_RemovesTrackerEntriesAndSignalRGroups()
    {
        var hub = new WebhookDeliveryHub(
            (SignalRMetrics)RealMetrics,
            Mock.Of<ILogger<WebhookDeliveryHub>>(),
            MockServiceProvider.Object,
            _tracker.Object);
        var context = CreateHubCallerContext();
        typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
        typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
        var urls = new[] { "https://one.example/webhook", "https://two.example/webhook" };

        await hub.UnsubscribeFromWebhooks(urls);

        _tracker.Verify(service => service.RemoveWebhooksFromConnectionAsync(
            DefaultConnectionId,
            It.Is<IEnumerable<string>>(items => items.SequenceEqual(urls))), Times.Once);
        foreach (var url in urls)
        {
            VerifyRemovedFromGroup(SignalRConstants.Groups.Webhook(url));
        }
    }
}
