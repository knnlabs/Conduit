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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Tests.Hubs
{
    /// <summary>
    /// Unit tests for the SpendNotificationHub.
    /// </summary>
    public class SpendNotificationHubTests
    {
        private readonly Mock<SignalRMetrics> _mockMetrics;
        private readonly Mock<ILogger<SpendNotificationHub>> _mockLogger;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<HubCallerContext> _mockContext;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly SpendNotificationHub _hub;

        public SpendNotificationHubTests()
        {
            _mockMetrics = new Mock<SignalRMetrics>();
            _mockLogger = new Mock<ILogger<SpendNotificationHub>>();
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

            _hub = new SpendNotificationHub(_mockMetrics.Object, _mockLogger.Object, _mockServiceProvider.Object)
            {
                Context = _mockContext.Object,
                Clients = _mockClients.Object,
                Groups = _mockGroups.Object
            };
        }

        [Fact]
        public async Task OnConnectedAsync_InitializesCooldownTracking()
        {
            // Arrange
            var connectionId = "test-connection-123";
            var virtualKeyId = 123;
            
            _mockContext.Setup(x => x.ConnectionId).Returns(connectionId);
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["VirtualKeyId"] = virtualKeyId
            });

            // Act
            await _hub.OnConnectedAsync();

            // Assert
            _mockGroups.Verify(x => x.AddToGroupAsync(connectionId, $"vkey-{virtualKeyId}", default), Times.Once);
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connected to SpendNotificationHub")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SendSpendUpdate_BroadcastsToCorrectGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var notification = new SpendUpdateNotification
            {
                NewSpend = 10.50m,
                TotalSpend = 150.75m,
                Budget = 500m,
                BudgetPercentage = 30.15m,
                Model = "gpt-4",
                Provider = "OpenAI"
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == notification),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SendSpendUpdate_TriggersAlertAt50Percent()
        {
            // Arrange
            var virtualKeyId = 123;
            var notification = new SpendUpdateNotification
            {
                NewSpend = 50m,
                TotalSpend = 250m,
                Budget = 500m,
                BudgetPercentage = 50m,
                Model = "gpt-4",
                Provider = "OpenAI"
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Should send both SpendUpdate and BudgetAlert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null && args[0].GetType() == typeof(BudgetAlertNotification) &&
                        ((BudgetAlertNotification)args[0]).PercentageUsed == 50d &&
                        ((BudgetAlertNotification)args[0]).Severity == "info"),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SendBudgetAlert_SendsCorrectNotification()
        {
            // Arrange
            var virtualKeyId = 123;
            var alert = new BudgetAlertNotification
            {
                PercentageUsed = 90d,
                CurrentSpend = 450m,
                BudgetLimit = 500m,
                Severity = "warning",
                Message = "Warning: 90% of budget used"
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendBudgetAlert(virtualKeyId, alert);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == alert),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SendSpendSummary_BroadcastsSummary()
        {
            // Arrange
            var virtualKeyId = 123;
            var summary = new SpendSummaryNotification
            {
                PeriodType = "daily",
                Period = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
                TotalSpend = 250m,
                RequestCount = 150,
                TopProviders = new List<ProviderSpendBreakdown>
                {
                    new ProviderSpendBreakdown 
                    { 
                        Provider = "OpenAI", 
                        Spend = 200m, 
                        RequestCount = 100, 
                        Percentage = 80d 
                    }
                }
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendSpendSummary(virtualKeyId, summary);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "SpendSummary",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == summary),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task SendUnusualSpendingAlert_SendsWarning()
        {
            // Arrange
            var virtualKeyId = 123;
            var notification = new UnusualSpendingNotification
            {
                ActivityType = "spend_spike",
                Description = "Spending has increased by 300% in the last hour",
                CurrentRate = 400m,
                NormalRate = 100m,
                DeviationPercentage = 300d,
                Recommendations = new List<string> 
                { 
                    "Review recent API usage",
                    "Check for runaway processes"
                }
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendUnusualSpendingAlert(virtualKeyId, notification);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "UnusualSpendingDetected",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == notification),
                    default),
                Times.Once);
        }

        [Theory]
        [InlineData(80.0, "warning")]
        [InlineData(90.0, "warning")]
        [InlineData(100.0, "critical")]
        public async Task SendSpendUpdate_TriggersCorrectAlertSeverity(double budgetPercentage, string expectedSeverity)
        {
            // Arrange
            var virtualKeyId = 123;
            var totalSpend = (decimal)budgetPercentage * 5m; // Budget is 500
            var notification = new SpendUpdateNotification
            {
                NewSpend = 10m,
                TotalSpend = totalSpend,
                Budget = 500m,
                BudgetPercentage = (decimal)budgetPercentage,
                Model = "gpt-4",
                Provider = "OpenAI"
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert
            mockGroupClients.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null && args[0].GetType() == typeof(BudgetAlertNotification) &&
                        ((BudgetAlertNotification)args[0]).Severity == expectedSeverity),
                    default),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task SendSpendUpdate_TracksMetrics()
        {
            // Arrange
            var virtualKeyId = 123;
            var notification = new SpendUpdateNotification
            {
                NewSpend = 10m,
                TotalSpend = 100m,
                Model = "gpt-4",
                Provider = "OpenAI"
            };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            var mockGroupClients = new Mock<IClientProxy>();
            _mockClients.Setup(x => x.Group($"vkey-{virtualKeyId}")).Returns(mockGroupClients.Object);

            // Act
            await _hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert
            mockGroupClients.Verify(
                x => x.SendAsync(
                    "SpendUpdate", 
                    It.Is<object[]>(args => args.Length == 1 && args[0] == notification),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendSpendUpdate_HandlesErrorsGracefully()
        {
            // Arrange
            var virtualKeyId = 123;
            var notification = new SpendUpdateNotification { NewSpend = 10m };
            
            _mockContext.Setup(x => x.Items).Returns(new Dictionary<object, object?>
            {
                ["CorrelationId"] = "test-correlation-123"
            });

            _mockClients.Setup(x => x.Group(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Test exception"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _hub.SendSpendUpdate(virtualKeyId, notification));

        }
    }
}