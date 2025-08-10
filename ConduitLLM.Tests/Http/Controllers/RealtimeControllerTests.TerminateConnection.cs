using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

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
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Invalid virtual key", error.error.ToString());
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
            dynamic error = notFoundResult.Value;
            Assert.Equal("Connection not found or not owned by this key", error.error.ToString());
        }

        #endregion
    }
}