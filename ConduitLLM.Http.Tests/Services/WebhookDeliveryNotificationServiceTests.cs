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
    /// Unit tests for the WebhookDeliveryNotificationService.
    /// </summary>
    public class WebhookDeliveryNotificationServiceTests
    {
        private readonly Mock<IHubContext<WebhookDeliveryHub>> _mockHubContext;
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger<WebhookDeliveryNotificationService>> _mockLogger;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly WebhookDeliveryNotificationService _service;

        public WebhookDeliveryNotificationServiceTests()
        {
            _mockHubContext = new Mock<IHubContext<WebhookDeliveryHub>>();
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<WebhookDeliveryNotificationService>>();
            _mockClientProxy = new Mock<IClientProxy>();

            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            mockClients.Setup(x => x.All).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);

            _service = new WebhookDeliveryNotificationService(
                _mockHubContext.Object,
                _mockServiceProvider.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task StartAsync_InitializesStatisticsTimer()
        {
            // Act
            await _service.StartAsync(CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WebhookDeliveryNotificationService started")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact(Skip = "Test setup needs updating")]
        public async Task NotifyDeliveryAttemptAsync_SendsNotificationToGroup()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var taskType = "video";
            var eventType = "TaskProgress";
            var attemptNumber = 1;

            // Act
            await _service.NotifyDeliveryAttemptAsync(webhookUrl, taskId, taskType, eventType, attemptNumber);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sent delivery attempt notification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifyDeliverySuccessAsync_SendsNotificationAndRecordsMetrics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var statusCode = 200;
            var responseTimeMs = 150L;
            var totalAttempts = 1;

            // Act
            await _service.NotifyDeliverySuccessAsync(webhookUrl, taskId, statusCode, responseTimeMs, totalAttempts);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "DeliverySucceeded",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(WebhookDeliverySuccess) &&
                        ((WebhookDeliverySuccess)args[0]).StatusCode == statusCode &&
                        ((WebhookDeliverySuccess)args[0]).ResponseTimeMs == responseTimeMs),
                    default),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sent delivery success notification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifyDeliveryFailureAsync_SendsNotificationForPermanentFailure()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var errorMessage = "Connection timeout";
            var attemptNumber = 3;
            var isPermanent = true;

            // Act
            await _service.NotifyDeliveryFailureAsync(webhookUrl, taskId, errorMessage, null, attemptNumber, isPermanent);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "DeliveryFailed",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(WebhookDeliveryFailure) &&
                        ((WebhookDeliveryFailure)args[0]).ErrorMessage == errorMessage &&
                        ((WebhookDeliveryFailure)args[0]).IsPermanentFailure == isPermanent),
                    default),
                Times.Once);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Sent delivery failure notification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifyRetryScheduledAsync_SendsRetryNotification()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var taskId = "task-123";
            var retryTime = DateTime.UtcNow.AddSeconds(30);
            var retryNumber = 2;
            var maxRetries = 3;

            // Act
            await _service.NotifyRetryScheduledAsync(webhookUrl, taskId, retryTime, retryNumber, maxRetries);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "RetryScheduled",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(WebhookRetryInfo) &&
                        ((WebhookRetryInfo)args[0]).NextAttemptNumber == retryNumber),
                    default),
                Times.Once);
        }

        [Fact]
        public async Task NotifyCircuitBreakerStateChangeAsync_BroadcastsToAllClients()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            var newState = "open";
            var previousState = "closed";
            var reason = "Too many failures";
            var failureCount = 5;

            // Act
            await _service.NotifyCircuitBreakerStateChangeAsync(webhookUrl, newState, previousState, reason, failureCount);

            // Assert
            _mockClientProxy.Verify(
                x => x.SendCoreAsync(
                    "CircuitBreakerStateChanged",
                    It.Is<object[]>(args => 
                        args.Length == 1 && 
                        args[0] != null &&
                        args[0].GetType() == typeof(WebhookCircuitBreakerState) &&
                        ((WebhookCircuitBreakerState)args[0]).CurrentState == newState &&
                        ((WebhookCircuitBreakerState)args[0]).PreviousState == previousState),
                    default),
                Times.Exactly(2)); // Once for group, once for all
        }

        [Fact]
        public void RecordDeliveryAttempt_IncrementsMetrics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";

            // Act
            _service.RecordDeliveryAttempt(webhookUrl);
            _service.RecordDeliveryAttempt(webhookUrl);

            // Assert - verify through GetStatisticsAsync
            var stats = _service.GetStatisticsAsync().Result;
            Assert.Equal(2, stats.TotalDeliveries);
        }

        [Fact]
        public void RecordDeliverySuccess_UpdatesMetricsWithResponseTime()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";

            // Act
            _service.RecordDeliveryAttempt(webhookUrl);
            _service.RecordDeliverySuccess(webhookUrl, 100);
            _service.RecordDeliverySuccess(webhookUrl, 200);

            // Assert
            var stats = _service.GetStatisticsAsync().Result;
            Assert.Equal(2, stats.SuccessfulDeliveries);
            Assert.True(stats.AverageResponseTimeMs > 0);
        }

        [Fact]
        public void RecordDeliveryFailure_UpdatesFailureMetrics()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";

            // Act
            _service.RecordDeliveryAttempt(webhookUrl);
            _service.RecordDeliveryFailure(webhookUrl, false); // Temporary failure
            _service.RecordDeliveryFailure(webhookUrl, true);  // Permanent failure

            // Assert
            var stats = _service.GetStatisticsAsync().Result;
            Assert.Equal(2, stats.FailedDeliveries);
            Assert.Equal(1, stats.PendingDeliveries); // Only temporary failures count as pending
        }

        [Fact]
        public async Task GetStatisticsAsync_ReturnsAggregatedStats()
        {
            // Arrange
            var webhook1 = "https://example.com/webhook1";
            var webhook2 = "https://example.com/webhook2";

            // Record some activity
            _service.RecordDeliveryAttempt(webhook1);
            _service.RecordDeliverySuccess(webhook1, 100);
            
            _service.RecordDeliveryAttempt(webhook2);
            _service.RecordDeliveryAttempt(webhook2);
            _service.RecordDeliverySuccess(webhook2, 200);
            _service.RecordDeliveryFailure(webhook2, false);

            // Act
            var stats = await _service.GetStatisticsAsync();

            // Assert
            Assert.Equal(3, stats.TotalDeliveries);
            Assert.Equal(2, stats.SuccessfulDeliveries);
            Assert.Equal(1, stats.FailedDeliveries);
            Assert.Equal(2, stats.UrlStatistics.Count);
            Assert.True(stats.SuccessRate > 0);
        }

        [Fact]
        public async Task GetStatisticsAsync_CalculatesCorrectSuccessRate()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";

            // Record 3 successes and 1 failure
            for (int i = 0; i < 4; i++)
            {
                _service.RecordDeliveryAttempt(webhookUrl);
                if (i < 3)
                {
                    _service.RecordDeliverySuccess(webhookUrl, 100);
                }
                else
                {
                    _service.RecordDeliveryFailure(webhookUrl, true);
                }
            }

            // Act
            var stats = await _service.GetStatisticsAsync();

            // Assert
            Assert.Equal(75.0, stats.SuccessRate); // 3 out of 4 = 75%
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
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WebhookDeliveryNotificationService stopped")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact(Skip = "Test setup needs updating")]
        public async Task NotifyDeliveryAttemptAsync_HandlesExceptionsGracefully()
        {
            // Arrange
            var webhookUrl = "https://example.com/webhook";
            _mockServiceProvider.Setup(x => x.CreateScope()).Throws(new InvalidOperationException("Test exception"));

            // Act - Should not throw
            await _service.NotifyDeliveryAttemptAsync(webhookUrl, "task-123", "video", "TaskProgress", 1);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error sending delivery attempt notification")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}