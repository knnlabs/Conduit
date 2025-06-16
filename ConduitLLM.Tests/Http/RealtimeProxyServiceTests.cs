using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Audio;
using ConduitLLM.Core.Models.Realtime;
using ConduitLLM.Http.Services;
using RealtimeConnectionInfo = ConduitLLM.Core.Models.Realtime.ConnectionInfo;

using Microsoft.Extensions.Logging;

using Moq;

using Xunit;

namespace ConduitLLM.Tests.Http
{
    public class RealtimeProxyServiceTests
    {
        private readonly Mock<IRealtimeMessageTranslator> _mockTranslator;
        private readonly Mock<IVirtualKeyService> _mockVirtualKeyService;
        private readonly Mock<IRealtimeConnectionManager> _mockConnectionManager;
        private readonly Mock<IAudioRouter> _mockAudioRouter;
        private readonly Mock<IRealtimeUsageTracker> _mockUsageTracker;
        private readonly Mock<ILogger<RealtimeProxyService>> _mockLogger;
        private readonly RealtimeProxyService _proxyService;

        public RealtimeProxyServiceTests()
        {
            _mockTranslator = new Mock<IRealtimeMessageTranslator>();
            _mockVirtualKeyService = new Mock<IVirtualKeyService>();
            _mockUsageTracker = new Mock<IRealtimeUsageTracker>();
            _mockLogger = new Mock<ILogger<RealtimeProxyService>>();
            
            // Create a mock that implements both interfaces
            var connectionManagerMock = new Mock<IRealtimeConnectionManager>();
            connectionManagerMock.As<IAudioRouter>();
            _mockConnectionManager = connectionManagerMock;
            _mockAudioRouter = connectionManagerMock.As<IAudioRouter>();
            
            _proxyService = new RealtimeProxyService(
                _mockTranslator.Object,
                _mockVirtualKeyService.Object,
                _mockConnectionManager.Object,
                _mockUsageTracker.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task HandleConnectionAsync_Should_Reject_Disabled_VirtualKey()
        {
            // Arrange
            var mockClientSocket = new Mock<WebSocket>();
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "test-key-hash",
                IsEnabled = false // Disabled key
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _proxyService.HandleConnectionAsync(
                    "test-connection-id",
                    mockClientSocket.Object,
                    virtualKey,
                    "gpt-4o-realtime",
                    null,
                    CancellationToken.None));
        }

        [Fact]
        public async Task HandleConnectionAsync_Should_Reject_OverBudget_VirtualKey()
        {
            // Arrange
            var mockClientSocket = new Mock<WebSocket>();
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "test-key-hash",
                IsEnabled = true,
                MaxBudget = 100m,
                CurrentSpend = 150m // Over budget
            };

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _proxyService.HandleConnectionAsync(
                    "test-connection-id",
                    mockClientSocket.Object,
                    virtualKey,
                    "gpt-4o-realtime",
                    null,
                    CancellationToken.None));
        }

        [Fact]
        public async Task HandleConnectionAsync_Should_Throw_When_No_Provider_Available()
        {
            // Arrange
            var mockClientSocket = new Mock<WebSocket>();
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "test-key-hash",
                IsEnabled = true
            };

            // Setup audio router to return null (no provider available)
            _mockAudioRouter.Setup(x => x.GetRealtimeClientAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IRealtimeAudioClient?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _proxyService.HandleConnectionAsync(
                    "test-connection-id",
                    mockClientSocket.Object,
                    virtualKey,
                    "gpt-4o-realtime",
                    null,
                    CancellationToken.None));
        }

        [Fact]
        public async Task HandleConnectionAsync_Should_Create_Session_With_Correct_Config()
        {
            // Arrange
            var mockClientSocket = new Mock<WebSocket>();
            mockClientSocket.Setup(x => x.State).Returns(WebSocketState.Open);
            
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "test-key-hash",
                IsEnabled = true
            };

            var mockRealtimeClient = new Mock<IRealtimeAudioClient>();
            var mockProviderSession = new Mock<RealtimeSession>();
            var mockProviderStream = new Mock<IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>>();

            RealtimeSessionConfig? capturedConfig = null;

            // Setup audio router
            _mockAudioRouter.Setup(x => x.GetRealtimeClientAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<RealtimeSessionConfig, string, CancellationToken>((config, key, ct) => capturedConfig = config)
                .ReturnsAsync(mockRealtimeClient.Object);

            // Setup realtime client
            mockRealtimeClient.Setup(x => x.CreateSessionAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProviderSession.Object);

            mockRealtimeClient.Setup(x => x.StreamAudioAsync(
                    It.IsAny<RealtimeSession>(),
                    It.IsAny<CancellationToken>()))
                .Returns(mockProviderStream.Object);

            // Setup client socket to immediately close
            mockClientSocket.SetupSequence(x => x.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            // Act
            await _proxyService.HandleConnectionAsync(
                "test-connection-id",
                mockClientSocket.Object,
                virtualKey,
                "gpt-4o-realtime",
                null,
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedConfig);
            Assert.Equal("gpt-4o-realtime", capturedConfig.Model);
            Assert.Equal("alloy", capturedConfig.Voice);
            Assert.Equal("You are a helpful assistant.", capturedConfig.SystemPrompt);
        }

        [Fact]
        public async Task HandleConnectionAsync_Should_Close_Session_On_Client_Disconnect()
        {
            // Arrange
            var mockClientSocket = new Mock<WebSocket>();
            mockClientSocket.Setup(x => x.State).Returns(WebSocketState.Open);
            
            var virtualKey = new VirtualKey
            {
                Id = 1,
                KeyHash = "test-key-hash",
                IsEnabled = true
            };

            var mockRealtimeClient = new Mock<IRealtimeAudioClient>();
            var mockProviderSession = new Mock<RealtimeSession>();
            var mockProviderStream = new Mock<IAsyncDuplexStream<RealtimeAudioFrame, RealtimeResponse>>();

            // Setup audio router and realtime client
            _mockAudioRouter.Setup(x => x.GetRealtimeClientAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockRealtimeClient.Object);

            mockRealtimeClient.Setup(x => x.CreateSessionAsync(
                    It.IsAny<RealtimeSessionConfig>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockProviderSession.Object);

            mockRealtimeClient.Setup(x => x.StreamAudioAsync(
                    It.IsAny<RealtimeSession>(),
                    It.IsAny<CancellationToken>()))
                .Returns(mockProviderStream.Object);

            // Setup client socket to immediately close
            mockClientSocket.SetupSequence(x => x.ReceiveAsync(
                    It.IsAny<ArraySegment<byte>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            // Act
            await _proxyService.HandleConnectionAsync(
                "test-connection-id",
                mockClientSocket.Object,
                virtualKey,
                "gpt-4o-realtime",
                null,
                CancellationToken.None);

            // Assert
            mockRealtimeClient.Verify(x => x.CloseSessionAsync(
                mockProviderSession.Object,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetConnectionStatusAsync_Should_Return_Null_For_Unknown_Connection()
        {
            // Arrange
            _mockConnectionManager.Setup(x => x.GetConnectionAsync(It.IsAny<string>()))
                .ReturnsAsync((RealtimeConnectionInfo?)null);

            // Act
            var status = await _proxyService.GetConnectionStatusAsync("unknown-connection-id");

            // Assert
            Assert.Null(status);
        }

        [Fact]
        public async Task GetConnectionStatusAsync_Should_Return_Status_For_Known_Connection()
        {
            // Arrange
            var connectionInfo = new RealtimeConnectionInfo
            {
                ConnectionId = "test-connection-id",
                Provider = "openai",
                Model = "gpt-4o-realtime",
                ConnectedAt = DateTime.UtcNow.AddMinutes(-5),
                LastActivity = DateTime.UtcNow.AddSeconds(-30),
                Usage = new ConnectionUsageStats
                {
                    MessagesSent = 10,
                    MessagesReceived = 8,
                    EstimatedCost = 1.5m
                },
                EstimatedCost = 1.5m
            };

            _mockConnectionManager.Setup(x => x.GetConnectionAsync("test-connection-id"))
                .ReturnsAsync(connectionInfo);

            // Act
            var status = await _proxyService.GetConnectionStatusAsync("test-connection-id");

            // Assert
            Assert.NotNull(status);
            Assert.Equal("test-connection-id", status.ConnectionId);
            Assert.Equal("openai", status.Provider);
            Assert.Equal("gpt-4o-realtime", status.Model);
            Assert.Equal(1.5m, status.EstimatedCost);
        }

        [Fact]
        public async Task CloseConnectionAsync_Should_Return_False_For_Unknown_Connection()
        {
            // Arrange
            _mockConnectionManager.Setup(x => x.GetConnectionAsync(It.IsAny<string>()))
                .ReturnsAsync((RealtimeConnectionInfo?)null);

            // Act
            var result = await _proxyService.CloseConnectionAsync("unknown-connection-id", "Test close");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CloseConnectionAsync_Should_Return_True_For_Known_Connection()
        {
            // Arrange
            var connectionInfo = new RealtimeConnectionInfo
            {
                ConnectionId = "test-connection-id",
                VirtualKey = "test-key"
            };

            _mockConnectionManager.Setup(x => x.GetConnectionAsync("test-connection-id"))
                .ReturnsAsync(connectionInfo);

            _mockConnectionManager.Setup(x => x.UnregisterConnectionAsync("test-connection-id"))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _proxyService.CloseConnectionAsync("test-connection-id", "Test close");

            // Assert
            Assert.True(result);
            _mockConnectionManager.Verify(x => x.UnregisterConnectionAsync("test-connection-id"), Times.Once);
        }
    }
}