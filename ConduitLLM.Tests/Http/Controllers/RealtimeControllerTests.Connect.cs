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
using ConduitLLM.Configuration.DTOs;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Http.Controllers
{
    public partial class RealtimeControllerTests
    {
        #region Connect Tests

        [Fact]
        public async Task Connect_WithoutWebSocketRequest_ShouldReturnBadRequest()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            
            // Setup non-websocket request
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(false);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.Connect(model);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Equal("WebSocket connection required", errorResponse.error.ToString());
        }

        [Fact]
        public async Task Connect_WithoutVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            
            // Setup websocket context without auth headers
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Act
            var result = await _controller.Connect(model);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Virtual key required", errorResponse.error.ToString());
        }

        [Fact]
        public async Task Connect_WithInvalidVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_invalid_key";
            
            // Setup websocket context with auth header
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.Connect(model);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(unauthorizedResult.Value);
            Assert.Equal("Invalid virtual key", errorResponse.error.ToString());
        }

        [Fact]
        public async Task Connect_WithoutRealtimePermissions_ShouldReturnForbidden()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4,gpt-3.5-turbo" // No realtime models
            };
            
            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(keyEntity);

            // Act
            var result = await _controller.Connect(model);

            // Assert
            var forbiddenResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbiddenResult.StatusCode);
            var errorResponse = Assert.IsType<ErrorResponseDto>(forbiddenResult.Value);
            Assert.Equal("Virtual key does not have real-time audio permissions", errorResponse.error.ToString());
        }

        [Fact]
        public async Task Connect_WithValidCredentials_ShouldEstablishConnection()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_valid_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4o-realtime-preview,gpt-4"
            };

            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);

            var mockWebSocket = new Mock<WebSocket>();
            mockWebSocketManager.Setup(x => x.AcceptWebSocketAsync())
                .ReturnsAsync(mockWebSocket.Object);

            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.RegisterConnectionAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<WebSocket>()))
                .Returns(Task.CompletedTask);

            _mockProxyService.Setup(x => x.HandleConnectionAsync(
                It.IsAny<string>(),
                It.IsAny<WebSocket>(),
                It.IsAny<VirtualKey>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Connect(model);

            // Assert
            Assert.IsType<EmptyResult>(result);
        }

        #endregion

        #region Helper Methods

        private HttpContext CreateHttpContextWithWebSockets(WebSocketManager webSocketManager)
        {
            var httpContext = new DefaultHttpContext();
            var mockFeature = new Mock<IHttpWebSocketFeature>();
            mockFeature.Setup(x => x.IsWebSocketRequest).Returns(webSocketManager.IsWebSocketRequest);
            httpContext.Features.Set<IHttpWebSocketFeature>(mockFeature.Object);

            // Set up the WebSocketManager through reflection or mocking
            var webSocketManagerProperty = typeof(HttpContext).GetProperty("WebSockets");
            if (webSocketManagerProperty != null && webSocketManagerProperty.CanWrite)
            {
                webSocketManagerProperty.SetValue(httpContext, webSocketManager);
            }

            return httpContext;
        }

        #endregion
    }
}