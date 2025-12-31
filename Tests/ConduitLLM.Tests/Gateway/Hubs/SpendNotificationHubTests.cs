using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Gateway.Hubs;

namespace ConduitLLM.Tests.Gateway.Hubs
{
    /// <summary>
    /// Unit tests for the SpendNotificationHub SignalR hub.
    /// Tests spend notifications, budget alerts, and threshold management.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "SignalR")]
    [Trait("Feature", "SpendNotificationHub")]
    public class SpendNotificationHubTests : HubTestBase, IDisposable
    {
        private readonly Mock<ILogger<SpendNotificationHub>> _mockLogger;

        public SpendNotificationHubTests(ITestOutputHelper output) : base(output)
        {
            _mockLogger = CreateLogger<SpendNotificationHub>();
            // Note: We use RealMetrics from HubTestBase since Counter<long> is a sealed struct
            // that cannot be mocked. RealMetrics provides working counter instances.
        }

        /// <summary>
        /// Cleanup any static state between tests.
        /// Note: SpendNotificationHub uses a static ConcurrentDictionary for cooldowns.
        /// </summary>
        public new void Dispose()
        {
            // In a real scenario, we'd need a way to reset static state.
            // For now, tests should use unique virtual key IDs.
            base.Dispose();
        }

        /// <summary>
        /// Creates a SpendNotificationHub instance with mocked context.
        /// </summary>
        private SpendNotificationHub CreateHub(int? virtualKeyId = DefaultVirtualKeyId)
        {
            var hub = new SpendNotificationHub(
                RealMetrics,
                _mockLogger.Object,
                MockServiceProvider.Object);

            // Create and configure context
            var context = CreateHubCallerContext(virtualKeyId);

            // Set up auth service
            if (virtualKeyId.HasValue)
            {
                SetupAuthServiceReturnsVirtualKey(virtualKeyId.Value);
            }
            else
            {
                SetupAuthServiceReturnsNoVirtualKey();
            }

            // Set Context, Groups, and Clients on hub using reflection
            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
            typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
            typeof(Hub).GetProperty("Clients")?.SetValue(hub, MockClients.Object);

            return hub;
        }

        #region Connection Lifecycle Tests

        [Fact]
        public async Task OnConnectedAsync_WithValidVirtualKey_AddsToVirtualKeyGroup()
        {
            // Arrange
            var hub = CreateHub(virtualKeyId: 1001);

            // Act
            await hub.OnConnectedAsync();

            // Assert
            VerifyAddedToGroup(VirtualKeyGroup(1001));
        }

        [Fact]
        public async Task OnConnectedAsync_InitializesAlertCooldown()
        {
            // Arrange
            var hub = CreateHub(virtualKeyId: 1002);

            // Act
            await hub.OnConnectedAsync();

            // Assert - Verify logging indicates initialization
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Client connected to SpendNotificationHub")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        [Fact]
        public async Task OnConnectedAsync_WithoutVirtualKey_AbortsConnection()
        {
            // Arrange
            var context = CreateUnauthorizedHubCallerContext();
            var abortCalled = false;
            context.Setup(x => x.Abort()).Callback(() => abortCalled = true);

            var hub = new SpendNotificationHub(RealMetrics, _mockLogger.Object, MockServiceProvider.Object);
            SetupAuthServiceReturnsNoVirtualKey();

            typeof(Hub).GetProperty("Context")?.SetValue(hub, context.Object);
            typeof(Hub).GetProperty("Groups")?.SetValue(hub, MockGroups.Object);
            typeof(Hub).GetProperty("Clients")?.SetValue(hub, MockClients.Object);

            // Act
            await hub.OnConnectedAsync();

            // Assert
            Assert.True(abortCalled, "Context.Abort() should have been called");
        }

        #endregion

        #region SendSpendUpdate Tests

        [Fact]
        public async Task SendSpendUpdate_SendsToVirtualKeyGroup()
        {
            // Arrange
            var virtualKeyId = 2001;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 0.05m,
                TotalSpend = 10.50m,
                Model = "gpt-4",
                Provider = "OpenAI"
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert
            VerifySentToGroup(VirtualKeyGroup(virtualKeyId), "SpendUpdate");
        }

        [Fact]
        public async Task SendSpendUpdate_WhenException_ThrowsAndLogsError()
        {
            // Arrange
            var virtualKeyId = 2003;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification { NewSpend = 0.05m, TotalSpend = 1.0m };

            // Setup exception
            MockClientProxy.Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("SignalR error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => hub.SendSpendUpdate(virtualKeyId, notification));

            // Verify error was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Error sending spend update")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        [Fact]
        public async Task SendSpendUpdate_WithBudget_ChecksThresholds()
        {
            // Arrange - use unique virtual key ID to avoid cooldown state from other tests
            var virtualKeyId = 2004;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 5.0m,
                TotalSpend = 85.0m,
                Budget = 100.0m,
                BudgetPercentage = 85m // 85% - should trigger 80% threshold
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Should send both SpendUpdate and BudgetAlert
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());

            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task SendSpendUpdate_LogsInformation()
        {
            // Arrange
            var virtualKeyId = 2005;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 0.25m,
                TotalSpend = 15.75m
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Sent spend update")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        #endregion

        #region SendBudgetAlert Tests

        [Fact]
        public async Task SendBudgetAlert_SendsToVirtualKeyGroup()
        {
            // Arrange
            var virtualKeyId = 3001;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var alert = new BudgetAlertNotification
            {
                PercentageUsed = 80.0,
                CurrentSpend = 80.0m,
                BudgetLimit = 100.0m,
                Severity = "warning",
                Message = "80% of budget used"
            };

            // Act
            await hub.SendBudgetAlert(virtualKeyId, alert);

            // Assert
            VerifySentToGroup(VirtualKeyGroup(virtualKeyId), "BudgetAlert");
        }

        [Fact]
        public async Task SendBudgetAlert_SendsCorrectMethod()
        {
            // Arrange
            var virtualKeyId = 3002;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var alert = new BudgetAlertNotification
            {
                PercentageUsed = 90.0,
                Severity = "warning"
            };

            // Act
            await hub.SendBudgetAlert(virtualKeyId, alert);

            // Assert - verify correct method is called
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task SendBudgetAlert_LogsWarning()
        {
            // Arrange
            var virtualKeyId = 3003;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var alert = new BudgetAlertNotification
            {
                PercentageUsed = 95.0,
                Severity = "warning"
            };

            // Act
            await hub.SendBudgetAlert(virtualKeyId, alert);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Sent budget alert")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        #endregion

        #region SendSpendSummary Tests

        [Fact]
        public async Task SendSpendSummary_SendsToVirtualKeyGroup()
        {
            // Arrange
            var virtualKeyId = 4001;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var summary = new SpendSummaryNotification
            {
                PeriodType = "daily",
                TotalSpend = 150.00m,
                RequestCount = 500,
                AverageRequestCost = 0.30m
            };

            // Act
            await hub.SendSpendSummary(virtualKeyId, summary);

            // Assert
            VerifySentToGroup(VirtualKeyGroup(virtualKeyId), "SpendSummary");
        }

        [Fact]
        public async Task SendSpendSummary_SendsCorrectMethod()
        {
            // Arrange
            var virtualKeyId = 4002;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var summary = new SpendSummaryNotification
            {
                PeriodType = "hourly",
                TotalSpend = 25.00m
            };

            // Act
            await hub.SendSpendSummary(virtualKeyId, summary);

            // Assert - verify correct method is called
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendSummary",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        [Fact]
        public async Task SendSpendSummary_LogsInformation()
        {
            // Arrange
            var virtualKeyId = 4003;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var summary = new SpendSummaryNotification
            {
                PeriodType = "weekly",
                TotalSpend = 500.00m
            };

            // Act
            await hub.SendSpendSummary(virtualKeyId, summary);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Sent") && o.ToString()!.Contains("spend summary")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        #endregion

        #region SendUnusualSpendingAlert Tests

        [Fact]
        public async Task SendUnusualSpendingAlert_SendsToVirtualKeyGroup()
        {
            // Arrange
            var virtualKeyId = 5001;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new UnusualSpendingNotification
            {
                ActivityType = "Spike",
                Description = "Unusual spending spike detected",
                CurrentRate = 50.0m,
                NormalRate = 10.0m,
                DeviationPercentage = 400.0
            };

            // Act
            await hub.SendUnusualSpendingAlert(virtualKeyId, notification);

            // Assert
            VerifySentToGroup(VirtualKeyGroup(virtualKeyId), "UnusualSpendingDetected");
        }

        [Fact]
        public async Task SendUnusualSpendingAlert_LogsWarning()
        {
            // Arrange
            var virtualKeyId = 5002;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new UnusualSpendingNotification
            {
                ActivityType = "RateIncrease",
                DeviationPercentage = 250.0
            };

            // Act
            await hub.SendUnusualSpendingAlert(virtualKeyId, notification);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("unusual spending alert")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }

        [Fact]
        public async Task SendUnusualSpendingAlert_SendsCorrectMethod()
        {
            // Arrange
            var virtualKeyId = 5003;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new UnusualSpendingNotification
            {
                ActivityType = "HighCost",
                DeviationPercentage = 150.0
            };

            // Act
            await hub.SendUnusualSpendingAlert(virtualKeyId, notification);

            // Assert - verify correct method is called
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "UnusualSpendingDetected",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        #endregion

        #region Budget Threshold Tests

        [Fact]
        public async Task CheckAndSendBudgetAlerts_At50Percent_SendsInfoAlert()
        {
            // Arrange - Use unique virtual key ID
            var virtualKeyId = 6001;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 1.0m,
                TotalSpend = 50.0m,
                Budget = 100.0m,
                BudgetPercentage = 50m // 50%
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Should trigger 50% threshold alert
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => args.Length > 0 &&
                        args[0] != null &&
                        ((BudgetAlertNotification)args[0]).Severity == "info"),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckAndSendBudgetAlerts_At90Percent_SendsWarningAlert()
        {
            // Arrange - Use unique virtual key ID
            var virtualKeyId = 6002;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 1.0m,
                TotalSpend = 90.0m,
                Budget = 100.0m,
                BudgetPercentage = 90m // 90%
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Should trigger warning severity
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => args.Length > 0 &&
                        args[0] != null &&
                        ((BudgetAlertNotification)args[0]).Severity == "warning"),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckAndSendBudgetAlerts_At100Percent_SendsCriticalAlert()
        {
            // Arrange - Use unique virtual key ID
            var virtualKeyId = 6003;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 1.0m,
                TotalSpend = 100.0m,
                Budget = 100.0m,
                BudgetPercentage = 100m // 100%
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Should trigger critical severity
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.Is<object[]>(args => args.Length > 0 &&
                        args[0] != null &&
                        ((BudgetAlertNotification)args[0]).Severity == "critical"),
                    It.IsAny<CancellationToken>()),
                Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckAndSendBudgetAlerts_WithNoBudget_DoesNotSendAlerts()
        {
            // Arrange
            var virtualKeyId = 6004;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 50.0m,
                TotalSpend = 500.0m,
                Budget = null, // No budget set
                BudgetPercentage = null
            };

            // Act
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - Only SpendUpdate should be sent, not BudgetAlert
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());

            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Never());
        }

        [Fact]
        public async Task CheckAndSendBudgetAlerts_CooldownPreventsSpam()
        {
            // Arrange - Use unique virtual key ID
            // Use 55% to only trigger the 50% threshold (not 80%, 90%, or 100%)
            var virtualKeyId = 6005;
            var hub = CreateHub(virtualKeyId);
            await hub.OnConnectedAsync();

            var notification = new SpendUpdateNotification
            {
                NewSpend = 1.0m,
                TotalSpend = 55.0m,
                Budget = 100.0m,
                BudgetPercentage = 55m // Only crosses 50% threshold
            };

            // Act - Send twice
            await hub.SendSpendUpdate(virtualKeyId, notification);
            await hub.SendSpendUpdate(virtualKeyId, notification);

            // Assert - BudgetAlert should only be sent once due to cooldown
            // (50% threshold triggered on first call, blocked on second)
            MockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "BudgetAlert",
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()),
                Times.Once());
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullMetrics_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SpendNotificationHub(null!, _mockLogger.Object, MockServiceProvider.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new SpendNotificationHub(RealMetrics, null!, MockServiceProvider.Object));
        }

        #endregion
    }
}
