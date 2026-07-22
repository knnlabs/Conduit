using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Consumers;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Tests.Messaging;

using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Consumers;

public sealed class WebhookDeliveryConsumerTests
{
    [Fact]
    public async Task HandleAsync_TerminalFailure_NotifiesOnceAndThrowsNonRetryable()
    {
        var fixture = new Fixture();
        fixture.Webhook
            .Setup(service => service.SendTaskCompletionWebhookAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var request = Fixture.Request with { RetryCount = 3 };

        await Assert.ThrowsAsync<NonRetryableMessageException>(() =>
            fixture.Consumer.HandleAsync(request, fixture.Context));

        Assert.Empty(fixture.Context.Scheduled);
        fixture.Webhook.Verify(service => service.SendTaskCompletionWebhookAsync(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Dictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Notifications.Verify(service => service.NotifyDeliveryFailureAsync(
            request.WebhookUrl, request.TaskId, It.IsAny<string>(), null, 4, true), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnexpectedDeliveryError_UsesConsumerManagedRetry()
    {
        var fixture = new Fixture();
        fixture.Webhook
            .Setup(service => service.SendTaskCompletionWebhookAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection reset"));

        await fixture.Consumer.HandleAsync(Fixture.Request, fixture.Context);

        var scheduled = Assert.Single(fixture.Context.Scheduled);
        var retry = Assert.IsType<WebhookDeliveryRequested>(scheduled.Event);
        Assert.Equal(1, retry.RetryCount);
        fixture.Notifications.Verify(service => service.NotifyDeliveryFailureAsync(
            Fixture.Request.WebhookUrl, Fixture.Request.TaskId, It.IsAny<string>(), null, 1, false), Times.Once);
    }

    private sealed class Fixture
    {
        public static WebhookDeliveryRequested Request => new()
        {
            TaskId = "task-1",
            TaskType = "video",
            WebhookUrl = "https://example.test/webhook",
            EventType = WebhookEventType.TaskCompleted,
            PayloadJson = "{}"
        };

        public Mock<IWebhookNotificationService> Webhook { get; } = new();
        public Mock<IWebhookDeliveryTracker> Tracker { get; } = new();
        public Mock<IWebhookCircuitBreaker> CircuitBreaker { get; } = new();
        public Mock<IWebhookDeliveryNotificationService> Notifications { get; } = new();
        public TestEventContext Context { get; } = new();
        public WebhookDeliveryConsumer Consumer { get; }

        public Fixture()
        {
            Tracker.Setup(service => service.IsDeliveredAsync(It.IsAny<string>())).ReturnsAsync(false);
            CircuitBreaker.Setup(service => service.IsOpen(It.IsAny<string>())).Returns(false);
            Consumer = new WebhookDeliveryConsumer(
                Webhook.Object,
                Tracker.Object,
                CircuitBreaker.Object,
                Notifications.Object,
                Mock.Of<ILogger<WebhookDeliveryConsumer>>());
        }
    }
}
