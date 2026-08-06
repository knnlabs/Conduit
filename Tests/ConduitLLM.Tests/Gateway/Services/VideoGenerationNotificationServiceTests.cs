using System.Text.Json;

using ConduitLLM.Core.Constants;
using ConduitLLM.Gateway.Hubs;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

using Moq;

namespace ConduitLLM.Tests.Gateway.Services;

public sealed class VideoGenerationNotificationServiceTests
{
    [Fact]
    public async Task Progress_IsPublishedToPublicVideoSubscribers()
    {
        var (service, publicClient) = CreateService();

        await service.NotifyVideoGenerationProgressAsync("task-1", 42, "processing", "Rendering");

        publicClient.Verify(client => client.SendCoreAsync(
            SignalRConstants.ClientMethods.TaskProgress,
            It.Is<object?[]>(arguments => IsProgressPayload(arguments)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Completion_IsPublishedToPublicVideoSubscribers()
    {
        var (service, publicClient) = CreateService();

        await service.NotifyVideoGenerationCompletedAsync(
            "task-1", "https://example.test/video.mp4", TimeSpan.FromSeconds(10), 1.25m);

        publicClient.Verify(client => client.SendCoreAsync(
            SignalRConstants.ClientMethods.TaskCompleted,
            It.Is<object?[]>(arguments => IsCompletionPayload(arguments)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Failure_IsPublishedToPublicVideoSubscribers()
    {
        var (service, publicClient) = CreateService();

        await service.NotifyVideoGenerationFailedAsync("task-1", "Provider failed", false);

        publicClient.Verify(client => client.SendCoreAsync(
            SignalRConstants.ClientMethods.TaskFailed,
            It.Is<object?[]>(arguments => IsFailurePayload(arguments)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (VideoGenerationNotificationService Service, Mock<IClientProxy> PublicClient) CreateService()
    {
        var secureContext = CreateHubContext<VideoGenerationHub>(out _);
        var publicContext = CreateHubContext<PublicVideoGenerationHub>(out var publicClient);

        return (
            new VideoGenerationNotificationService(
                secureContext.Object,
                publicContext.Object,
                Mock.Of<ILogger<VideoGenerationNotificationService>>()),
            publicClient);
    }

    private static Mock<IHubContext<THub>> CreateHubContext<THub>(out Mock<IClientProxy> client)
        where THub : Hub
    {
        client = new Mock<IClientProxy>();
        client
            .Setup(proxy => proxy.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients>();
        clients
            .Setup(hubClients => hubClients.Group(It.IsAny<string>()))
            .Returns(client.Object);

        var context = new Mock<IHubContext<THub>>();
        context.SetupGet(hubContext => hubContext.Clients).Returns(clients.Object);
        return context;
    }

    private static bool IsProgressPayload(object?[] arguments)
    {
        using var document = ParsePayload(arguments);
        return document is not null
            && document.RootElement.GetProperty("taskId").GetString() == "task-1"
            && document.RootElement.GetProperty("status").GetString() == "processing"
            && document.RootElement.GetProperty("progress").GetInt32() == 42
            && document.RootElement.GetProperty("message").GetString() == "Rendering";
    }

    private static bool IsCompletionPayload(object?[] arguments)
    {
        using var document = ParsePayload(arguments);
        return document is not null
            && document.RootElement.GetProperty("taskId").GetString() == "task-1"
            && document.RootElement.GetProperty("videoUrl").GetString()
                == "https://example.test/video.mp4";
    }

    private static bool IsFailurePayload(object?[] arguments)
    {
        using var document = ParsePayload(arguments);
        return document is not null
            && document.RootElement.GetProperty("taskId").GetString() == "task-1"
            && document.RootElement.GetProperty("error").GetString() == "Provider failed";
    }

    private static JsonDocument? ParsePayload(object?[] arguments)
    {
        return arguments.Length == 1 && arguments[0] is not null
            ? JsonDocument.Parse(JsonSerializer.Serialize(arguments[0]))
            : null;
    }
}
