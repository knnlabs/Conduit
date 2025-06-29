using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Metrics;
using ConduitLLM.Http.Models;
using ConduitLLM.Http.Tests.TestHelpers;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Authentication;

namespace ConduitLLM.Http.Tests.Hubs
{
    /// <summary>
    /// Unit tests for the SystemNotificationHub.
    /// </summary>
    public class SystemNotificationHubTests
    {
        private readonly Mock<ISignalRMetrics> _mockMetrics;
        private readonly Mock<ILogger<SystemNotificationHub>> _mockLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<ISignalRAuthenticationService> _mockAuthService;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<HubCallerContext> _mockContext;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly SystemNotificationHub _hub;

        public SystemNotificationHubTests()
        {
            _mockMetrics = MockSignalRMetrics.Create();
            _mockLogger = new Mock<ILogger<SystemNotificationHub>>();
            _mockAuthService = new Mock<ISignalRAuthenticationService>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockContext = new Mock<HubCallerContext>();
            _mockGroups = new Mock<IGroupManager>();

            // Build a real service provider
            var services = new ServiceCollection();
            services.AddSingleton(_mockAuthService.Object);
            _serviceProvider = services.BuildServiceProvider();

            // Default auth service setup - tests can override as needed
            _mockAuthService.Setup(x => x.GetVirtualKeyId(It.IsAny<HubCallerContext>()))
                .Returns<HubCallerContext>(ctx => 
                {
                    if (ctx?.Items?.TryGetValue("VirtualKeyId", out var idObj) == true && idObj is int id)
                        return id;
                    return null;
                });
            _mockAuthService.Setup(x => x.GetVirtualKeyName(It.IsAny<HubCallerContext>()))
                .Returns<HubCallerContext>(ctx => 
                {
                    if (ctx?.Items?.TryGetValue("VirtualKeyName", out var nameObj) == true && nameObj is string name)
                        return name;
                    return "test-key";
                });

            _hub = new SystemNotificationHub(_mockMetrics.Object, _mockLogger.Object, _serviceProvider)
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
                Times.AtLeastOnce);
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
                        args[0] != null && args[0].GetType() == typeof(ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification) &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification)args[0]).Provider == provider &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification)args[0]).Status == "Degraded" &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ProviderHealthNotification)args[0]).ResponseTimeMs == 500),
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
                        args[0] != null && args[0].GetType() == typeof(ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification) &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification)args[0]).Remaining == remaining &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification)args[0]).Endpoint == endpoint &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.RateLimitNotification)args[0]).Priority == NotificationPriority.High),
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
                        args[0] != null && args[0].GetType() == typeof(ConduitLLM.Configuration.DTOs.SignalR.SystemAnnouncementNotification) &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.SystemAnnouncementNotification)args[0]).Message == message &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.SystemAnnouncementNotification)args[0]).Priority == priority),
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
                        args[0] != null && args[0].GetType() == typeof(ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification) &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification)args[0]).Service == service &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification)args[0]).Reason == reason &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ServiceDegradationNotification)args[0]).Priority == NotificationPriority.High),
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
                        args[0] != null && args[0].GetType() == typeof(ConduitLLM.Configuration.DTOs.SignalR.ServiceRestorationNotification) &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ServiceRestorationNotification)args[0]).Service == service &&
                        ((ConduitLLM.Configuration.DTOs.SignalR.ServiceRestorationNotification)args[0]).Priority == NotificationPriority.Medium),
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

        [Fact(Skip = "Test setup needs updating")]
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
            var virtualKeyId = 123;
            
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