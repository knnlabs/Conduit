using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Gateway.Authentication;
using ConduitLLM.Gateway.Interfaces;
using ConduitLLM.Gateway.Metrics;

namespace ConduitLLM.Tests.Gateway.Hubs
{
    /// <summary>
    /// Base class for SignalR hub unit tests providing common mock setup and utilities.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "SignalR")]
    [Trait("Feature", "Hubs")]
    public abstract class HubTestBase : TestBase
    {
        // Test data constants
        protected const int DefaultVirtualKeyId = 123;
        protected const string DefaultVirtualKeyName = "test-key";
        protected const string DefaultConnectionId = "test-connection-id";

        // Core SignalR mocks
        protected Mock<IGroupManager> MockGroups { get; }
        protected Mock<IHubCallerClients> MockClients { get; }
        protected Mock<IClientProxy> MockClientProxy { get; }

        // Service mocks
        protected Mock<IServiceProvider> MockServiceProvider { get; }
        protected Mock<ISignalRAuthenticationService> MockAuthService { get; }
        protected Mock<ISignalRMetrics> MockMetrics { get; }

        // Real metrics instance for tests that need working Counter<long> instances
        // (Counter<long> is a sealed struct that cannot be mocked)
        protected ISignalRMetrics RealMetrics { get; }

        protected HubTestBase(ITestOutputHelper output) : base(output)
        {
            // Initialize core SignalR mocks
            MockGroups = new Mock<IGroupManager>();
            MockClients = new Mock<IHubCallerClients>();
            MockClientProxy = new Mock<IClientProxy>();

            // Initialize service mocks
            MockServiceProvider = new Mock<IServiceProvider>();
            MockAuthService = new Mock<ISignalRAuthenticationService>();
            MockMetrics = new Mock<ISignalRMetrics>();

            // Create a real metrics instance for tests that need non-mockable counters
            var meterFactory = new TestMeterFactory();
            RealMetrics = new SignalRMetrics(meterFactory);

            // Wire up common mock behavior
            SetupClientMocks();
            SetupServiceProviderMocks();
        }

        /// <summary>
        /// Simple test meter factory that creates real meters.
        /// </summary>
        private class TestMeterFactory : IMeterFactory
        {
            private readonly List<Meter> _meters = new();

            public Meter Create(MeterOptions options)
            {
                var meter = new Meter(options.Name, options.Version);
                _meters.Add(meter);
                return meter;
            }

            public void Dispose()
            {
                foreach (var meter in _meters)
                {
                    meter.Dispose();
                }
                _meters.Clear();
            }
        }

        /// <summary>
        /// Creates a mock HubCallerContext with configurable virtual key.
        /// </summary>
        protected Mock<HubCallerContext> CreateHubCallerContext(
            int? virtualKeyId = DefaultVirtualKeyId,
            string virtualKeyName = DefaultVirtualKeyName,
            string connectionId = DefaultConnectionId)
        {
            var items = new Dictionary<object, object?>();
            if (virtualKeyId.HasValue)
            {
                items["VirtualKeyId"] = virtualKeyId.Value;
                items["VirtualKeyName"] = virtualKeyName;
            }

            var mock = new Mock<HubCallerContext>();
            mock.Setup(x => x.Items).Returns(items);
            mock.Setup(x => x.ConnectionId).Returns(connectionId);
            mock.Setup(x => x.Features).Returns(new FeatureCollection());
            mock.Setup(x => x.ConnectionAborted).Returns(CancellationToken.None);

            return mock;
        }

        /// <summary>
        /// Creates a mock HubCallerContext without a virtual key (unauthorized).
        /// </summary>
        protected Mock<HubCallerContext> CreateUnauthorizedHubCallerContext(
            string connectionId = DefaultConnectionId)
        {
            return CreateHubCallerContext(virtualKeyId: null, connectionId: connectionId);
        }

        /// <summary>
        /// Sets up the auth service to return the specified virtual key ID and name.
        /// </summary>
        protected void SetupAuthServiceReturnsVirtualKey(int virtualKeyId, string name = DefaultVirtualKeyName)
        {
            MockAuthService.Setup(x => x.GetVirtualKeyId(It.IsAny<HubCallerContext>()))
                .Returns(virtualKeyId);
            MockAuthService.Setup(x => x.GetVirtualKeyName(It.IsAny<HubCallerContext>()))
                .Returns(name);
        }

        /// <summary>
        /// Sets up the auth service to return no virtual key (unauthorized).
        /// </summary>
        protected void SetupAuthServiceReturnsNoVirtualKey()
        {
            MockAuthService.Setup(x => x.GetVirtualKeyId(It.IsAny<HubCallerContext>()))
                .Returns((int?)null);
            MockAuthService.Setup(x => x.GetVirtualKeyName(It.IsAny<HubCallerContext>()))
                .Returns("Unknown");
        }

        /// <summary>
        /// Sets up the auth service to return a virtual key entity.
        /// </summary>
        protected void SetupAuthServiceReturnsVirtualKeyEntity(VirtualKey virtualKey)
        {
            MockAuthService.Setup(x => x.GetAuthenticatedVirtualKeyAsync(It.IsAny<HubCallerContext>()))
                .ReturnsAsync(virtualKey);
        }

        /// <summary>
        /// Sets up the auth service for admin status.
        /// </summary>
        protected void SetupAuthServiceIsAdmin(bool isAdmin)
        {
            MockAuthService.Setup(x => x.IsAdminAsync(It.IsAny<HubCallerContext>()))
                .ReturnsAsync(isAdmin);
        }

        /// <summary>
        /// Sets up the auth service for resource access.
        /// </summary>
        protected void SetupAuthServiceCanAccessResource(string resourceType, string resourceId, bool canAccess)
        {
            MockAuthService.Setup(x => x.CanAccessResourceAsync(
                    It.IsAny<HubCallerContext>(), resourceType, resourceId))
                .ReturnsAsync(canAccess);
        }

        /// <summary>
        /// Verifies that a connection was added to the specified group.
        /// </summary>
        protected void VerifyAddedToGroup(string groupName, string? connectionId = null, Times? times = null)
        {
            MockGroups.Verify(
                x => x.AddToGroupAsync(
                    connectionId ?? DefaultConnectionId,
                    groupName,
                    It.IsAny<CancellationToken>()),
                times ?? Times.Once());
        }

        /// <summary>
        /// Verifies that a connection was removed from the specified group.
        /// </summary>
        protected void VerifyRemovedFromGroup(string groupName, string? connectionId = null, Times? times = null)
        {
            MockGroups.Verify(
                x => x.RemoveFromGroupAsync(
                    connectionId ?? DefaultConnectionId,
                    groupName,
                    It.IsAny<CancellationToken>()),
                times ?? Times.Once());
        }

        /// <summary>
        /// Verifies that a message was sent to the specified group.
        /// </summary>
        protected void VerifySentToGroup(string groupName, string methodName, Times? times = null)
        {
            MockClients.Verify(x => x.Group(groupName), times ?? Times.Once());
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    methodName,
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                times ?? Times.Once());
        }

        /// <summary>
        /// Verifies that a specific message was sent to a group.
        /// </summary>
        protected void VerifySentToGroupWithArgs<T>(string groupName, string methodName, T expectedArg)
        {
            MockClients.Verify(x => x.Group(groupName), Times.AtLeastOnce());
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    methodName,
                    It.Is<object[]>(args => args.Length > 0 && args[0] is T),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        /// <summary>
        /// Verifies that no message was sent to any group.
        /// </summary>
        protected void VerifyNoMessagesSent()
        {
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        private void SetupClientMocks()
        {
            // Setup client mock chain
            MockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(MockClientProxy.Object);
            MockClients.Setup(x => x.All).Returns(MockClientProxy.Object);
            // Note: Caller returns ISingleClientProxy, but we only mock Group for hub tests

            // Setup default SendCoreAsync behavior
            MockClientProxy.Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        private void SetupServiceProviderMocks()
        {
            MockServiceProvider.Setup(x => x.GetService(typeof(ISignalRAuthenticationService)))
                .Returns(MockAuthService.Object);
            // Use RealMetrics by default since Counter<long> can't be mocked
            MockServiceProvider.Setup(x => x.GetService(typeof(ISignalRMetrics)))
                .Returns(RealMetrics);
        }

        /// <summary>
        /// Creates a virtual key group name for the given virtual key ID.
        /// </summary>
        protected static string VirtualKeyGroup(int virtualKeyId) => $"vkey-{virtualKeyId}";

        /// <summary>
        /// Creates a task group name for the given task ID.
        /// </summary>
        protected static string TaskGroup(string taskId) => $"task-{taskId}";

        /// <summary>
        /// Creates a task type group name for the given virtual key ID and task type.
        /// </summary>
        protected static string TaskTypeGroup(int virtualKeyId, string taskType) => $"vkey-{virtualKeyId}-{taskType}";
    }
}
