using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Options;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Authentication;

namespace ConduitLLM.Tests.Gateway.Authentication
{
    /// <summary>
    /// Unit tests for connection limit enforcement in VirtualKeySignalRRateLimitFilter
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "VirtualKeySignalRRateLimitFilter")]
    [Trait("Feature", "ConnectionLimiting")]
    public class VirtualKeySignalRRateLimitFilterConnectionLimitTests : TestBase
    {
        private readonly Mock<ISignalRRateLimitService> _mockSignalRRateLimitService;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<VirtualKeySignalRRateLimitFilter>> _mockLogger;

        public VirtualKeySignalRRateLimitFilterConnectionLimitTests(ITestOutputHelper output) : base(output)
        {
            _mockSignalRRateLimitService = new Mock<ISignalRRateLimitService>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = CreateLogger<VirtualKeySignalRRateLimitFilter>();
        }

        private VirtualKeySignalRRateLimitFilter CreateFilter(SignalRConnectionOptions? options = null)
        {
            options ??= new SignalRConnectionOptions
            {
                MaxConnectionsPerVirtualKey = 100,
                MaxTotalConnections = 10000,
                EnforceLimits = true
            };

            var mockOptions = new Mock<IOptions<SignalRConnectionOptions>>();
            mockOptions.Setup(x => x.Value).Returns(options);

            return new VirtualKeySignalRRateLimitFilter(
                _mockSignalRRateLimitService.Object,
                _mockLogger.Object,
                _mockServiceProvider.Object,
                mockOptions.Object);
        }

        private (Mock<HubCallerContext> MockContext, HubLifetimeContext LifetimeContext) CreateHubContext(
            string? virtualKeyHash = "test-vk-hash",
            int? virtualKeyId = 123)
        {
            var items = new Dictionary<object, object?>();
            if (virtualKeyHash != null)
            {
                items["VirtualKeyHash"] = virtualKeyHash;
            }
            if (virtualKeyId.HasValue)
            {
                items["VirtualKeyId"] = virtualKeyId.Value;
            }

            var mockHubCallerContext = new Mock<HubCallerContext>();
            mockHubCallerContext.Setup(x => x.Items).Returns(items);
            mockHubCallerContext.Setup(x => x.ConnectionId).Returns("test-connection-id");
            mockHubCallerContext.Setup(x => x.Features).Returns(new FeatureCollection());

            var mockHub = new Mock<Hub>();
            var lifetimeContext = new HubLifetimeContext(
                mockHubCallerContext.Object,
                _mockServiceProvider.Object,
                mockHub.Object);

            return (mockHubCallerContext, lifetimeContext);
        }

        [Fact]
        public async Task OnConnectedAsync_WhenLimitNotReached_AllowsConnection()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext();
            var nextCalled = false;

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult
                {
                    IsAllowed = true,
                    CurrentConnections = 50,
                    MaxConnections = 100
                });

            _mockSignalRRateLimitService
                .Setup(x => x.IncrementConnectionCountAsync(It.IsAny<string>()))
                .ReturnsAsync(51);

            // Act
            await filter.OnConnectedAsync(context, ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            // Assert
            Assert.True(nextCalled, "next() should have been called");
            _mockSignalRRateLimitService.Verify(
                x => x.IncrementConnectionCountAsync(It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_WhenLimitReached_ThrowsHubException()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext();

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult
                {
                    IsAllowed = false,
                    CurrentConnections = 100,
                    MaxConnections = 100,
                    DenialReason = "Connection limit exceeded (100/100)"
                });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HubException>(() =>
                filter.OnConnectedAsync(context, ctx => Task.CompletedTask));

            Assert.Contains("Connection limit exceeded", exception.Message);
        }

        [Fact]
        public async Task OnConnectedAsync_WhenLimitReached_DoesNotIncrementConnectionCount()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext();

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult
                {
                    IsAllowed = false,
                    CurrentConnections = 100,
                    MaxConnections = 100,
                    DenialReason = "Connection limit exceeded"
                });

            // Act
            try
            {
                await filter.OnConnectedAsync(context, ctx => Task.CompletedTask);
            }
            catch (HubException)
            {
                // Expected
            }

            // Assert - IncrementConnectionCountAsync should NOT be called when limit is exceeded
            _mockSignalRRateLimitService.Verify(
                x => x.IncrementConnectionCountAsync(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task OnConnectedAsync_WhenEnforceLimitsFalse_AllowsConnectionEvenOverLimit()
        {
            // Arrange
            var options = new SignalRConnectionOptions
            {
                MaxConnectionsPerVirtualKey = 100,
                EnforceLimits = false // Disabled
            };
            var filter = CreateFilter(options);
            var (_, context) = CreateHubContext();
            var nextCalled = false;

            _mockSignalRRateLimitService
                .Setup(x => x.IncrementConnectionCountAsync(It.IsAny<string>()))
                .ReturnsAsync(150);

            // Act
            await filter.OnConnectedAsync(context, ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            // Assert
            Assert.True(nextCalled, "next() should have been called when EnforceLimits is false");

            // CheckConnectionLimitAsync should NOT be called when enforcement is disabled
            _mockSignalRRateLimitService.Verify(
                x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
        }

        [Fact]
        public async Task OnConnectedAsync_WithNoVirtualKey_AllowsConnection()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext(virtualKeyHash: null, virtualKeyId: null);
            var nextCalled = false;

            // Act
            await filter.OnConnectedAsync(context, ctx =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            // Assert
            Assert.True(nextCalled, "next() should have been called when no virtual key is present");

            // No rate limit service calls should be made
            _mockSignalRRateLimitService.Verify(
                x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never);
            _mockSignalRRateLimitService.Verify(
                x => x.IncrementConnectionCountAsync(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task OnConnectedAsync_CallsCheckLimitBeforeIncrement()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext();
            var callOrder = new List<string>();

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .Callback(() => callOrder.Add("CheckLimit"))
                .ReturnsAsync(new ConnectionLimitResult { IsAllowed = true });

            _mockSignalRRateLimitService
                .Setup(x => x.IncrementConnectionCountAsync(It.IsAny<string>()))
                .Callback(() => callOrder.Add("Increment"))
                .ReturnsAsync(1);

            // Act
            await filter.OnConnectedAsync(context, ctx => Task.CompletedTask);

            // Assert - Check should happen before increment
            Assert.Equal(2, callOrder.Count);
            Assert.Equal("CheckLimit", callOrder[0]);
            Assert.Equal("Increment", callOrder[1]);
        }

        [Fact]
        public async Task OnConnectedAsync_UsesConfiguredMaxConnectionsPerVirtualKey()
        {
            // Arrange
            const int configuredMax = 250;
            var options = new SignalRConnectionOptions
            {
                MaxConnectionsPerVirtualKey = configuredMax,
                EnforceLimits = true
            };
            var filter = CreateFilter(options);
            var (_, context) = CreateHubContext();

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult { IsAllowed = true });

            _mockSignalRRateLimitService
                .Setup(x => x.IncrementConnectionCountAsync(It.IsAny<string>()))
                .ReturnsAsync(1);

            // Act
            await filter.OnConnectedAsync(context, ctx => Task.CompletedTask);

            // Assert - Verify the configured max was passed to the service
            _mockSignalRRateLimitService.Verify(
                x => x.CheckConnectionLimitAsync(It.IsAny<string>(), configuredMax),
                Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_WhenLimitReached_DoesNotCallNext()
        {
            // Arrange
            var filter = CreateFilter();
            var (_, context) = CreateHubContext();
            var nextCalled = false;

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult
                {
                    IsAllowed = false,
                    DenialReason = "Limit exceeded"
                });

            // Act
            try
            {
                await filter.OnConnectedAsync(context, ctx =>
                {
                    nextCalled = true;
                    return Task.CompletedTask;
                });
            }
            catch (HubException)
            {
                // Expected
            }

            // Assert
            Assert.False(nextCalled, "next() should NOT have been called when limit is exceeded");
        }

        [Fact]
        public async Task OnConnectedAsync_PassesCorrectVirtualKeyHashToService()
        {
            // Arrange
            const string expectedHash = "specific-vk-hash-123";
            var filter = CreateFilter();
            var (_, context) = CreateHubContext(virtualKeyHash: expectedHash);

            _mockSignalRRateLimitService
                .Setup(x => x.CheckConnectionLimitAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new ConnectionLimitResult { IsAllowed = true });

            _mockSignalRRateLimitService
                .Setup(x => x.IncrementConnectionCountAsync(It.IsAny<string>()))
                .ReturnsAsync(1);

            // Act
            await filter.OnConnectedAsync(context, ctx => Task.CompletedTask);

            // Assert
            _mockSignalRRateLimitService.Verify(
                x => x.CheckConnectionLimitAsync(expectedHash, It.IsAny<int>()),
                Times.Once);

            _mockSignalRRateLimitService.Verify(
                x => x.IncrementConnectionCountAsync(expectedHash),
                Times.Once);
        }
    }
}
