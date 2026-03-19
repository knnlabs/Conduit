using ConduitLLM.Core.Constants;
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
            var acceptedResult = result.Should().BeOfType<AcceptedResult>().Subject;
            var taskResponse = acceptedResult.Value.Should().BeOfType<VideoGenerationTaskResponse>().Subject;
            Assert.Equal(taskId, taskResponse.TaskId);
            Assert.Equal(TaskStateConstants.Pending, taskResponse.Status);
            Assert.Contains(taskId, taskResponse.CheckStatusUrl);
            _mockTaskRegistry.Verify(x => x.RegisterTask(taskId, It.IsAny<CancellationTokenSource>()), Times.Once);
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

            // Assert - ExceptionToResponseMapper maps ArgumentException to 400
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
        }

        [Fact]
        public async Task GenerateVideoAsync_WithUnauthorizedAccessException_ShouldReturnUnauthorized()
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

            // Assert - ExceptionToResponseMapper maps UnauthorizedAccessException to 401
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(401, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
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

            // Assert - ExceptionToResponseMapper maps NotSupportedException to 400
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(400, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("invalid_request_error", errorResponse.Error.Type);
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
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(500, objectResult.StatusCode);
            var errorResponse = objectResult.Value.Should().BeOfType<OpenAIErrorResponse>().Subject;
            Assert.Equal("server_error", errorResponse.Error.Type);
        }

        #endregion
    }
}
