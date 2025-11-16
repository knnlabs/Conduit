using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Http.Controllers;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class VideosControllerTests
    {
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
            Assert.Equal("The requested task was not found", problemDetails.Detail);
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
    }
}