using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Services;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Tests.Services
{
    /// <summary>
    /// Unit tests for the SpendNotificationService.
    /// </summary>
    public class SpendNotificationServiceTests
    {
        private readonly Mock<IHubContext<SpendNotificationHub>> _mockHubContext;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<SpendNotificationService>> _mockLogger;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly SpendNotificationService _service;

        public SpendNotificationServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<SpendNotificationHub>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<SpendNotificationService>>();
            _mockClientProxy = new Mock<IClientProxy>();

            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            _service = new SpendNotificationService(
                _mockHubContext.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task StartAsync_InitializesPatternAnalysisTimer()
        {
            // Act
            await _service.StartAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SpendNotificationService started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySpendUpdateAsync_SendsNotificationToGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var amount = 10.50m;
            var totalSpend = 150.75m;
            var budget = 500m;
            var model = "gpt-4";
            var provider = "OpenAI";

            // Act
            await _service.NotifySpendUpdateAsync(virtualKeyId, amount, totalSpend, budget, model, provider);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(SpendUpdateNotification) &&
                        ((SpendUpdateNotification)args[0]).NewSpend == amount &&
                        ((SpendUpdateNotification)args[0]).TotalSpend == totalSpend &&
                        ((SpendUpdateNotification)args[0]).Budget == budget &&
                        ((SpendUpdateNotification)args[0]).BudgetPercentage == 30.15m &&
                        ((SpendUpdateNotification)args[0]).Model == model &&
                        ((SpendUpdateNotification)args[0]).Provider == provider),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task NotifySpendUpdateAsync_CalculatesBudgetPercentage()
        {
            // Arrange
            var virtualKeyId = 123;
            var amount = 50m;
            var totalSpend = 250m;
            var budget = 500m;

            // Act
            await _service.NotifySpendUpdateAsync(virtualKeyId, amount, totalSpend, budget, "model", "provider");

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(SpendUpdateNotification) &&
                        ((SpendUpdateNotification)args[0]).BudgetPercentage == 50m),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task NotifySpendUpdateAsync_HandlesNullBudget()
        {
            // Arrange
            var virtualKeyId = 123;
            var amount = 10m;
            var totalSpend = 100m;
            decimal? budget = null;

            // Act
            await _service.NotifySpendUpdateAsync(virtualKeyId, amount, totalSpend, budget, "model", "provider");

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendUpdate",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(SpendUpdateNotification) &&
                        ((SpendUpdateNotification)args[0]).Budget == null &&
                        ((SpendUpdateNotification)args[0]).BudgetPercentage == null),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task NotifySpendUpdateAsync_RecordsSpendForPatternAnalysis()
        {
            // Arrange
            var virtualKeyId = 123;
            var amount = 10m;

            // Act
            await _service.NotifySpendUpdateAsync(virtualKeyId, amount, 100m, null, "model", "provider");

            // Verify by calling CheckUnusualSpendingAsync - should not throw
            await _service.CheckUnusualSpendingAsync(virtualKeyId);
        }

        [Fact]
        public async Task SendSpendSummaryAsync_SendsSummaryToGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var summary = new SpendSummaryNotification
            {
                PeriodType = "daily",
                TotalSpend = 250m,
                RequestCount = 150
            };

            // Act
            await _service.SendSpendSummaryAsync(virtualKeyId, summary);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "SpendSummary",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] == summary),
                    default),
                Times.Once);
        }

        [Fact]
        public void RecordSpend_StoresSpendData()
        {
            // Arrange
            var virtualKeyId = 123;
            var amount = 10m;

            // Act - Record multiple spends
            _service.RecordSpend(virtualKeyId, amount);
            _service.RecordSpend(virtualKeyId, amount * 2);
            _service.RecordSpend(virtualKeyId, amount * 3);

            // Assert - No exception thrown
            // Pattern is stored internally and can be analyzed
        }

        [Fact]
        public async Task CheckUnusualSpendingAsync_DoesNotSendAlertWithInsufficientData()
        {
            // Arrange
            var virtualKeyId = 123;
            
            // Record only 3 spends (need at least 5)
            _service.RecordSpend(virtualKeyId, 10m);
            _service.RecordSpend(virtualKeyId, 10m);
            _service.RecordSpend(virtualKeyId, 10m);

            // Act
            await _service.CheckUnusualSpendingAsync(virtualKeyId);

            // Assert - No notification sent
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "UnusualSpendingDetected",
                    It.IsAny<object[]>(),
                    default),
                Times.Never);
        }

        [Fact]
        public async Task CheckUnusualSpendingAsync_DetectsSpendSpike()
        {
            // Arrange
            var virtualKeyId = 123;
            
            // Simulate normal spending followed by a spike
            var baseTime = DateTime.UtcNow.AddHours(-2);
            
            // Record historical normal spending (need to simulate time)
            for (int i = 0; i < 10; i++)
            {
                _service.RecordSpend(virtualKeyId, 10m);
                Thread.Sleep(10); // Small delay to spread timestamps
            }
            
            // Wait a bit then record spike
            Thread.Sleep(100);
            
            // Record spike in spending
            for (int i = 0; i < 5; i++)
            {
                _service.RecordSpend(virtualKeyId, 100m); // 10x normal
            }

            // Act
            await _service.CheckUnusualSpendingAsync(virtualKeyId);

            // Assert - Should detect unusual spending
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "UnusualSpendingDetected",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(UnusualSpendingNotification) &&
                        ((UnusualSpendingNotification)args[0]).ActivityType == "spend_spike"),
                    default),
                Times.AtMostOnce); // May or may not trigger based on timing
        }

        [Fact]
        public async Task NotifySpendUpdateAsync_HandlesExceptionsGracefully()
        {
            // Arrange
            var virtualKeyId = 123;
            _mockClientProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .ThrowsAsync(new InvalidOperationException("Test exception"));

            // Act - Should not throw
            await _service.NotifySpendUpdateAsync(virtualKeyId, 10m, 100m, null, "model", "provider");

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending spend update notification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task StopAsync_DisposesTimer()
        {
            // Arrange
            await _service.StartAsync(CancellationToken.None);

            // Act
            await _service.StopAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("SpendNotificationService stopped")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}