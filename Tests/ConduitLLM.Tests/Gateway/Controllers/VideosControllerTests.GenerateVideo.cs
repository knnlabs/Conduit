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

            // Token generation removed - using ephemeral keys

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
            // SignalRToken removed - clients use ephemeral keys
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
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            var problemDetails = unauthorizedResult.Value.Should().BeOfType<ProblemDetails>().Subject;
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
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
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
            var forbiddenResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(403, forbiddenResult.StatusCode);
            var problemDetails = forbiddenResult.Value.Should().BeOfType<ProblemDetails>().Subject;
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
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
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
            var internalServerErrorResult = result.Should().BeOfType<ObjectResult>().Subject;
            Assert.Equal(500, internalServerErrorResult.StatusCode);
            var problemDetails = internalServerErrorResult.Value.Should().BeOfType<ProblemDetails>().Subject;
            Assert.Equal("Internal Server Error", problemDetails.Title);
        }

        #endregion
    }
}