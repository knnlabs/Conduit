using System.Net;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace ConduitLLM.Tests.Gateway.Endpoints;

public sealed class TasksEndpointsOwnershipTests
{
    [Theory]
    [InlineData("GET", "/v1/conduit/tasks/task_other")]
    [InlineData("GET", "/v1/conduit/tasks/task_other/poll?timeout=1&interval=1")]
    [InlineData("POST", "/v1/conduit/tasks/task_other/cancel")]
    public async Task TaskRoutes_HideTasksOwnedByAnotherVirtualKey(string method, string path)
    {
        var taskService = new Mock<IAsyncTaskService>();
        taskService
            .Setup(service => service.GetTaskStatusAsync(
                "task_other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsyncTaskStatus
            {
                TaskId = "task_other",
                Metadata = new TaskMetadata(virtualKeyId: 2)
            });

        await using var host = await GatewayEndpointTestHost.StartAsync(services =>
            services.AddSingleton(taskService.Object));
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        taskService.Verify(
            service => service.CancelTaskAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        taskService.Verify(
            service => service.PollTaskUntilCompletedAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
