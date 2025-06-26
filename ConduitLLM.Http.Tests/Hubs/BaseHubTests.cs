using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class BaseHubTests
    {
        public class TestHub : BaseHub
        {
            public TestHub(ILogger<TestHub> logger) : base(logger)
            {
            }

            protected override string GetHubName() => "TestHub";

            public new Task AddToGroupAsync(string groupName) => base.AddToGroupAsync(groupName);
            public new Task RemoveFromGroupAsync(string groupName) => base.RemoveFromGroupAsync(groupName);
        }

        private readonly Mock<ILogger<TestHub>> _loggerMock;
        private readonly TestHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;

        public BaseHubTests()
        {
            _loggerMock = new Mock<ILogger<TestHub>>();
            _contextMock = new Mock<HubCallerContext>();
            _groupsMock = new Mock<IGroupManager>();

            _hub = new TestHub(_loggerMock.Object)
            {
                Context = _contextMock.Object,
                Groups = _groupsMock.Object
            };
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TestHub(null!));
        }

        [Fact]
        public async Task OnConnectedAsync_LogsConnection()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Client connected to TestHub: {connectionId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_WithException_LogsWarning()
        {
            // Arrange
            var connectionId = "test-connection-id";
            var exception = new Exception("Test exception");
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnDisconnectedAsync(exception);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Client disconnected from TestHub with error: {connectionId}")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_WithoutException_LogsInformation()
        {
            // Arrange
            var connectionId = "test-connection-id";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _loggerMock.Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Client disconnected from TestHub: {connectionId}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task AddToGroupAsync_AddsConnectionToGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            var groupName = "test-group";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.AddToGroupAsync(groupName);

            // Assert
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, groupName, default), Times.Once);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Added connection {connectionId} to group {groupName} in TestHub")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public async Task RemoveFromGroupAsync_RemovesConnectionFromGroup()
        {
            // Arrange
            var connectionId = "test-connection-id";
            var groupName = "test-group";
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            // Act
            await _hub.RemoveFromGroupAsync(groupName);

            // Assert
            _groupsMock.Verify(x => x.RemoveFromGroupAsync(connectionId, groupName, default), Times.Once);
            _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Removed connection {connectionId} from group {groupName} in TestHub")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}