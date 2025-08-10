using System;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class HybridAudioControllerTests
    {
        #region CreateSession Tests

        [Fact]
        public async Task CreateSession_WithValidConfig_ShouldReturnSessionId()
        {
            // Arrange
            var config = new HybridSessionConfig
            {
                SttProvider = "whisper",
                LlmModel = "gpt-4",
                TtsProvider = "elevenlabs",
                SystemPrompt = "Be helpful",
                DefaultVoice = "voice-1"
            };

            var sessionId = "session-" + Guid.NewGuid();
            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(config, It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionId);

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<HybridAudioController.CreateSessionResponse>(okResult.Value);
            Assert.Equal(sessionId, response.SessionId);
        }

        [Fact]
        public async Task CreateSession_WithVirtualKey_ShouldCheckPermissions()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var virtualKey = "vk-test-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            var keyEntity = new VirtualKey
            {
                Id = 1,
                IsEnabled = true,
                KeyHash = "test-hash"
            };

            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync(keyEntity);

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("session-123");

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            Assert.IsType<OkObjectResult>(result);
            _mockVirtualKeyService.Verify(x => x.GetVirtualKeyByKeyValueAsync(virtualKey), Times.Once);
        }

        [Fact]
        public async Task CreateSession_WithInvalidVirtualKey_ShouldReturnForbidden()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var virtualKey = "vk-invalid-key";
            
            _controller.HttpContext.Items["ApiKey"] = virtualKey;
            
            _mockVirtualKeyService.Setup(x => x.GetVirtualKeyByKeyValueAsync(virtualKey))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var forbidResult = Assert.IsType<ForbidResult>(result);
            Assert.Equal("Virtual key is not valid or enabled", forbidResult.AuthenticationSchemes[0]);
        }

        [Fact]
        public async Task CreateSession_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var config = new HybridSessionConfig();
            var errorMessage = "Invalid configuration";

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal(errorMessage, error.error.ToString());
        }

        [Fact]
        public async Task CreateSession_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var config = new HybridSessionConfig();

            _mockHybridAudioService.Setup(x => x.CreateSessionAsync(
                    It.IsAny<HybridSessionConfig>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CreateSession(config);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred creating the session", error.error.ToString());
        }

        #endregion

        #region CloseSession Tests

        [Fact]
        public async Task CloseSession_WithValidSessionId_ShouldReturnNoContent()
        {
            // Arrange
            var sessionId = "session-123";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            _mockHybridAudioService.Verify(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CloseSession_WithArgumentException_ShouldReturnBadRequest()
        {
            // Arrange
            var sessionId = "invalid-session";
            var errorMessage = "Session not found";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(errorMessage));

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            dynamic error = badRequestResult.Value;
            Assert.Equal(errorMessage, error.error.ToString());
        }

        [Fact]
        public async Task CloseSession_WithGeneralException_ShouldReturn500()
        {
            // Arrange
            var sessionId = "session-123";

            _mockHybridAudioService.Setup(x => x.CloseSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Service error"));

            // Act
            var result = await _controller.CloseSession(sessionId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("An error occurred closing the session", error.error.ToString());
        }

        #endregion
    }
}