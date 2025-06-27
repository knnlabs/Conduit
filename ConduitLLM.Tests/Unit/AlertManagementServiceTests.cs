using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.Services;
using ConduitLLM.Http.DTOs.HealthMonitoring;
using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Tests.Unit
{
    /// <summary>
    /// Unit tests for the AlertManagementService
    /// </summary>
    public class AlertManagementServiceTests
    {
        private readonly Mock<ILogger<AlertManagementService>> _loggerMock;
        private readonly Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache> _cacheMock;
        private readonly Mock<IHubContext<HealthMonitoringHub>> _hubContextMock;
        private readonly Mock<IServiceProvider> _serviceProviderMock;
        private readonly AlertManagementService _service;

        public AlertManagementServiceTests()
        {
            _loggerMock = new Mock<ILogger<AlertManagementService>>();
            _cacheMock = new Mock<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
            _hubContextMock = new Mock<IHubContext<HealthMonitoringHub>>();
            _serviceProviderMock = new Mock<IServiceProvider>();

            // Setup cache mock
            object cacheValue;
            _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheValue))
                .Returns(false);
            _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>()))
                .Returns(Mock.Of<Microsoft.Extensions.Caching.Memory.ICacheEntry>());

            _service = new AlertManagementService(
                _cacheMock.Object,
                _loggerMock.Object,
                _hubContextMock.Object,
                _serviceProviderMock.Object);
        }

        [Fact]
        public async Task TriggerAlertAsync_Should_CreateNewAlert()
        {
            // Arrange
            var alert = new HealthAlert
            {
                Severity = AlertSeverity.Warning,
                Type = AlertType.PerformanceDegradation,
                Component = "API",
                Title = "Slow Response",
                Message = "API response time exceeds threshold"
            };

            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            var mockChannel = new Mock<INotificationChannel>();
            mockChannel.Setup(x => x.SendAlertAsync(It.IsAny<HealthAlert>()))
                .ReturnsAsync(true);
            _channelFactoryMock.Setup(x => x.CreateChannel(It.IsAny<string>()))
                .Returns(mockChannel.Object);

            // Act
            await _service.TriggerAlertAsync(alert);

            // Assert
            mockClients.Verify(x => x.HealthAlert(It.Is<HealthAlert>(a => 
                a.Title == alert.Title && 
                a.Component == alert.Component)), Times.Once);
            mockChannel.Verify(x => x.SendAlertAsync(It.IsAny<HealthAlert>()), Times.Exactly(2)); // Email and Webhook
        }

        [Fact]
        public async Task TriggerAlertAsync_Should_RespectCooldownPeriod()
        {
            // Arrange
            var alert1 = new HealthAlert
            {
                Severity = AlertSeverity.Warning,
                Type = AlertType.PerformanceDegradation,
                Component = "API",
                Title = "Slow Response",
                Message = "API response time exceeds threshold"
            };

            var alert2 = new HealthAlert
            {
                Severity = AlertSeverity.Warning,
                Type = AlertType.PerformanceDegradation,
                Component = "API",
                Title = "Slow Response",
                Message = "API response time still exceeds threshold"
            };

            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            // Act
            await _service.TriggerAlertAsync(alert1);
            await _service.TriggerAlertAsync(alert2); // Should be ignored due to cooldown

            // Assert
            mockClients.Verify(x => x.HealthAlert(It.IsAny<HealthAlert>()), Times.Once);
        }

        [Fact]
        public async Task AcknowledgeAlertAsync_Should_UpdateAlert()
        {
            // Arrange
            var alert = new HealthAlert
            {
                Severity = AlertSeverity.Error,
                Type = AlertType.ServiceDown,
                Component = "Database",
                Title = "Database Unavailable",
                Message = "Cannot connect to database"
            };

            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            await _service.TriggerAlertAsync(alert);

            // Act
            var result = await _service.AcknowledgeAlertAsync(alert.Id, "admin", "Investigating issue");

            // Assert
            Assert.True(result);
            var activeAlerts = await _service.GetActiveAlertsAsync();
            var acknowledgedAlert = activeAlerts.FirstOrDefault(a => a.Id == alert.Id);
            Assert.NotNull(acknowledgedAlert);
            Assert.NotNull(acknowledgedAlert.AcknowledgedAt);
            Assert.Equal("admin", acknowledgedAlert.AcknowledgedBy);
        }

        [Fact]
        public async Task ResolveAlertAsync_Should_RemoveFromActiveAlerts()
        {
            // Arrange
            var alert = new HealthAlert
            {
                Severity = AlertSeverity.Critical,
                Type = AlertType.ServiceDown,
                Component = "Redis",
                Title = "Redis Connection Lost",
                Message = "Cannot connect to Redis server"
            };

            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            await _service.TriggerAlertAsync(alert);

            // Act
            var result = await _service.ResolveAlertAsync(alert.Id, "admin", "Redis restarted and connection restored");

            // Assert
            Assert.True(result);
            var activeAlerts = await _service.GetActiveAlertsAsync();
            Assert.DoesNotContain(activeAlerts, a => a.Id == alert.Id);
        }

        [Fact]
        public async Task CreateSuppressionAsync_Should_PreventMatchingAlerts()
        {
            // Arrange
            var suppression = new AlertSuppression
            {
                AlertPattern = "Database*",
                Component = "Database",
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(30),
                Reason = "Scheduled maintenance",
                CreatedBy = "admin"
            };

            var suppressionId = await _service.CreateSuppressionAsync(suppression);

            var alert = new HealthAlert
            {
                Severity = AlertSeverity.Error,
                Type = AlertType.ServiceDown,
                Component = "Database",
                Title = "Database Connection Failed",
                Message = "Cannot connect to database during maintenance"
            };

            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            // Act
            await _service.TriggerAlertAsync(alert);

            // Assert
            Assert.NotNull(suppressionId);
            var activeAlerts = await _service.GetActiveAlertsAsync();
            var suppressedAlert = activeAlerts.FirstOrDefault(a => a.Id == alert.Id);
            
            // Alert should either not exist or be marked as suppressed
            Assert.True(suppressedAlert == null || suppressedAlert.IsSuppressed);
        }

        [Fact]
        public async Task GetActiveAlertsAsync_Should_FilterBySeverity()
        {
            // Arrange
            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            var alerts = new[]
            {
                new HealthAlert { Severity = AlertSeverity.Critical, Type = AlertType.ServiceDown, Component = "API", Title = "API Down" },
                new HealthAlert { Severity = AlertSeverity.Warning, Type = AlertType.PerformanceDegradation, Component = "DB", Title = "Slow Queries" },
                new HealthAlert { Severity = AlertSeverity.Info, Type = AlertType.Custom, Component = "System", Title = "Info Message" }
            };

            foreach (var alert in alerts)
            {
                await _service.TriggerAlertAsync(alert);
            }

            // Act
            var criticalAlerts = await _service.GetActiveAlertsAsync(severity: AlertSeverity.Critical);
            var warningAlerts = await _service.GetActiveAlertsAsync(severity: AlertSeverity.Warning);

            // Assert
            Assert.Single(criticalAlerts);
            Assert.Equal("API Down", criticalAlerts.First().Title);
            Assert.Single(warningAlerts);
            Assert.Equal("Slow Queries", warningAlerts.First().Title);
        }

        [Fact]
        public async Task GetActiveAlertsAsync_Should_FilterByComponent()
        {
            // Arrange
            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            var alerts = new[]
            {
                new HealthAlert { Severity = AlertSeverity.Error, Type = AlertType.ServiceDown, Component = "Database", Title = "DB Error 1" },
                new HealthAlert { Severity = AlertSeverity.Error, Type = AlertType.ServiceDown, Component = "Database", Title = "DB Error 2" },
                new HealthAlert { Severity = AlertSeverity.Error, Type = AlertType.ServiceDown, Component = "Redis", Title = "Redis Error" }
            };

            foreach (var alert in alerts)
            {
                await _service.TriggerAlertAsync(alert);
            }

            // Act
            var dbAlerts = await _service.GetActiveAlertsAsync(component: "Database");
            var redisAlerts = await _service.GetActiveAlertsAsync(component: "Redis");

            // Assert
            Assert.Equal(2, dbAlerts.Count());
            Assert.Single(redisAlerts);
            Assert.Equal("Redis Error", redisAlerts.First().Title);
        }

        [Fact]
        public async Task MaxActiveAlerts_Should_RemoveOldestAlerts()
        {
            // Arrange
            _options.MaxActiveAlerts = 3;
            var mockClients = new Mock<IHealthMonitoringClient>();
            _hubContextMock.Setup(x => x.Clients.All).Returns(mockClients.Object);

            // Act - Add 4 alerts (exceeds max of 3)
            for (int i = 1; i <= 4; i++)
            {
                var alert = new HealthAlert
                {
                    Severity = AlertSeverity.Warning,
                    Type = AlertType.Custom,
                    Component = "Test",
                    Title = $"Alert {i}",
                    Message = $"Test alert number {i}"
                };
                await _service.TriggerAlertAsync(alert);
                await Task.Delay(100); // Ensure different timestamps
            }

            // Assert
            var activeAlerts = await _service.GetActiveAlertsAsync();
            Assert.Equal(3, activeAlerts.Count());
            Assert.DoesNotContain(activeAlerts, a => a.Title == "Alert 1"); // Oldest should be removed
            Assert.Contains(activeAlerts, a => a.Title == "Alert 4"); // Newest should be present
        }

        [Fact]
        public async Task CancelSuppressionAsync_Should_RemoveSuppression()
        {
            // Arrange
            var suppression = new AlertSuppression
            {
                AlertPattern = "*",
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddHours(1),
                Reason = "Test suppression",
                CreatedBy = "test"
            };

            var suppressionId = await _service.CreateSuppressionAsync(suppression);

            // Act
            var result = await _service.CancelSuppressionAsync(suppressionId);

            // Assert
            Assert.True(result);
            var activeSuppressions = await _service.GetActiveAlertsAsync();
            Assert.Empty(activeSuppressions);
        }

        [Fact]
        public void IsAlertSuppressed_Should_MatchWildcardPatterns()
        {
            // Arrange
            var suppression = new AlertSuppression
            {
                AlertPattern = "Database*Error",
                Component = "Database",
                StartTime = DateTime.UtcNow.AddMinutes(-5),
                EndTime = DateTime.UtcNow.AddMinutes(30)
            };

            // Act & Assert - Matching cases
            Assert.True(_service.TestIsAlertSuppressed(
                new HealthAlert { Title = "Database Connection Error", Component = "Database" },
                new[] { suppression }));
            
            Assert.True(_service.TestIsAlertSuppressed(
                new HealthAlert { Title = "Database Timeout Error", Component = "Database" },
                new[] { suppression }));

            // Non-matching cases
            Assert.False(_service.TestIsAlertSuppressed(
                new HealthAlert { Title = "Database Warning", Component = "Database" },
                new[] { suppression }));
            
            Assert.False(_service.TestIsAlertSuppressed(
                new HealthAlert { Title = "Database Connection Error", Component = "Redis" },
                new[] { suppression }));
        }
    }

    // Extension to expose private method for testing
    public static class AlertManagementServiceExtensions
    {
        public static bool TestIsAlertSuppressed(this AlertManagementService service, HealthAlert alert, IEnumerable<AlertSuppression> suppressions)
        {
            // This would require making the method internal and using InternalsVisibleTo
            // For now, we'll test through the public API
            return false; // Placeholder
        }
    }
}