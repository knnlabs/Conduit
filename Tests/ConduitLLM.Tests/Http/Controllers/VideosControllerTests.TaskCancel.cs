using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

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

            _mockVideoService.Setup(x => x.CancelVideoGenerationAsync(taskId, virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
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

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockTaskRegistry.Verify(x => x.TryCancel(taskId), Times.Once);
            _mockVideoService.Verify(x => x.CancelVideoGenerationAsync(taskId, virtualKey, It.IsAny<CancellationToken>()), Times.Once);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
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
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);
            Assert.Equal("Cannot Cancel Task", problemDetails.Title);
            Assert.Contains("already completed", problemDetails.Detail);
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
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
        }

        [Fact]
        public async Task CancelTask_WhenCancellationFails_ShouldReturnConflict()
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
                .Returns(false);

            _mockVideoService.Setup(x => x.CancelVideoGenerationAsync(taskId, virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

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
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);
            Assert.Equal("Cancellation Failed", problemDetails.Title);
        }

        #endregion
    }
}