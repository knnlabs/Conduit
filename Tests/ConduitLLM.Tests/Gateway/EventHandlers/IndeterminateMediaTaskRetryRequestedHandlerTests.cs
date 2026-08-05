using System.Text.Json;

using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.EventHandlers;
using ConduitLLM.Tests.Messaging;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace ConduitLLM.Tests.Gateway.EventHandlers;

public sealed class IndeterminateMediaTaskRetryRequestedHandlerTests
{
    [Fact]
    public async Task HandleAsync_PreparesAndPublishesPersistedImageRequest()
    {
        var taskService = new Mock<IAsyncTaskService>();
        var persisted = new ImageGenerationRequested
        {
            TaskId = string.Empty,
            VirtualKeyId = 42,
            Request = new ImageGenerationRequest { Prompt = "draw a lighthouse", Model = "image-model" }
        };
        taskService.Setup(service => service.GetTaskStatusAsync("task-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsyncTaskStatus
            {
                TaskId = "task-1",
                TaskType = "image_generation",
                State = TaskState.Indeterminate,
                Metadata = new TaskMetadata(42)
                {
                    CorrelationId = "correlation-1",
                    Payload = JsonSerializer.Serialize(persisted)
                }
            });
        taskService.Setup(service => service.PrepareIndeterminateTaskRetryAsync(
                "task-1", "dispatch-1", "safe after provider check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaTaskRetryPreparation(MediaTaskRetryPreparationStatus.Prepared));
        var handler = new IndeterminateMediaTaskRetryRequestedHandler(
            taskService.Object,
            NullLogger<IndeterminateMediaTaskRetryRequestedHandler>.Instance);
        var context = new TestEventContext();

        await handler.HandleAsync(new IndeterminateMediaTaskRetryRequested
        {
            TaskId = "task-1",
            DispatchId = "dispatch-1",
            Reason = "safe after provider check"
        }, context);

        var published = Assert.IsType<ImageGenerationRequested>(Assert.Single(context.Published));
        Assert.Equal("task-1", published.TaskId);
        Assert.Equal("correlation-1", published.CorrelationId);
        Assert.Equal("draw a lighthouse", published.Request.Prompt);
    }

    [Fact]
    public async Task HandleAsync_InvalidPayloadDoesNotMoveTaskOutOfIndeterminate()
    {
        var taskService = new Mock<IAsyncTaskService>();
        taskService.Setup(service => service.GetTaskStatusAsync("task-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AsyncTaskStatus
            {
                TaskId = "task-1",
                TaskType = "image_generation",
                State = TaskState.Indeterminate,
                Metadata = new TaskMetadata(42) { Payload = "not-json" }
            });
        var handler = new IndeterminateMediaTaskRetryRequestedHandler(
            taskService.Object,
            NullLogger<IndeterminateMediaTaskRetryRequestedHandler>.Instance);

        await Assert.ThrowsAsync<JsonException>(() => handler.HandleAsync(
            new IndeterminateMediaTaskRetryRequested
            {
                TaskId = "task-1",
                DispatchId = "dispatch-1",
                Reason = "safe after provider check"
            }, new TestEventContext()));

        taskService.Verify(service => service.PrepareIndeterminateTaskRetryAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
