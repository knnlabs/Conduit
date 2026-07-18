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
        #region GetTaskStatus Tests

        [Fact]
        public async Task GetTaskStatus_WithValidTaskId_ShouldReturnOk()
        {
            // Arrange
            var taskId = "task-video-123";
            var virtualKey = "condt_test_key_123456";

            var videoResponse = new VideoGenerationResponse
            {
                Data = new List<VideoData>
                {
                    new VideoData { Url = "https://example.com/video.mp4" }
                }
            };

            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                State = TaskState.Completed,
                Progress = 100,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                UpdatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Result = videoResponse, // Stored result is deserialized inline by the controller
                Metadata = new TaskMetadata(123) // Same virtual key ID as in claims
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
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<VideoGenerationTaskStatus>().Subject;
            Assert.Equal(taskId, response.TaskId);
            Assert.Equal(TaskStateConstants.Completed, response.Status);
            Assert.Equal(100, response.Progress);
            Assert.NotNull(response.Result);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(404, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("The requested task was not found", errorResponse.Error.Message);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(401, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("Virtual key not found in request context", errorResponse.Error.Message);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("An unexpected error occurred", errorResponse.Error.Message);
        }

        #endregion
    }
}
