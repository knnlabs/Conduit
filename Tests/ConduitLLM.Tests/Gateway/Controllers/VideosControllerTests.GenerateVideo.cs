using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
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

            _mockTaskService.Setup(x => x.CreateTaskAsync(
                    "video_generation",
                    123,
                    It.IsAny<TaskMetadata>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(taskId);

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
            var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
            var taskResponse = acceptedResult.Value.Should().BeOfType<VideoGenerationTaskResponse>().Subject;
            Assert.Equal(taskId, taskResponse.TaskId);
            Assert.Equal(TaskStateConstants.Pending, taskResponse.Status);
            Assert.Contains(taskId, taskResponse.CheckStatusUrl);
            _mockTaskRegistry.Verify(x => x.RegisterTask(taskId, It.IsAny<CancellationTokenSource>()), Times.Once);
            _mockEventBus.Verify(x => x.PublishAsync(
                It.IsAny<VideoGenerationRequested>(),
                It.IsAny<CancellationToken>()), Times.Once);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(401, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("Virtual key not found in request context", errorResponse.Error.Message);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithEmptyPrompt_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "",
                Model = "runway-ml"
            };

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = "condt_test_key";
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("Prompt is required", errorResponse.Error.Message);
            _mockTaskService.Verify(x => x.CreateTaskAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<TaskMetadata>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithEmptyModel_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = ""
            };

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = "condt_test_key";
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("Model is required", errorResponse.Error.Message);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithInvalidDuration_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "runway-ml",
                Duration = 999
            };

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = "condt_test_key";
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Contains("Duration must be between", errorResponse.Error.Message);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithInvalidFps_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new VideoGenerationRequest
            {
                Prompt = "Test prompt",
                Model = "runway-ml",
                Fps = 999
            };

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = "condt_test_key";
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Contains("FPS must be between", errorResponse.Error.Message);
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

            _mockTaskService.Setup(x => x.CreateTaskAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<TaskMetadata>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Internal error"));

            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Items["VirtualKey"] = "condt_test_key";
            _controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("VirtualKeyId", "123")
                }, "Test"));

            // Act
            var result = await _controller.GenerateVideoAsync(request);

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("server_error", errorResponse.Error.Type);
        }

        #endregion
    }
}
