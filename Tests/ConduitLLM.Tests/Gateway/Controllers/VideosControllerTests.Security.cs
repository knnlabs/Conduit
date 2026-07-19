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
        #region Security Tests

        [Fact]
        public async Task GetTaskStatus_WhenUserDoesNotOwnTask_ShouldReturn404()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Progress = 100,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = "video-url-123",
                Metadata = new TaskMetadata(456) // Different virtual key ID
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123") // Different from task owner
                }, "Test"));

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);

            // Verify security logging
            _mockLogger.Verify(x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("attempted to access task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task GetTaskStatus_WithNullMetadata_ShouldReturn404()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Progress = 100,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = "video-url-123",
                Metadata = null // No metadata - should be treated as unauthorized
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
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);
        }

        [Fact]
        public async Task GetTaskStatus_WithInvalidVirtualKeyId_ShouldReturn401()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "invalid") // Invalid ID
                }, "Test"));

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(401, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("Virtual key not found in request context", errorResponse.Error.Message);
        }

        [Fact]
        public async Task RetryTask_WhenUserDoesNotOwnTask_ShouldReturn404()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Failed,
                IsRetryable = true,
                RetryCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                Metadata = new TaskMetadata(456) // Different virtual key ID
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123") // Different from task owner
                }, "Test"));

            // Act
            var result = await _controller.RetryTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);

            // Verify security logging
            _mockLogger.Verify(x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("attempted to retry task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task CancelTask_WhenUserDoesNotOwnTask_ShouldReturn404()
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
                Metadata = new TaskMetadata(456) // Different virtual key ID
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123") // Different from task owner
                }, "Test"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);

            // Verify security logging
            _mockLogger.Verify(x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("attempted to cancel task")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task RetryTask_WithValidOwnership_ShouldAllowRetry()
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
                    new System.Security.Claims.Claim("VirtualKeyId", "123") // Same as task owner
                }, "Test"));

            // Act
            var result = await _controller.RetryTask(taskId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<VideoGenerationTaskStatus>().Subject;
            Assert.Equal(taskId, response.TaskId);
            Assert.Equal(TaskStateConstants.Pending, response.Status);
        }

        [Fact]
        public async Task CancelTask_WithValidOwnership_ShouldAllowCancellation()
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
                Metadata = new TaskMetadata(123) // Same virtual key ID as in claims
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockTaskRegistry.Setup(x => x.TryCancel(taskId))
                .Returns(true);

            _mockTaskService.Setup(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123") // Same as task owner
                }, "Test"));

            // Act
            var result = await _controller.CancelTask(taskId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
            _mockTaskRegistry.Verify(x => x.TryCancel(taskId), Times.Once);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
