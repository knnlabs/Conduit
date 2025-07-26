using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Http.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Http.Tests.Services
{
    /// <summary>
    /// Unit tests for the UsageAnalyticsNotificationService class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Notifications")]
    public class UsageAnalyticsNotificationServiceTests
    {
        private readonly Mock<IHubContext<UsageAnalyticsHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<ILogger<UsageAnalyticsNotificationService>> _mockLogger;
        private readonly UsageAnalyticsNotificationService _service;
        private readonly ITestOutputHelper _output;

        public UsageAnalyticsNotificationServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockHubContext = new Mock<IHubContext<UsageAnalyticsHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockLogger = new Mock<ILogger<UsageAnalyticsNotificationService>>();

            // Setup hub context mocks
            _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            
            // Setup default SendCoreAsync to return completed task
            _mockClientProxy.Setup(x => x.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _service = new UsageAnalyticsNotificationService(
                _mockHubContext.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullHubContext_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new UsageAnalyticsNotificationService(null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new UsageAnalyticsNotificationService(_mockHubContext.Object, null!));
        }

        #endregion

        #region SendUsageMetricsAsync Tests

        [Fact]
        public async Task SendUsageMetricsAsync_WithNormalUsage_ShouldSendToKeyGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var metrics = new UsageMetricsNotification
            {
                RequestsPerMinute = 50,
                TokensPerMinute = 5000,
                UniqueModelsUsed = 2,
                MostUsedModel = "gpt-4",
                RequestsByModel = new Dictionary<string, int>
                {
                    { "gpt-4", 30 },
                    { "gpt-3.5-turbo", 20 }
                }
            };

            // Act
            await _service.SendUsageMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-usage-123"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "UsageMetrics", 
                It.Is<object[]>(args => args[0] == metrics),
                default), Times.Once);
            
            // Should not send to global group for normal usage
            _mockClients.Verify(x => x.Group("analytics-global-usage"), Times.Never);
        }

        [Fact]
        public async Task SendUsageMetricsAsync_WithHighRequestRate_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var metrics = new UsageMetricsNotification
            {
                RequestsPerMinute = 150, // > 100
                TokensPerMinute = 5000,
                UniqueModelsUsed = 1,
                MostUsedModel = "gpt-4",
                RequestsByModel = new Dictionary<string, int>
                {
                    { "gpt-4", 150 }
                }
            };

            // Act
            await _service.SendUsageMetricsAsync(virtualKeyId, metrics);

            // Assert
            // Should send to both key-specific and global groups
            _mockClients.Verify(x => x.Group("analytics-usage-123"), Times.Once);
            _mockClients.Verify(x => x.Group("analytics-global-usage"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                It.IsAny<string>(), 
                It.IsAny<object[]>(),
                default), Times.Exactly(2));
        }

        [Fact]
        public async Task SendUsageMetricsAsync_WithHighTokenRate_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 123;
            var metrics = new UsageMetricsNotification
            {
                RequestsPerMinute = 50,
                TokensPerMinute = 15000, // > 10000
                UniqueModelsUsed = 1,
                MostUsedModel = "gpt-4",
                RequestsByModel = new Dictionary<string, int>
                {
                    { "gpt-4", 50 }
                }
            };

            // Act
            await _service.SendUsageMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-global-usage"), Times.Once);
        }

        [Fact]
        public async Task SendUsageMetricsAsync_WithException_ShouldLogError()
        {
            // Arrange
            var virtualKeyId = 123;
            var metrics = new UsageMetricsNotification();
            
            _mockClientProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .ThrowsAsync(new Exception("SignalR error"));

            // Act
            await _service.SendUsageMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to send usage metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        #endregion

        #region SendCostAnalyticsAsync Tests

        [Fact]
        public async Task SendCostAnalyticsAsync_WithNormalCost_ShouldSendToKeyGroup()
        {
            // Arrange
            var virtualKeyId = 456;
            var analytics = new CostAnalyticsNotification
            {
                TotalCost = 100.50m,
                CostPerHour = 5.25m,
                CostByModel = new System.Collections.Generic.Dictionary<string, decimal>
                {
                    { "gpt-4", 80.0m },
                    { "gpt-3.5-turbo", 20.5m }
                }
            };

            // Act
            await _service.SendCostAnalyticsAsync(virtualKeyId, analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-cost-456"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "CostAnalytics", 
                It.Is<object[]>(args => args[0] == analytics),
                default), Times.Once);
            
            // Should not send to global group for normal cost
            _mockClients.Verify(x => x.Group("analytics-global-cost"), Times.Never);
        }

        [Fact]
        public async Task SendCostAnalyticsAsync_WithHighCostRate_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 456;
            var analytics = new CostAnalyticsNotification
            {
                TotalCost = 500.0m,
                CostPerHour = 15.0m, // > 10.0
                CostByModel = new System.Collections.Generic.Dictionary<string, decimal>
                {
                    { "gpt-4", 500.0m }
                }
            };

            // Act
            await _service.SendCostAnalyticsAsync(virtualKeyId, analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-cost-456"), Times.Once);
            _mockClients.Verify(x => x.Group("analytics-global-cost"), Times.Once);
        }

        #endregion

        #region SendPerformanceMetricsAsync Tests

        [Fact]
        public async Task SendPerformanceMetricsAsync_WithGoodPerformance_ShouldSendToKeyGroup()
        {
            // Arrange
            var virtualKeyId = 789;
            var metrics = new PerformanceMetricsNotification
            {
                ModelName = "gpt-4",
                ProviderType = ProviderType.OpenAI,
                AverageLatencyMs = 1500,
                P95LatencyMs = 2500,
                P99LatencyMs = 3500,
                ErrorRate = 0.01,
                SuccessRate = 0.99,
                SampleSize = 100
            };

            // Act
            await _service.SendPerformanceMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-performance-789"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "PerformanceMetrics", 
                It.Is<object[]>(args => args[0] == metrics),
                default), Times.Once);
            
            // Should not send to global group for good performance
            _mockClients.Verify(x => x.Group("analytics-global-performance"), Times.Never);
        }

        [Fact]
        public async Task SendPerformanceMetricsAsync_WithHighLatency_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 789;
            var metrics = new PerformanceMetricsNotification
            {
                ModelName = "gpt-4",
                AverageLatencyMs = 6000, // > 5000
                P95LatencyMs = 8000,
                P99LatencyMs = 10000,
                ErrorRate = 0.02
            };

            // Act
            await _service.SendPerformanceMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-performance-789"), Times.Once);
            _mockClients.Verify(x => x.Group("analytics-global-performance"), Times.Once);
        }

        [Fact]
        public async Task SendPerformanceMetricsAsync_WithHighErrorRate_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 789;
            var metrics = new PerformanceMetricsNotification
            {
                ModelName = "gpt-4",
                AverageLatencyMs = 2000,
                ErrorRate = 0.08, // > 0.05
                SuccessRate = 0.92,
                SampleSize = 100
            };

            // Act
            await _service.SendPerformanceMetricsAsync(virtualKeyId, metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-global-performance"), Times.Once);
        }

        #endregion

        #region SendErrorAnalyticsAsync Tests

        [Fact]
        public async Task SendErrorAnalyticsAsync_WithLowErrors_ShouldSendToKeyGroup()
        {
            // Arrange
            var virtualKeyId = 999;
            var analytics = new ErrorAnalyticsNotification
            {
                TotalErrors = 5,
                ErrorRate = 0.02,
                ErrorsByType = new System.Collections.Generic.Dictionary<string, int>
                {
                    { "rate_limit", 3 },
                    { "timeout", 2 }
                },
                ErrorsByModel = new System.Collections.Generic.Dictionary<string, int>
                {
                    { "gpt-4", 5 }
                }
            };

            // Act
            await _service.SendErrorAnalyticsAsync(virtualKeyId, analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-errors-999"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "ErrorAnalytics", 
                It.Is<object[]>(args => args[0] == analytics),
                default), Times.Once);
            
            // Should not send to global group for low errors
            _mockClients.Verify(x => x.Group("analytics-global-errors"), Times.Never);
        }

        [Fact]
        public async Task SendErrorAnalyticsAsync_WithHighErrorRate_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 999;
            var analytics = new ErrorAnalyticsNotification
            {
                TotalErrors = 50,
                ErrorRate = 0.15, // > 0.1
                ErrorsByType = new System.Collections.Generic.Dictionary<string, int>
                {
                    { "server_error", 50 }
                }
            };

            // Act
            await _service.SendErrorAnalyticsAsync(virtualKeyId, analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-errors-999"), Times.Once);
            _mockClients.Verify(x => x.Group("analytics-global-errors"), Times.Once);
        }

        [Fact]
        public async Task SendErrorAnalyticsAsync_WithHighErrorCount_ShouldSendToGlobalGroup()
        {
            // Arrange
            var virtualKeyId = 999;
            var analytics = new ErrorAnalyticsNotification
            {
                TotalErrors = 150, // > 100
                ErrorRate = 0.05,
                ErrorsByType = new System.Collections.Generic.Dictionary<string, int>
                {
                    { "rate_limit", 150 }
                }
            };

            // Act
            await _service.SendErrorAnalyticsAsync(virtualKeyId, analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-global-errors"), Times.Once);
        }

        #endregion

        #region Global Metrics Tests

        [Fact]
        public async Task SendGlobalUsageMetricsAsync_ShouldSendToGlobalGroup()
        {
            // Arrange
            var metrics = new UsageMetricsNotification
            {
                RequestsPerMinute = 1000,
                TokensPerMinute = 100000,
                UniqueModelsUsed = 3,
                MostUsedModel = "gpt-4",
                RequestsByModel = new Dictionary<string, int>
                {
                    { "gpt-4", 500 },
                    { "gpt-3.5-turbo", 300 },
                    { "claude-3", 200 }
                }
            };

            // Act
            await _service.SendGlobalUsageMetricsAsync(metrics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-global-usage"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "GlobalUsageMetrics", 
                It.Is<object[]>(args => args.Length == 1),
                default), Times.Once);
        }

        [Fact]
        public async Task SendGlobalCostAnalyticsAsync_ShouldSendToGlobalGroup()
        {
            // Arrange
            var analytics = new CostAnalyticsNotification
            {
                TotalCost = 10000.0m,
                CostPerHour = 100.0m,
                CostByModel = new System.Collections.Generic.Dictionary<string, decimal>
                {
                    { "gpt-4", 7000.0m },
                    { "gpt-3.5-turbo", 2000.0m },
                    { "claude-3", 1000.0m }
                }
            };

            // Act
            await _service.SendGlobalCostAnalyticsAsync(analytics);

            // Assert
            _mockClients.Verify(x => x.Group("analytics-global-cost"), Times.Once);
            _mockClientProxy.Verify(x => x.SendCoreAsync(
                "GlobalCostAnalytics", 
                It.Is<object[]>(args => args.Length == 1),
                default), Times.Once);
        }

        [Fact]
        public async Task SendGlobalUsageMetricsAsync_WithException_ShouldLogError()
        {
            // Arrange
            var metrics = new UsageMetricsNotification();
            
            _mockClientProxy.Setup(x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), default))
                .ThrowsAsync(new Exception("SignalR error"));

            // Act
            await _service.SendGlobalUsageMetricsAsync(metrics);

            // Assert
            _mockLogger.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed to send global usage metrics")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        #endregion
    }
}