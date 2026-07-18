using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Gateway.Controllers;

using FluentAssertions;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class VideosControllerTests
    {
        #region RetryTask Tests

        [Fact]
        public async Task RetryTask_WithFailedTask_ShouldReturnOk()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var failedTaskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Failed,
                IsRetryable = true,
                RetryCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                Metadata = new TaskMetadata(123) // Same virtual key ID as in claims
            };

            var updatedTaskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Pending,
                IsRetryable = true,
                RetryCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                Metadata = new TaskMetadata(123)
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedTaskStatus);

            _mockTaskService.Setup(x => x.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Pending,
                    It.IsAny<int?>(),
                    It.IsAny<object?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockTaskService.SetupSequence(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedTaskStatus)
                .ReturnsAsync(updatedTaskStatus);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.RetryTask(taskId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<VideoGenerationTaskStatus>().Subject;
            Assert.Equal(taskId, response.TaskId);
            Assert.Equal(TaskStateConstants.Pending, response.Status);
            Assert.Contains("Retry", response.Error);
        }

        [Fact]
        public async Task RetryTask_WithNonFailedTask_ShouldReturnBadRequest()
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
            var result = await _controller.RetryTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Contains("Only failed tasks can be retried", errorResponse.Error.Message);
        }

        [Fact]
        public async Task RetryTask_WithNonRetryableTask_ShouldReturnBadRequest()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Failed,
                IsRetryable = false,
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
            var result = await _controller.RetryTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("This task has been marked as non-retryable", errorResponse.Error.Message);
        }

        [Fact]
        public async Task RetryTask_WithMaxRetriesExceeded_ShouldReturnBadRequest()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Failed,
                IsRetryable = true,
                RetryCount = 3,
                MaxRetries = 3,
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
            var result = await _controller.RetryTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Contains("already been retried", errorResponse.Error.Message);
        }

        #endregion
    }
}
