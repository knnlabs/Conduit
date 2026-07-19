using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class VideosControllerTests
    {
        #region CancelTask Tests

        [Fact]
        public async Task CancelTask_WithPendingTask_ShouldReturnNoContent()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Processing,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                Metadata = new TaskMetadata(123)
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockTaskRegistry.Setup(x => x.TryCancel(taskId))
                .Returns(true);

            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // The event is published fire-and-forget on the thread pool
            // (PublishEventFireAndForget); signal completion so the Verify below
            // doesn't race the publish under parallel test load.
            var published = new TaskCompletionSource();
            _mockEventBus.Setup(x => x.PublishAsync(
                    It.IsAny<VideoGenerationCancelled>(),
                    It.IsAny<CancellationToken>()))
                .Callback(() => published.TrySetResult())
                .Returns(Task.CompletedTask);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.CancelTask(taskId);
            await published.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockTaskRegistry.Verify(x => x.TryCancel(taskId), Times.Once);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
            _mockEventBus.Verify(x => x.PublishAsync(
                It.IsAny<VideoGenerationCancelled>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CancelTask_WithCompletedTask_ShouldReturnConflict()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                Metadata = new TaskMetadata(123)
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(409, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Contains("already completed", errorResponse.Error.Message);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CancelTask_WithNonExistentTask_ShouldReturnNotFound()
        {
            // Arrange
            var taskId = "non-existent-task";
            var virtualKey = "condt_test_key_123456";

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AsyncTaskStatus?)null);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);
        }

        #endregion
    }
}
