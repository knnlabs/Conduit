using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class NavigationStateHubTests
    {
        private readonly Mock<ILogger<NavigationStateHub>> _loggerMock;
        private readonly NavigationStateHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;
        private readonly Mock<IHubCallerClients> _clientsMock;
        private readonly Mock<ISingleClientProxy> _callerMock;

        public NavigationStateHubTests()
        {
            _loggerMock = new Mock<ILogger<NavigationStateHub>>();
            _contextMock = new Mock<HubCallerContext>();
            _groupsMock = new Mock<IGroupManager>();
            _clientsMock = new Mock<IHubCallerClients>();
            _callerMock = new Mock<ISingleClientProxy>();

            _clientsMock.Setup(x => x.Caller).Returns(_callerMock.Object);

            _hub = new NavigationStateHub(_loggerMock.Object)
            {
                Context = _contextMock.Object,
                Groups = _groupsMock.Object,
                Clients = _clientsMock.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_AddsToNavigationUpdatesGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, "navigation-updates", default), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_RemovesFromNavigationUpdatesGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, "navigation-updates", default), Times.Once);
        }

        [Fact]
        public async Task SubscribeToModel_ValidModelId_AddsToGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            var modelId = "test-model-123";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.SubscribeToModel(modelId);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"model-{modelId}", default), Times.Once);
        }

        [Fact]
        public async Task SubscribeToModel_EmptyModelId_DoesNotAddToGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.SubscribeToModel("");

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("attempted to subscribe to empty model ID")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task SubscribeToModel_NullModelId_DoesNotAddToGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.SubscribeToModel(null!);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task UnsubscribeFromModel_ValidModelId_RemovesFromGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            var modelId = "test-model-123";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.UnsubscribeFromModel(modelId);

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, $"model-{modelId}", default), Times.Once);
        }

        [Fact]
        public async Task UnsubscribeFromModel_EmptyModelId_DoesNotRemoveFromGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.UnsubscribeFromModel("");

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        }

        [Fact]
        public async Task RequestCurrentState_SendsResponseToCaller()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.RequestCurrentState();

            // Assert
            _callerMock.Verify(x => x.SendCoreAsync(
                "StateUpdateRequested",
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] != null &&
                    args[0].GetType().GetProperty("timestamp") != null),
                default), Times.Once);
        }
    }
}