using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Tests.Gateway.Hubs.Mocks;

namespace ConduitLLM.Tests.Gateway.Hubs
{
    /// <summary>
    /// Unit tests for the SecureHub abstract base class.
    /// Tests are performed using the MockSecureHub concrete implementation.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "SignalR")]
    [Trait("Feature", "SecureHub")]
    public class SecureHubTests : HubTestBase
    {
        private readonly Mock<ILogger<MockSecureHub>> _mockLogger;
        private readonly MockSecureHub _hub;
        private readonly Mock<HubCallerContext> _mockContext;

        public SecureHubTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = CreateLogger<MockSecureHub>();
            _hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);
            _mockContext = CreateHubCallerContext();

            // Wire up the hub's context and groups
            SetupHub();
        }

        private void SetupHub()
        {
            // Set up the mock context
            var hubType = typeof(Hub);
            var contextProperty = hubType.GetProperty("Context");
            var groupsProperty = hubType.GetProperty("Groups");
            var clientsProperty = hubType.GetProperty("Clients");

            // Use reflection to set the protected properties since Hub doesn't expose setters
            // We need to create our hub with proper context injection
        }

        /// <summary>
        /// Helper to create a hub instance with context properly set up.
        /// </summary>
        private MockSecureHub CreateHubWithContext(
            int? virtualKeyId = DefaultVirtualKeyId,
            string virtualKeyName = DefaultVirtualKeyName)
        {
            var hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);

            // Create and configure context
            var context = CreateHubCallerContext(virtualKeyId, virtualKeyName);

            // Set up auth service to return the virtual key info
            if (virtualKeyId.HasValue)
            {
                SetupAuthServiceReturnsVirtualKey(virtualKeyId.Value, virtualKeyName);
            }
            else
            {
                SetupAuthServiceReturnsNoVirtualKey();
            }

            // Use reflection to set Context and Groups on the hub
            var hubType = typeof(Hub);
            hubType.GetProperty("Context")?.SetValue(hub, context.Object);
            hubType.GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
            hubType.GetProperty("Clients")?.SetValue(hub, MockClients.Object);

            return hub;
        }

        #region OnConnectedAsync Tests

        [Fact]
        public async Task OnConnectedAsync_WithValidVirtualKey_AddsToVirtualKeyGroup()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);

            // Act
            await hub.OnConnectedAsync();

            // Assert
            VerifyAddedToGroup(VirtualKeyGroup(DefaultVirtualKeyId));
        }

        [Fact]
        public async Task OnConnectedAsync_WithoutVirtualKey_AbortsConnection()
        {
            // Arrange
            var context = CreateUnauthorizedHubCallerContext();
            var abortCalled = false;
            context.Setup(x => x.Abort()).Callback(() => abortCalled = true);

            var hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);
            SetupAuthServiceReturnsNoVirtualKey();

            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
            typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
            typeof(Hub).GetProperty("Clients")?.SetValue(hub, MockClients.Object);

            // Act
            await hub.OnConnectedAsync();

            // Assert
            Assert.True(abortCalled, "Context.Abort() should have been called");
        }

        [Fact]
        public async Task OnConnectedAsync_WithoutVirtualKey_LogsAuthenticationFailure()
        {
            // Arrange
            var context = CreateUnauthorizedHubCallerContext();
            context.Setup(x => x.Abort());

            var hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);
            SetupAuthServiceReturnsNoVirtualKey();

            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
            typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
            typeof(Hub).GetProperty("Clients")?.SetValue(hub, MockClients.Object);

            // Act
            await hub.OnConnectedAsync();

            // Assert - Verify warning is logged for authentication failure
            // The actual message is "Connection without valid virtual key ID to MockSecureHub"
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("without valid virtual key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task OnConnectedAsync_WithValidVirtualKey_LogsConnectionInfo()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: 456, virtualKeyName: "my-test-key");

            // Act
            await hub.OnConnectedAsync();

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("connected to MockSecureHub")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        #endregion

        #region OnDisconnectedAsync Tests

        [Fact]
        public async Task OnDisconnectedAsync_WithVirtualKey_RemovesFromVirtualKeyGroup()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            await hub.OnConnectedAsync(); // Connect first

            // Act
            await hub.OnDisconnectedAsync(null);

            // Assert
            VerifyRemovedFromGroup(VirtualKeyGroup(DefaultVirtualKeyId));
        }

        [Fact]
        public async Task OnDisconnectedAsync_WithException_LogsAndRemovesFromGroup()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            await hub.OnConnectedAsync();
            var exception = new Exception("Test disconnect exception");

            // Act
            await hub.OnDisconnectedAsync(exception);

            // Assert
            VerifyRemovedFromGroup(VirtualKeyGroup(DefaultVirtualKeyId));
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("disconnected")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        [Fact]
        public async Task OnDisconnectedAsync_WithoutVirtualKey_DoesNotRemoveFromGroup()
        {
            // Arrange
            var context = CreateUnauthorizedHubCallerContext();
            context.Setup(x => x.Abort()); // Prevent actual abort

            var hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);
            SetupAuthServiceReturnsNoVirtualKey();

            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
            typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);

            // Act
            await hub.OnDisconnectedAsync(null);

            // Assert - RemoveFromGroupAsync should not be called when no virtual key
            MockGroups.Verify(
                x => x.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never());
        }

        #endregion

        #region RequireVirtualKeyId Tests

        [Fact]
        public void RequireVirtualKeyId_WithValidKey_ReturnsKeyId()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: 789);

            // Act
            var result = hub.RequireVirtualKeyId();

            // Assert
            Assert.Equal(789, result);
        }

        [Fact]
        public void RequireVirtualKeyId_WithoutKey_ThrowsHubException()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: null);

            // Act & Assert
            var exception = Assert.Throws<HubException>(() => hub.RequireVirtualKeyId());
            Assert.Equal("Unauthorized", exception.Message);
        }

        #endregion

        #region GetVirtualKeyId Tests

        [Fact]
        public void GetVirtualKeyId_ExtractsFromContext_ReturnsCorrectId()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: 555);

            // Act
            var result = hub.GetVirtualKeyId();

            // Assert
            Assert.Equal(555, result);
        }

        [Fact]
        public void GetVirtualKeyId_WithoutKey_ReturnsNull()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: null);

            // Act
            var result = hub.GetVirtualKeyId();

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region CanAccessTaskAsync Tests

        [Fact]
        public async Task CanAccessTaskAsync_WithValidTask_ReturnsTrue()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            var taskId = "task-123";
            SetupAuthServiceCanAccessResource("task", taskId, true);

            // Act
            var result = await hub.CanAccessTaskAsync(taskId);

            // Assert
            Assert.True(result);
            MockAuthService.Verify(
                x => x.CanAccessResourceAsync(It.IsAny<HubCallerContext>(), "task", taskId),
                Times.Once());
        }

        [Fact]
        public async Task CanAccessTaskAsync_WithInvalidTask_ReturnsFalse()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            var taskId = "task-456";
            SetupAuthServiceCanAccessResource("task", taskId, false);

            // Act
            var result = await hub.CanAccessTaskAsync(taskId);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region IsAdminAsync Tests

        [Fact]
        public async Task IsAdminAsync_DelegatesToAuthService_ReturnsTrue()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            SetupAuthServiceIsAdmin(true);

            // Act
            var result = await hub.IsAdminAsync();

            // Assert
            Assert.True(result);
            MockAuthService.Verify(
                x => x.IsAdminAsync(It.IsAny<HubCallerContext>()),
                Times.Once());
        }

        [Fact]
        public async Task IsAdminAsync_DelegatesToAuthService_ReturnsFalse()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            SetupAuthServiceIsAdmin(false);

            // Act
            var result = await hub.IsAdminAsync();

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetVirtualKeyAsync Tests

        [Fact]
        public async Task GetVirtualKeyAsync_ReturnsVirtualKey()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            var expectedKey = new VirtualKey
            {
                Id = DefaultVirtualKeyId,
                KeyName = DefaultVirtualKeyName,
                IsEnabled = true
            };
            SetupAuthServiceReturnsVirtualKeyEntity(expectedKey);

            // Act
            var result = await hub.GetVirtualKeyAsync(DefaultVirtualKeyId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DefaultVirtualKeyId, result.Id);
            Assert.Equal(DefaultVirtualKeyName, result.KeyName);
        }

        [Fact]
        public async Task GetVirtualKeyAsync_WhenNotFound_ReturnsNull()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);
            MockAuthService.Setup(x => x.GetAuthenticatedVirtualKeyAsync(It.IsAny<HubCallerContext>()))
                .ReturnsAsync((VirtualKey?)null);

            // Act
            var result = await hub.GetVirtualKeyAsync(DefaultVirtualKeyId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetOrCreateCorrelationId Tests

        [Fact]
        public void GetOrCreateCorrelationId_CreatesNewId_WhenNotExists()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);

            // Act
            var correlationId = hub.GetOrCreateCorrelationId();

            // Assert
            Assert.NotNull(correlationId);
            Assert.True(Guid.TryParse(correlationId, out _), "CorrelationId should be a valid GUID");
        }

        [Fact]
        public void GetOrCreateCorrelationId_ReturnsExisting_WhenExists()
        {
            // Arrange
            var existingCorrelationId = Guid.NewGuid().ToString();
            var context = CreateHubCallerContext(virtualKeyId: DefaultVirtualKeyId);
            context.Object.Items["CorrelationId"] = existingCorrelationId;

            var hub = new MockSecureHub(_mockLogger.Object, MockServiceProvider.Object);
            SetupAuthServiceReturnsVirtualKey(DefaultVirtualKeyId);
            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);

            // Act
            var result = hub.GetOrCreateCorrelationId();

            // Assert
            Assert.Equal(existingCorrelationId, result);
        }

        [Fact]
        public void GetOrCreateCorrelationId_ReturnsSameValue_OnMultipleCalls()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: DefaultVirtualKeyId);

            // Act
            var firstCall = hub.GetOrCreateCorrelationId();
            var secondCall = hub.GetOrCreateCorrelationId();

            // Assert
            Assert.Equal(firstCall, secondCall);
        }

        #endregion

        #region VirtualKeyId Property Tests

        [Fact]
        public void VirtualKeyId_Property_ReturnsStringRepresentation()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: 999);

            // Act
            var result = hub.VirtualKeyId;

            // Assert
            Assert.Equal("999", result);
        }

        [Fact]
        public void VirtualKeyId_Property_ReturnsNull_WhenNoKey()
        {
            // Arrange
            var hub = CreateHubWithContext(virtualKeyId: null);

            // Act
            var result = hub.VirtualKeyId;

            // Assert
            Assert.Null(result);
        }

        #endregion
    }
}
