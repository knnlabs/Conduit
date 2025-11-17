using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Query and retrieval tests for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
    {
        #region GetRecentEvents Tests

        [Fact]
        public async Task GetRecentEvents_ShouldReturnEventsFromMetricsService()
        {
            // Arrange
            var recentEvents = new List<SecurityEvent>
            {
                new() { EventType = SecurityEventType.SuccessfulAuthentication, SourceIp = "192.168.1.1" },
                new() { EventType = SecurityEventType.AccessDenied, SourceIp = "192.168.1.2" }
            };

            _mockMetricsService.Setup(x => x.GetRecentEventsAsync(It.IsAny<int>()))
                .ReturnsAsync(recentEvents);

            // Act
            var result = await _service.GetRecentEventsAsync(10);

            // Assert
            result.Should().HaveCount(2);
            result.Should().ContainSingle(e => e.EventType == SecurityEventType.SuccessfulAuthentication);
            result.Should().ContainSingle(e => e.EventType == SecurityEventType.AccessDenied);
        }

        [Fact]
        public async Task GetRecentEvents_WithNegativeCount_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GetRecentEventsAsync(-1));
        }

        #endregion

        #region GetEventsByType Tests

        [Fact]
        public async Task GetEventsByType_ShouldReturnFilteredEvents()
        {
            // Arrange
            var startTime = DateTime.UtcNow.AddHours(-1);
            var endTime = DateTime.UtcNow;
            
            var events = new List<SecurityEvent>
            {
                new() { EventType = SecurityEventType.FailedAuthentication, SourceIp = "192.168.1.1" },
                new() { EventType = SecurityEventType.FailedAuthentication, SourceIp = "192.168.1.2" }
            };

            _mockMetricsService.Setup(x => x.GetEventsByTypeAsync(
                    SecurityEventType.FailedAuthentication,
                    It.IsAny<DateTime>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(events);

            // Act
            var result = await _service.GetEventsByTypeAsync(
                SecurityEventType.FailedAuthentication,
                startTime,
                endTime);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(e => e.EventType == SecurityEventType.FailedAuthentication);
        }

        [Fact]
        public async Task GetEventsByType_WithInvalidDateRange_ShouldThrowArgumentException()
        {
            // Arrange
            var startTime = DateTime.UtcNow;
            var endTime = DateTime.UtcNow.AddHours(-1); // End before start

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GetEventsByTypeAsync(
                    SecurityEventType.AccessDenied,
                    startTime,
                    endTime));
        }

        #endregion

        #region GetEventsBySourceIp Tests

        [Fact]
        public async Task GetEventsBySourceIp_ShouldReturnEventsFromSpecificIp()
        {
            // Arrange
            var sourceIp = "192.168.1.100";
            var events = new List<SecurityEvent>
            {
                new() { EventType = SecurityEventType.FailedAuthentication, SourceIp = sourceIp },
                new() { EventType = SecurityEventType.SuspiciousActivity, SourceIp = sourceIp }
            };

            _mockMetricsService.Setup(x => x.GetEventsBySourceIpAsync(sourceIp, It.IsAny<int>()))
                .ReturnsAsync(events);

            // Act
            var result = await _service.GetEventsBySourceIpAsync(sourceIp, 50);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(e => e.SourceIp == sourceIp);
        }

        [Fact]
        public async Task GetEventsBySourceIp_WithNullIp_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.GetEventsBySourceIpAsync(null!, 10));
        }

        [Fact]
        public async Task GetEventsBySourceIp_WithEmptyIp_ShouldThrowArgumentException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.GetEventsBySourceIpAsync(string.Empty, 10));
        }

        #endregion

        #region GetSecurityMetrics Tests

        [Fact]
        public async Task GetSecurityMetrics_ShouldReturnAggregatedMetrics()
        {
            // Arrange
            var metrics = new SecurityMetrics
            {
                TotalEvents = 1000,
                FailedAuthenticationAttempts = 50,
                SuspiciousActivities = 10,
                BlockedRequests = 25,
                ThreatsDetected = 5,
                LastUpdated = DateTime.UtcNow
            };

            _mockMetricsService.Setup(x => x.GetMetricsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(metrics);

            // Act
            var result = await _service.GetSecurityMetricsAsync(
                DateTime.UtcNow.AddDays(-1),
                DateTime.UtcNow);

            // Assert
            result.TotalEvents.Should().Be(1000);
            result.FailedAuthenticationAttempts.Should().Be(50);
            result.ThreatsDetected.Should().Be(5);
        }

        #endregion

        #region ClearOldEvents Tests

        [Fact]
        public async Task ClearOldEvents_ShouldCallMetricsService()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-30);

            _mockMetricsService.Setup(x => x.ClearOldEventsAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(150);

            // Act
            var result = await _service.ClearOldEventsAsync(cutoffDate);

            // Assert
            result.Should().Be(150);
            
            _mockLogger.VerifyLog(LogLevel.Information, "Cleared");
            _mockLogger.VerifyLog(LogLevel.Information, "150");
            _mockLogger.VerifyLog(LogLevel.Information, "old security events older than");
        }

        #endregion
    }
}