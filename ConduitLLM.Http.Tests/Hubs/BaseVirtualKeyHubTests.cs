using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Tests.Hubs
{
    public class BaseVirtualKeyHubTests
    {
        public class TestHub : BaseVirtualKeyHub
        {
            public TestHub(ILogger<TestHub> logger) : base(logger)
            {
            }

            protected override string GetHubName() => "TestHub";

            public new int? GetVirtualKeyId() => base.GetVirtualKeyId();
            public new string GetVirtualKeyName() => base.GetVirtualKeyName();
            public new int RequireVirtualKeyId() => base.RequireVirtualKeyId();
            public new static int? ConvertToInt(object value) => BaseVirtualKeyHub.ConvertToInt(value);
        }

        private readonly Mock<ILogger<TestHub>> _loggerMock;
        private readonly TestHub _hub;
        private readonly Mock<HubCallerContext> _contextMock;
        private readonly Mock<IGroupManager> _groupsMock;

        public BaseVirtualKeyHubTests()
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
        public void GetVirtualKeyId_FromItems_ReturnsId()
        {
            // Arrange
            var expectedId = 123;
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = expectedId
            };
            _contextMock.Setup(x => x.Items).Returns(items);

            // Act
            var result = _hub.GetVirtualKeyId();

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void GetVirtualKeyId_FromClaims_ReturnsId()
        {
            // Arrange
            var expectedId = 456;
            var items = new Dictionary<object, object?>();
            var claims = new List<Claim> { new Claim("VirtualKeyId", expectedId.ToString()) };
            var identity = new ClaimsIdentity(claims);
            var user = new ClaimsPrincipal(identity);

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.User).Returns(user);

            // Act
            var result = _hub.GetVirtualKeyId();

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void GetVirtualKeyId_NotFound_ReturnsNull()
        {
            // Arrange
            var items = new Dictionary<object, object?>();
            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.User).Returns((ClaimsPrincipal?)null);

            // Act
            var result = _hub.GetVirtualKeyId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetVirtualKeyName_FromItems_ReturnsName()
        {
            // Arrange
            var expectedName = "TestKey";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyName"] = expectedName
            };
            _contextMock.Setup(x => x.Items).Returns(items);

            // Act
            var result = _hub.GetVirtualKeyName();

            // Assert
            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetVirtualKeyName_FromIdentity_ReturnsName()
        {
            // Arrange
            var expectedName = "IdentityKey";
            var items = new Dictionary<object, object?>();
            var identity = new ClaimsIdentity(new List<Claim>(), "test");
            identity.AddClaim(new Claim(ClaimTypes.Name, expectedName));
            var user = new ClaimsPrincipal(identity);

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.User).Returns(user);

            // Act
            var result = _hub.GetVirtualKeyName();

            // Assert
            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void RequireVirtualKeyId_WithValidId_ReturnsId()
        {
            // Arrange
            var expectedId = 789;
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = expectedId
            };
            _contextMock.Setup(x => x.Items).Returns(items);

            // Act
            var result = _hub.RequireVirtualKeyId();

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void RequireVirtualKeyId_WithoutId_ThrowsHubException()
        {
            // Arrange
            var items = new Dictionary<object, object?>();
            _contextMock.Setup(x => x.Items).Returns(items);

            // Act & Assert
            var exception = Assert.Throws<HubException>(() => _hub.RequireVirtualKeyId());
            Assert.Equal("Unauthorized", exception.Message);
        }

        [Theory]
        [InlineData(42, 42)]
        [InlineData(100L, 100)]
        [InlineData("999", 999)]
        public void ConvertToInt_ValidValues_ReturnsInt(object input, int expected)
        {
            // Act
            var result = TestHub.ConvertToInt(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("invalid")]
        [InlineData(3.14)]
        [InlineData(true)]
        public void ConvertToInt_InvalidValues_ReturnsNull(object input)
        {
            // Act
            var result = TestHub.ConvertToInt(input);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task OnConnectedAsync_CallsOnVirtualKeyConnected()
        {
            // Arrange
            var virtualKeyId = 123;
            var virtualKeyName = "TestKey";
            var connectionId = "test-connection-id";
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["VirtualKeyName"] = virtualKeyName
            };

            _contextMock.Setup(x => x.Items).Returns(items);
            _contextMock.Setup(x => x.ConnectionId).Returns(connectionId);

            var onConnectedCalled = false;
            var connectedHub = new Mock<BaseVirtualKeyHub>(_loggerMock.Object) { CallBase = true };
            connectedHub.Protected()
                .Setup<Task>("OnVirtualKeyConnectedAsync", virtualKeyId, virtualKeyName)
                .Returns(Task.CompletedTask)
                .Callback(() => onConnectedCalled = true);
            
            connectedHub.Object.Context = _contextMock.Object;
            connectedHub.Object.Groups = _groupsMock.Object;

            // Act
            await connectedHub.Object.OnConnectedAsync();

            // Assert
            Assert.True(onConnectedCalled);
            _groupsMock.Verify(x => x.AddToGroupAsync(connectionId, $"vkey-{virtualKeyId}", default), Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_CallsOnVirtualKeyDisconnected()
        {
            // Arrange
            var virtualKeyId = 123;
            var virtualKeyName = "TestKey";
            var exception = new Exception("Test exception");
            var items = new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["VirtualKeyName"] = virtualKeyName
            };

            _contextMock.Setup(x => x.Items).Returns(items);

            var onDisconnectedCalled = false;
            var disconnectedHub = new Mock<BaseVirtualKeyHub>(_loggerMock.Object) { CallBase = true };
            disconnectedHub.Protected()
                .Setup<Task>("OnVirtualKeyDisconnectedAsync", virtualKeyId, virtualKeyName, exception)
                .Returns(Task.CompletedTask)
                .Callback(() => onDisconnectedCalled = true);
            
            disconnectedHub.Object.Context = _contextMock.Object;

            // Act
            await disconnectedHub.Object.OnDisconnectedAsync(exception);

            // Assert
            Assert.True(onDisconnectedCalled);
        }
    }
}