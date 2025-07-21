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
    [Trait("Category", "Unit")]
    [Trait("Component", "Http")]
    [Trait("Phase", "2")]
    public class RealtimeControllerTests : ControllerTestBase
    {
        private readonly Mock<ILogger<RealtimeController>> _mockLogger;
        private readonly Mock<IRealtimeProxyService> _mockProxyService;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IRealtimeConnectionManager> _mockConnectionManager;
        private readonly RealtimeController _controller;

        public RealtimeControllerTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = CreateLogger<RealtimeController>();
            _mockProxyService = new Mock<IRealtimeProxyService>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockConnectionManager = new Mock<IRealtimeConnectionManager>();

            _controller = new RealtimeController(
                _mockLogger.Object,
                _mockProxyService.Object,
                _mockVirtualKeyService.Object,
                _mockConnectionManager.Object);

            _controller.ControllerContext = CreateControllerContext();
        }

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
            dynamic error = badRequestResult.Value;
            Assert.Equal("WebSocket connection required", error.error.ToString());
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
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Virtual key required", error.error.ToString());
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
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Invalid virtual key", error.error.ToString());
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
            dynamic error = forbiddenResult.Value;
            Assert.Equal("Virtual key does not have real-time audio permissions", error.error.ToString());
        }

        [Fact]
        public async Task Connect_WithValidRequest_ShouldEstablishWebSocketConnection()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "gpt-4o-realtime-preview"
            };
            
            var mockWebSocket = new Mock<WebSocket>();
            mockWebSocket.Setup(x => x.State).Returns(WebSocketState.Open);
            
            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            mockWebSocketManager.Setup(x => x.AcceptWebSocketAsync(It.IsAny<string>()))
                .ReturnsAsync(mockWebSocket.Object);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.RegisterConnectionAsync(
                    It.IsAny<string>(),
                    keyEntity.Id,
                    model,
                    It.IsAny<WebSocket>()))
                .Returns(Task.CompletedTask);

            _mockProxyService.Setup(x => x.HandleConnectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<WebSocket>(),
                    keyEntity,
                    model,
                    null,
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _mockConnectionManager.Setup(x => x.UnregisterConnectionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Connect(model);

            // Assert
            Assert.IsType<EmptyResult>(result);
            
            // Verify connection lifecycle
            _mockConnectionManager.Verify(x => x.RegisterConnectionAsync(
                It.IsAny<string>(),
                keyEntity.Id,
                model,
                mockWebSocket.Object), Times.Once);
                
            _mockProxyService.Verify(x => x.HandleConnectionAsync(
                It.IsAny<string>(),
                mockWebSocket.Object,
                keyEntity,
                model,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
                
            _mockConnectionManager.Verify(x => x.UnregisterConnectionAsync(
                It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Connect_WithProviderOverride_ShouldPassProviderToProxy()
        {
            // Arrange
            var model = "ultravox";
            var provider = "fixie";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "ultravox"
            };
            
            var mockWebSocket = new Mock<WebSocket>();
            
            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            mockWebSocketManager.Setup(x => x.AcceptWebSocketAsync(It.IsAny<string>()))
                .ReturnsAsync(mockWebSocket.Object);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["X-API-Key"] = virtualKey; // Test X-API-Key header
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(keyEntity);

            // Act
            var result = await _controller.Connect(model, provider);

            // Assert
            Assert.IsType<EmptyResult>(result);
            
            _mockProxyService.Verify(x => x.HandleConnectionAsync(
                It.IsAny<string>(),
                mockWebSocket.Object,
                keyEntity,
                model,
                provider, // Verify provider is passed
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Connect_WithProxyServiceException_ShouldReturn503()
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = "realtime" // Test "realtime" keyword permission
            };
            
            var mockWebSocket = new Mock<WebSocket>();
            
            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            mockWebSocketManager.Setup(x => x.AcceptWebSocketAsync(It.IsAny<string>()))
                .ReturnsAsync(mockWebSocket.Object);
            
            var httpContext = CreateHttpContextWithWebSockets(mockWebSocketManager.Object);
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, model))
                .ReturnsAsync(keyEntity);

            _mockProxyService.Setup(x => x.HandleConnectionAsync(
                    It.IsAny<string>(),
                    It.IsAny<WebSocket>(),
                    It.IsAny<VirtualKey>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Proxy connection failed"));

            // Act
            var result = await _controller.Connect(model);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(503, statusCodeResult.StatusCode);
            dynamic error = statusCodeResult.Value;
            Assert.Equal("Failed to establish real-time connection", error.error.ToString());
            Assert.Equal("Proxy connection failed", error.details.ToString());
        }

        #endregion

        #region GetConnections Tests

        [Fact]
        public async Task GetConnections_WithoutVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.GetConnections();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Virtual key required", error.error.ToString());
        }

        [Fact]
        public async Task GetConnections_WithValidKey_ShouldReturnActiveConnections()
        {
            // Arrange
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash"
            };
            
            var connections = new List<ConduitLLM.Core.Models.Realtime.ConnectionInfo>
            {
                new ConduitLLM.Core.Models.Realtime.ConnectionInfo
                {
                    ConnectionId = "conn-1",
                    Model = "gpt-4o-realtime-preview",
                    Provider = "openai",
                    ConnectedAt = DateTime.UtcNow.AddMinutes(-5)
                },
                new ConduitLLM.Core.Models.Realtime.ConnectionInfo
                {
                    ConnectionId = "conn-2",
                    Model = "ultravox",
                    Provider = "fixie",
                    ConnectedAt = DateTime.UtcNow.AddMinutes(-10)
                }
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.GetActiveConnectionsAsync(keyEntity.Id))
                .ReturnsAsync(connections);

            // Act
            var result = await _controller.GetConnections();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ConnectionStatusResponse>(okResult.Value);
            Assert.Equal(keyEntity.Id, response.VirtualKeyId);
            Assert.Equal(2, response.ActiveConnections.Count);
            Assert.Equal("conn-1", response.ActiveConnections[0].ConnectionId);
            Assert.Equal("conn-2", response.ActiveConnections[1].ConnectionId);
        }

        [Fact]
        public async Task GetConnections_WithInvalidKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var virtualKey = "condt_invalid_key";

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-API-Key"] = virtualKey;
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync((VirtualKey)null);

            // Act
            var result = await _controller.GetConnections();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Invalid virtual key", error.error.ToString());
        }

        #endregion

        #region TerminateConnection Tests

        [Fact]
        public async Task TerminateConnection_WithValidConnection_ShouldReturnNoContent()
        {
            // Arrange
            var connectionId = "conn-123";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash"
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.TerminateConnectionAsync(connectionId, keyEntity.Id))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task TerminateConnection_WithNonExistentConnection_ShouldReturnNotFound()
        {
            // Arrange
            var connectionId = "non-existent";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash"
            };

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["Authorization"] = $"Bearer {virtualKey}";
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            _mockVirtualKeyService.Setup(x => x.ValidateVirtualKeyAsync(virtualKey, null))
                .ReturnsAsync(keyEntity);

            _mockConnectionManager.Setup(x => x.TerminateConnectionAsync(connectionId, keyEntity.Id))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            dynamic error = notFoundResult.Value;
            Assert.Equal("Connection not found or not owned by this key", error.error.ToString());
        }

        [Fact]
        public async Task TerminateConnection_WithoutVirtualKey_ShouldReturnUnauthorized()
        {
            // Arrange
            var connectionId = "conn-123";
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            // Act
            var result = await _controller.TerminateConnection(connectionId);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            dynamic error = unauthorizedResult.Value;
            Assert.Equal("Virtual key required", error.error.ToString());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RealtimeController(
                null,
                _mockProxyService.Object,
                _mockVirtualKeyService.Object,
                _mockConnectionManager.Object));
            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullProxyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RealtimeController(
                _mockLogger.Object,
                null,
                _mockVirtualKeyService.Object,
                _mockConnectionManager.Object));
            Assert.Equal("proxyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullVirtualKeyService_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RealtimeController(
                _mockLogger.Object,
                _mockProxyService.Object,
                null,
                _mockConnectionManager.Object));
            Assert.Equal("virtualKeyService", ex.ParamName);
        }

        [Fact]
        public void Constructor_WithNullConnectionManager_ShouldThrowArgumentNullException()
        {
            // Arrange & Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new RealtimeController(
                _mockLogger.Object,
                _mockProxyService.Object,
                _mockVirtualKeyService.Object,
                null));
            Assert.Equal("connectionManager", ex.ParamName);
        }

        #endregion

        #region Authorization Tests

        [Fact]
        public void Controller_ShouldRequireAuthorization()
        {
            // Arrange & Act
            var controllerType = typeof(RealtimeController);
            var authorizeAttribute = Attribute.GetCustomAttribute(controllerType, typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute));

            // Assert
            Assert.NotNull(authorizeAttribute);
        }

        #endregion

        #region Permission Tests

        [Theory]
        [InlineData("elevenlabs-conversational", true)] // Specific realtime model
        [InlineData("gpt-4o-realtime-preview", true)]   // OpenAI realtime model
        [InlineData("ultravox", true)]                  // Fixie realtime model  
        [InlineData("realtime", true)]                  // Generic realtime permission
        [InlineData("gpt-4", false)]                    // Non-realtime model
        [InlineData("", false)]                         // Empty model list
        public async Task Connect_ShouldValidateRealtimePermissionsCorrectly(string allowedModels, bool shouldHavePermission)
        {
            // Arrange
            var model = "gpt-4o-realtime-preview";
            var virtualKey = "condt_test_key";
            var keyEntity = new VirtualKey
            {
                Id = 1,
                KeyHash = "hash",
                AllowedModels = allowedModels
            };
            
            var mockWebSocket = new Mock<WebSocket>();
            
            // Setup websocket context
            var mockWebSocketManager = new Mock<WebSocketManager>();
            mockWebSocketManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            if (shouldHavePermission)
            {
                mockWebSocketManager.Setup(x => x.AcceptWebSocketAsync(It.IsAny<string>()))
                    .ReturnsAsync(mockWebSocket.Object);
            }
            
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
            if (shouldHavePermission)
            {
                Assert.IsType<EmptyResult>(result);
            }
            else
            {
                var forbiddenResult = Assert.IsType<ObjectResult>(result);
                Assert.Equal(403, forbiddenResult.StatusCode);
            }
        }

        #endregion

        #region Helper Methods

        private HttpContext CreateHttpContextWithWebSockets(WebSocketManager webSocketManager)
        {
            // Create a service collection and build service provider
            var services = new ServiceCollection();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            var serviceProvider = services.BuildServiceProvider();
            
            // Create HttpContext with properly initialized features
            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            };
            
            // Set up WebSocket feature
            httpContext.Features.Set<IHttpWebSocketFeature>(new TestWebSocketFeature { WebSocketManager = webSocketManager });
            
            // Initialize request properties
            httpContext.Request.Method = "GET";
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("localhost");
            httpContext.Request.Path = "/v1/realtime/connect";
            
            return httpContext;
        }

        private class TestWebSocketFeature : IHttpWebSocketFeature
        {
            public bool IsWebSocketRequest => WebSocketManager?.IsWebSocketRequest ?? false;
            public WebSocketManager WebSocketManager { get; set; }
            
            public Task<WebSocket> AcceptAsync(WebSocketAcceptContext acceptContext)
            {
                return WebSocketManager?.AcceptWebSocketAsync(acceptContext?.SubProtocol) ?? Task.FromResult<WebSocket>(null);
            }
        }

        #endregion
    }
}