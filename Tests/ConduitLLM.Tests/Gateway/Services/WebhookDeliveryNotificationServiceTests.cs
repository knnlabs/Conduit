using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Core.Constants;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Services;

public sealed class WebhookDeliveryNotificationServiceTests
{
    [Fact]
    public async Task NotifyDeliveryAttemptAsync_SendsAttemptToWebhookGroup()
    {
        const string webhookUrl = "https://example.test/webhook";
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(client => client.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hubClients = new Mock<IHubClients>();
        hubClients
            .Setup(clients => clients.Group(SignalRConstants.Groups.Webhook(webhookUrl)))
            .Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<WebhookDeliveryHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(hubClients.Object);
        var service = new WebhookDeliveryNotificationService(
            hubContext.Object,
            Mock.Of<IServiceProvider>(),
            Mock.Of<ILogger<WebhookDeliveryNotificationService>>());

        await service.NotifyDeliveryAttemptAsync(
            webhookUrl, "task-1", "video", "completed", attemptNumber: 2);

        clientProxy.Verify(client => client.SendCoreAsync(
            "DeliveryAttempted",
            It.Is<object?[]>(arguments => IsExpectedAttempt(arguments, webhookUrl)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static bool IsExpectedAttempt(object?[] arguments, string webhookUrl)
    {
        return arguments.Length == 1 &&
               arguments[0] is WebhookDeliveryAttempt attempt &&
               attempt.TaskId == "task-1" &&
               attempt.AttemptNumber == 2 &&
               attempt.Url == webhookUrl;
    }
}
