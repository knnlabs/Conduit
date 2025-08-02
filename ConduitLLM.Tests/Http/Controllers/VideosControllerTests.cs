using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class VideosControllerTests : ControllerTestBase
    {
        private readonly Mock<IVideoGenerationService> _mockVideoService;
        private readonly Mock<IAsyncTaskService> _mockTaskService;
        private readonly Mock<IOperationTimeoutProvider> _mockTimeoutProvider;
        private readonly Mock<ICancellableTaskRegistry> _mockTaskRegistry;
        private readonly Mock<ILogger<VideosController>> _mockLogger;
        private readonly VideosController _controller;

        public VideosControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockVideoService = new Mock<IVideoGenerationService>();
            _mockTaskService = new Mock<IAsyncTaskService>();
            _mockTimeoutProvider = new Mock<IOperationTimeoutProvider>();
            _mockTaskRegistry = new Mock<ICancellableTaskRegistry>();
            _mockLogger = CreateLogger<VideosController>();
            var mockModelMappingService = new Mock<ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService>();

            _controller = new VideosController(
                _mockVideoService.Object,
                _mockTaskService.Object,
                _mockTimeoutProvider.Object,
                _mockTaskRegistry.Object,
                _mockLogger.Object,
                mockModelMappingService.Object);

            // Setup default controller context
            _controller.ControllerContext = CreateControllerContext();
        }

        #region GenerateVideoAsync Tests

        [Fact]
        public async Task GenerateVideoAsync_WithValidRequest_ShouldReturnAccepted()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "A beautiful sunset over mountains",
                Model = "runway-ml",
                Duration = 5,
                Size = "1280x720"
            };

            var virtualKey = "condt_test_key_123456";
            var taskId = "task-video-123";
            
            var videoResponse = new VideoGenerationResponse
            {
                Data = new List<VideoData>
                {
                    new VideoData { Url = $"pending:{taskId}" }
                }
            };

            _mockVideoService.Setup(x => x.GenerateVideoWithTaskAsync(
                    It.IsAny<VideoGenerationRequest>(),
                    virtualKey,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(videoResponse);

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var acceptedResult = Assert.IsType<AcceptedResult>(result);
            var taskResponse = Assert.IsType<VideoGenerationTaskResponse>(acceptedResult.Value);
            Assert.Equal(taskId, taskResponse.TaskId);
            Assert.Equal(TaskStateConstants.Pending, taskResponse.Status);
            Assert.Contains(taskId, taskResponse.CheckStatusUrl);
            _mockTaskRegistry.Verify(x => x.RegisterTask(taskId, It.IsAny<CancellationTokenSource>()), Times.Once);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithInvalidModelState_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "",  // Empty prompt to trigger validation
                Model = "runway-ml"
            };
            _controller.ModelState.AddModelError("Prompt", "Prompt is required");

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithoutVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "A beautiful sunset",
                Model = "runway-ml"
            };

            _controller.ControllerContext = CreateControllerContext();

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
            Assert.Equal("Virtual key not found in request context", problemDetails.Detail);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "invalid-model"
            };

            var virtualKey = "condt_test_key_123456";
            
            _mockVideoService.Setup(x => x.GenerateVideoWithTaskAsync(
                    It.IsAny<VideoGenerationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Invalid model specified"));

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Invalid Request", problemDetails.Title);
            Assert.Equal("Invalid model specified", problemDetails.Detail);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithUnauthorizedAccessException_ShouldReturnForbidden()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "runway-ml"
            };

            var virtualKey = "condt_test_key_123456";
            
            _mockVideoService.Setup(x => x.GenerateVideoWithTaskAsync(
                    It.IsAny<VideoGenerationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new UnauthorizedAccessException("Virtual key does not have permission"));

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var forbiddenResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbiddenResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(forbiddenResult.Value);
            Assert.Equal("Forbidden", problemDetails.Title);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithNotSupportedException_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "text-only-model"
            };

            var virtualKey = "condt_test_key_123456";
            
            _mockVideoService.Setup(x => x.GenerateVideoWithTaskAsync(
                    It.IsAny<VideoGenerationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotSupportedException("Model does not support video generation"));

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Not Supported", problemDetails.Title);
            Assert.Equal("Model does not support video generation", problemDetails.Detail);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "runway-ml"
            };

            var virtualKey = "condt_test_key_123456";
            
            _mockVideoService.Setup(x => x.GenerateVideoWithTaskAsync(
                    It.IsAny<VideoGenerationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Internal error"));

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = virtualKey;
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(internalServerErrorResult.Value);
            Assert.Equal("Internal Server Error", problemDetails.Title);
        }

        #endregion

        #region GetTaskStatus Tests

        [Fact]
        public async Task GetTaskStatus_WithValidTaskId_ShouldReturnOk()
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
                Metadata = new TaskMetadata(123) // Same virtual key ID as in claims
            };

            var videoResponse = new VideoGenerationResponse
            {
                Data = new List<VideoData>
                {
                    new VideoData { Url = "https://example.com/video.mp4" }
                }
            };

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskStatus);

            _mockVideoService.Setup(x => x.GetVideoGenerationStatusAsync(taskId, virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(videoResponse);

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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<VideoGenerationTaskStatus>(okResult.Value);
            Assert.Equal(taskId, response.TaskId);
            Assert.Equal(TaskStateConstants.Completed, response.Status);
            Assert.Equal(100, response.Progress);
            Assert.NotNull(response.VideoResponse);
        }

        [Fact]
        public async Task GetTaskStatus_WithNonExistentTask_ShouldReturnNotFound()
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
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
            Assert.Contains(taskId, problemDetails.Detail);
        }

        [Fact]
        public async Task GetTaskStatus_WithoutVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var taskId = "task-video-123";
            _controller.ControllerContext = CreateControllerContext();

            // Act
            var result = await _controller.GetTaskStatus(taskId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
        }

        [Fact]
        public async Task GetTaskStatus_WithException_ShouldReturn500()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            _mockTaskService.Setup(x => x.GetTaskStatusAsync(taskId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

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
            var internalServerErrorResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, internalServerErrorResult.StatusCode);
            var problemDetails = Assert.IsType<ProblemDetails>(internalServerErrorResult.Value);
            Assert.Equal("Internal Server Error", problemDetails.Title);
        }

        #endregion

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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<VideoGenerationTaskStatus>(okResult.Value);
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
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Invalid Task State", problemDetails.Title);
            Assert.Contains("failed tasks can be retried", problemDetails.Detail);
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
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Task Not Retryable", problemDetails.Title);
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
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
            Assert.Equal("Max Retries Exceeded", problemDetails.Title);
            Assert.Contains("already been retried", problemDetails.Detail);
        }

        #endregion

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

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        [Fact]
        public void Controller_ShouldHaveRateLimiting()
        {
            // Arrange & Act
            var controllerType = typeof(VideosController);
            var rateLimitAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute));

            // Assert
            Assert.NotNull(rateLimitAttribute);
            var attr = (Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute)rateLimitAttribute;
            Assert.Equal("VirtualKeyPolicy", attr.PolicyName);
        }

        #endregion

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
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
            Assert.Equal("The requested task was not found", problemDetails.Detail);
            
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
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
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
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
            Assert.Equal("Unauthorized", problemDetails.Title);
            Assert.Equal("Virtual key not found in request context", problemDetails.Detail);
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
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
            Assert.Equal("The requested task was not found", problemDetails.Detail);
            
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
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
            Assert.Equal("Task Not Found", problemDetails.Title);
            Assert.Equal("The requested task was not found", problemDetails.Detail);
            
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
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<VideoGenerationTaskStatus>(okResult.Value);
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

            _mockVideoService.Setup(x => x.CancelVideoGenerationAsync(taskId, virtualKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

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
            Assert.IsType<NoContentResult>(result);
            _mockTaskRegistry.Verify(x => x.TryCancel(taskId), Times.Once);
            _mockVideoService.Verify(x => x.CancelVideoGenerationAsync(taskId, virtualKey, It.IsAny<CancellationToken>()), Times.Once);
            _mockTaskService.Verify(x => x.CancelTaskAsync(taskId, It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}