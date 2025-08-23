using ConduitLLM.Configuration.Entities;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ConduitLLM.Configuration.DTOs;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class RealtimeControllerTests
    {
        #region TerminateConnection Tests

        [Fact]
        public async Task TerminateConnection_WithValidConnection_ShouldTerminate()
        {
            // Arrange
            var connectionId = "conn-123";
            var virtualKey = "condt_valid_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4o-realtime-preview"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.TerminateConnectionAsync(connectionId, keyEntity.Id))
                .ReturnsAsync(true);

            // Act
            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task TerminateConnection_WithInvalidKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var connectionId = "conn-123";
            var virtualKey = "condt_invalid_key";

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync((VirtualKey)null);

            // Act
            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorResponse.error.ToString());
        }

        [Fact]
        public async Task TerminateConnection_WithNonExistentConnection_ShouldReturnNotFound()
        {
            // Arrange
            var connectionId = "conn-nonexistent";
            var virtualKey = "condt_valid_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4o-realtime-preview"
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.TerminateConnectionAsync(connectionId, keyEntity.Id))
                .ReturnsAsync(false);

            // Act
            _controller.ControllerContext = CreateControllerContext();
            _controller.ControllerContext.HttpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
            Assert.Equal("Connection not found or not owned by this key", errorResponse.error.ToString());
        }

        #endregion
    }
}