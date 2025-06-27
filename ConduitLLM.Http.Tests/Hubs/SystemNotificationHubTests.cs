using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Metrics;
using ConduitLLM.Http.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Tests.Hubs
{
    /// <summary>
    /// Unit tests for the SystemNotificationHub.
    /// </summary>
    public class SystemNotificationHubTests
    {
        private readonly Mock<SignalRMetrics> _mockMetrics;
        private readonly Mock<ILogger<SystemNotificationHub>> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<HubCallerContext> _mockContext;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly SystemNotificationHub _hub;

        public SystemNotificationHubTests()
        {
            _mockMetrics = new Mock<SignalRMetrics>();
            _mockLogger = new Mock<ILogger<SystemNotificationHub>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockContext = new Mock<HubCallerContext>();
            _mockGroups = new Mock<IGroupManager>();

            // Setup metrics
            var mockMeter = new Mock<Meter>("test");
            var mockCounter = mockMeter.Object.CreateCounter<long>("test_counter");
            var mockUpDownCounter = mockMeter.Object.CreateUpDownCounter<long>("test_updown");
            
            _mockMetrics.Setup(m => m.ConnectionsTotal).Returns(mockCounter);
            _mockMetrics.Setup(m => m.ActiveConnections).Returns(mockUpDownCounter);
            _mockMetrics.Setup(m => m.MessagesSent).Returns(mockCounter);
            _mockMetrics.Setup(m => m.HubErrors).Returns(mockCounter);
            _mockMetrics.Setup(m => m.AuthenticationFailures).Returns(mockCounter);

            _hub = new SystemNotificationHub(_mockMetrics.Object, _mockLogger.Object, _mockServiceProvider.Object)
            {
                Context = _mockContext.Object,
                Clients = _mockClients.Object,
                Groups = _mockGroups.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_InitializesPreferences()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var virtualKeyId = "vk_test123";
            var userIdentifier = "user123";
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockContext.Setup(x => x.UserIdentifier).Returns(userIdentifier);
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = 123 // Use integer for VirtualKeyId
            });

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockGroups.Verify(x => x.AddToGroupAsync(connectionId, "vkey-123", default), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connected to SystemNotificationHub")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ProviderHealthChanged_SendsNotificationToGroup()
        {
            // Arrange
            var provider = "OpenAI";
            var status = HealthStatus.Degraded;
            var responseTime = TimeSpan.FromMilliseconds(500);
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.ProviderHealthChanged(provider, status, responseTime);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "Onprovider_health",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] is ProviderHealthNotification notification &&
                        notification.Provider == provider &&
                        notification.Status == "Degraded" &&
                        notification.ResponseTimeMs == 500),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task RateLimitWarning_SetsCriticalPriorityForLowRemaining()
        {
            // Arrange
            var remaining = 5;
            var resetTime = DateTime.UtcNow.AddMinutes(5);
            var endpoint = "/v1/chat/completions";
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.RateLimitWarning(remaining, resetTime, endpoint);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "Onrate_limit",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] is RateLimitNotification notification &&
                        notification.Remaining == remaining &&
                        notification.Endpoint == endpoint &&
                        notification.Priority == NotificationPriority.High),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SystemAnnouncement_BroadcastsToGroup()
        {
            // Arrange
            var message = "Scheduled maintenance at 2 AM UTC";
            var priority = NotificationPriority.Medium;
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SystemAnnouncement(message, priority);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "Onsystem_announcement",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] is SystemAnnouncementNotification notification &&
                        notification.Message == message &&
                        notification.Priority == priority),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task ServiceDegraded_SendsHighPriorityNotification()
        {
            // Arrange
            var service = "Image Generation";
            var reason = "High load causing slow response times";
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.ServiceDegraded(service, reason);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "Onservice_degradation",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] is ServiceDegradationNotification notification &&
                        notification.Service == service &&
                        notification.Reason == reason &&
                        notification.Priority == NotificationPriority.High),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task ServiceRestored_SendsMediumPriorityNotification()
        {
            // Arrange
            var service = "Image Generation";
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.ServiceRestored(service);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "Onservice_restoration",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] is ServiceRestorationNotification notification &&
                        notification.Service == service &&
                        notification.Priority == NotificationPriority.Medium),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task UpdatePreferences_UpdatesClientPreferences()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var preferences = new SystemNotificationHub.NotificationPreferences
            {
                EnabledTypes = new HashSet<string> { "provider_health", "rate_limit" },
                MinimumPriority = NotificationPriority.High
            };
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockCaller = new Mock<ISingleClientProxy>();
            _mockClients.Setup(x => x.Caller).Returns(mockCaller.Object);

            // Act
            await _hub.UpdatePreferences(preferences);

            // Assert
            mockCaller.Verify(
                x => x.SendCoreAsync(
                    "PreferencesUpdated",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == preferences),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task BroadcastNotification_LogsWarningWhenNoVirtualKey()
        {
            // Arrange
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>());

            // Act
            await _hub.ProviderHealthChanged("TestProvider", HealthStatus.Healthy, null);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cannot broadcast notification - no virtual key")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task BroadcastNotification_TracksMetrics()
        {
            // Arrange
            var virtualKeyId = "vk_test123";
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId,
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.ProviderHealthChanged("TestProvider", HealthStatus.Healthy, null);

            // Assert
            mockGroupClients.Verify(
                x => x.SendAsync(
                    "ProviderHealthUpdate",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OnDisconnectedAsync_CleansUpResources()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var virtualKeyId = "vk_test123";
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            });

            // First connect to set up state
            await _hub.OnConnectedAsync();

            // Act
            await _hub.OnDisconnectedAsync(null);

            // Assert
            _mockGroups.Verify(x => x.RemoveFromGroupAsync(connectionId, $"vkey-{virtualKeyId}", default), Times.Once);
        }
    }
}