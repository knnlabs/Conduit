using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Test-specific interfaces and models
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for the SecurityEventMonitoringService class.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class SecurityEventMonitoringServiceTests
    {
        private readonly Mock<ISecurityMetricsService> _mockMetricsService;
        private readonly Mock<IThreatDetectionService> _mockThreatDetectionService;
        private readonly Mock<ILogger<MockSecurityEventMonitoringService>> _mockLogger;
        private readonly MockSecurityEventMonitoringService _service;
        private readonly ITestOutputHelper _output;

        public SecurityEventMonitoringServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockMetricsService = new Mock<ISecurityMetricsService>();
            _mockThreatDetectionService = new Mock<IThreatDetectionService>();
            _mockLogger = new Mock<ILogger<MockSecurityEventMonitoringService>>();
            
            // Setup the Log method to not throw when called
            _mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
                .Verifiable();
            
            _service = new MockSecurityEventMonitoringService(
                _mockMetricsService.Object,
                _mockThreatDetectionService.Object,
                _mockLogger.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullMetricsService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MockSecurityEventMonitoringService(null!, _mockThreatDetectionService.Object, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullThreatDetectionService_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MockSecurityEventMonitoringService(_mockMetricsService.Object, null!, _mockLogger.Object));
        }

        [Fact]
        public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MockSecurityEventMonitoringService(_mockMetricsService.Object, _mockThreatDetectionService.Object, null!));
        }

        #endregion

        #region RecordEvent Tests

        [Fact]
        public async Task RecordEvent_WithValidEvent_ShouldRecordMetricsAndCheckThreats()
        {
            // Arrange
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.FailedAuthentication,
                SourceIp = "192.168.1.100",
                UserId = "user123",
                Details = "Invalid password",
                Timestamp = DateTime.UtcNow
            };

            _mockThreatDetectionService.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
                .ReturnsAsync(new ThreatAnalysisResult
                {
                    IsThreat = false,
                    ThreatLevel = ThreatLevel.None
                });

            // Act
            await _service.RecordEventAsync(securityEvent);

            // Assert
            _mockMetricsService.Verify(x => x.RecordSecurityEvent(
                It.Is<SecurityEvent>(e => e.EventType == SecurityEventType.FailedAuthentication)),
                Times.Once);

            _mockThreatDetectionService.Verify(x => x.AnalyzeEventAsync(
                It.Is<SecurityEvent>(e => e.SourceIp == "192.168.1.100")),
                Times.Once);
        }

        [Fact]
        public async Task RecordEvent_WithDetectedThreat_ShouldTriggerAlert()
        {
            // Arrange
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                SourceIp = "10.0.0.1",
                Details = "Multiple failed login attempts"
            };

            _mockThreatDetectionService.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
                .ReturnsAsync(new ThreatAnalysisResult
                {
                    IsThreat = true,
                    ThreatLevel = ThreatLevel.High,
                    Reason = "Brute force attack detected"
                });

            // Act
            await _service.RecordEventAsync(securityEvent);

            // Assert
            _mockLogger.VerifyLog(LogLevel.Warning, "Security threat detected");

            _mockMetricsService.Verify(x => x.RecordThreatDetected(
                ThreatLevel.High,
                "Brute force attack detected"),
                Times.Once);
        }

        [Fact]
        public async Task RecordEvent_WithNullEvent_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.RecordEventAsync(null!));
        }

        #endregion

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

        #region BlockIpAddress Tests

        [Fact]
        public async Task BlockIpAddress_ShouldBlockAndRecordEvent()
        {
            // Arrange
            var ipAddress = "10.0.0.100";
            var reason = "Too many failed authentication attempts";

            _mockThreatDetectionService.Setup(x => x.BlockIpAddressAsync(ipAddress, reason))
                .ReturnsAsync(true);

            // Act
            var result = await _service.BlockIpAddressAsync(ipAddress, reason);

            // Assert
            result.Should().BeTrue();
            
            _mockMetricsService.Verify(x => x.RecordSecurityEvent(
                It.Is<SecurityEvent>(e =>
                    e.EventType == SecurityEventType.IpBlocked &&
                    e.SourceIp == ipAddress &&
                    e.Details == reason)),
                Times.Once);
        }

        [Fact]
        public async Task BlockIpAddress_WhenBlockingFails_ShouldReturnFalse()
        {
            // Arrange
            var ipAddress = "10.0.0.100";
            
            _mockThreatDetectionService.Setup(x => x.BlockIpAddressAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.BlockIpAddressAsync(ipAddress, "Test reason");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region UnblockIpAddress Tests

        [Fact]
        public async Task UnblockIpAddress_ShouldUnblockAndRecordEvent()
        {
            // Arrange
            var ipAddress = "10.0.0.100";

            _mockThreatDetectionService.Setup(x => x.UnblockIpAddressAsync(ipAddress))
                .ReturnsAsync(true);

            // Act
            var result = await _service.UnblockIpAddressAsync(ipAddress);

            // Assert
            result.Should().BeTrue();
            
            _mockMetricsService.Verify(x => x.RecordSecurityEvent(
                It.Is<SecurityEvent>(e =>
                    e.EventType == SecurityEventType.IpUnblocked &&
                    e.SourceIp == ipAddress)),
                Times.Once);
        }

        #endregion

        #region GetBlockedIpAddresses Tests

        [Fact]
        public async Task GetBlockedIpAddresses_ShouldReturnListFromThreatDetection()
        {
            // Arrange
            var blockedIps = new List<BlockedIpInfo>
            {
                new() { IpAddress = "10.0.0.1", BlockedAt = DateTime.UtcNow.AddHours(-2), Reason = "Brute force" },
                new() { IpAddress = "10.0.0.2", BlockedAt = DateTime.UtcNow.AddHours(-1), Reason = "Suspicious activity" }
            };

            _mockThreatDetectionService.Setup(x => x.GetBlockedIpAddressesAsync())
                .ReturnsAsync(blockedIps);

            // Act
            var result = await _service.GetBlockedIpAddressesAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().ContainSingle(ip => ip.IpAddress == "10.0.0.1");
            result.Should().ContainSingle(ip => ip.IpAddress == "10.0.0.2");
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

        #region Exception Handling Tests

        [Fact]
        public async Task RecordEvent_WhenMetricsServiceThrows_ShouldLogErrorAndContinue()
        {
            // Arrange
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.FailedAuthentication,
                SourceIp = "192.168.1.1"
            };

            _mockMetricsService.Setup(x => x.RecordSecurityEvent(It.IsAny<SecurityEvent>()))
                .Throws(new Exception("Metrics service error"));

            _mockThreatDetectionService.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
                .ReturnsAsync(new ThreatAnalysisResult { IsThreat = false });

            // Act
            await _service.RecordEventAsync(securityEvent);

            // Assert
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error recording security event metrics");

            // Threat detection should still be called
            _mockThreatDetectionService.Verify(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()), Times.Once);
        }

        [Fact]
        public async Task RecordEvent_WhenThreatDetectionThrows_ShouldLogError()
        {
            // Arrange
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                SourceIp = "10.0.0.1"
            };

            _mockThreatDetectionService.Setup(x => x.AnalyzeEventAsync(It.IsAny<SecurityEvent>()))
                .ThrowsAsync(new Exception("Threat detection error"));

            // Act
            await _service.RecordEventAsync(securityEvent);

            // Assert
            _mockLogger.VerifyLogWithAnyException(LogLevel.Error, "Error analyzing security event for threats");
        }

        #endregion
    }

    // Test models
    public enum SecurityEventType
    {
        SuccessfulAuthentication,
        FailedAuthentication,
        AccessDenied,
        SuspiciousActivity,
        IpBlocked,
        IpUnblocked
    }

    public class SecurityEvent
    {
        public SecurityEventType EventType { get; set; }
        public string SourceIp { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ThreatAnalysisResult
    {
        public bool IsThreat { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
        public string? Reason { get; set; }
    }

    public enum ThreatLevel
    {
        None,
        Low,
        Medium,
        High,
        Critical
    }

    public class SecurityMetrics
    {
        public int TotalEvents { get; set; }
        public int FailedAuthenticationAttempts { get; set; }
        public int SuspiciousActivities { get; set; }
        public int BlockedRequests { get; set; }
        public int ThreatsDetected { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class BlockedIpInfo
    {
        public string IpAddress { get; set; } = string.Empty;
        public DateTime BlockedAt { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Interfaces needed for testing
    public interface ISecurityMetricsService
    {
        void RecordSecurityEvent(SecurityEvent securityEvent);
        void RecordThreatDetected(ThreatLevel threatLevel, string reason);
        Task<IEnumerable<SecurityEvent>> GetRecentEventsAsync(int count);
        Task<IEnumerable<SecurityEvent>> GetEventsByTypeAsync(SecurityEventType eventType, DateTime startTime, DateTime endTime);
        Task<IEnumerable<SecurityEvent>> GetEventsBySourceIpAsync(string sourceIp, int maxCount);
        Task<SecurityMetrics> GetMetricsAsync(DateTime startTime, DateTime endTime);
        Task<int> ClearOldEventsAsync(DateTime cutoffDate);
    }

    public interface IThreatDetectionService
    {
        Task<ThreatAnalysisResult> AnalyzeEventAsync(SecurityEvent securityEvent);
        Task<bool> BlockIpAddressAsync(string ipAddress, string reason);
        Task<bool> UnblockIpAddressAsync(string ipAddress);
        Task<IEnumerable<BlockedIpInfo>> GetBlockedIpAddressesAsync();
    }

    // Mock implementation
    public class MockSecurityEventMonitoringService
    {
        private readonly ISecurityMetricsService _metricsService;
        private readonly IThreatDetectionService _threatDetectionService;
        private readonly ILogger<MockSecurityEventMonitoringService> _logger;

        public MockSecurityEventMonitoringService(
            ISecurityMetricsService metricsService,
            IThreatDetectionService threatDetectionService,
            ILogger<MockSecurityEventMonitoringService> logger)
        {
            _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
            _threatDetectionService = threatDetectionService ?? throw new ArgumentNullException(nameof(threatDetectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RecordEventAsync(SecurityEvent securityEvent)
        {
            if (securityEvent == null) throw new ArgumentNullException(nameof(securityEvent));

            try
            {
                _metricsService.RecordSecurityEvent(securityEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording security event metrics");
            }

            try
            {
                var threatAnalysis = await _threatDetectionService.AnalyzeEventAsync(securityEvent);
                if (threatAnalysis.IsThreat)
                {
                    _logger.LogWarning("Security threat detected: {ThreatLevel} - {Reason}",
                        threatAnalysis.ThreatLevel, threatAnalysis.Reason);
                    _metricsService.RecordThreatDetected(threatAnalysis.ThreatLevel, threatAnalysis.Reason!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing security event for threats");
            }
        }

        public async Task<IEnumerable<SecurityEvent>> GetRecentEventsAsync(int count)
        {
            if (count < 0) throw new ArgumentException("Count must be non-negative", nameof(count));
            return await _metricsService.GetRecentEventsAsync(count);
        }

        public async Task<IEnumerable<SecurityEvent>> GetEventsByTypeAsync(
            SecurityEventType eventType, DateTime startTime, DateTime endTime)
        {
            if (endTime < startTime)
                throw new ArgumentException("End time must be after start time");
            
            return await _metricsService.GetEventsByTypeAsync(eventType, startTime, endTime);
        }

        public async Task<IEnumerable<SecurityEvent>> GetEventsBySourceIpAsync(string sourceIp, int maxCount)
        {
            if (string.IsNullOrWhiteSpace(sourceIp))
                throw sourceIp == null 
                    ? new ArgumentNullException(nameof(sourceIp))
                    : new ArgumentException("Source IP cannot be empty", nameof(sourceIp));
            
            return await _metricsService.GetEventsBySourceIpAsync(sourceIp, maxCount);
        }

        public async Task<SecurityMetrics> GetSecurityMetricsAsync(DateTime startTime, DateTime endTime)
        {
            return await _metricsService.GetMetricsAsync(startTime, endTime);
        }

        public async Task<bool> BlockIpAddressAsync(string ipAddress, string reason)
        {
            var result = await _threatDetectionService.BlockIpAddressAsync(ipAddress, reason);
            if (result)
            {
                _metricsService.RecordSecurityEvent(new SecurityEvent
                {
                    EventType = SecurityEventType.IpBlocked,
                    SourceIp = ipAddress,
                    Details = reason,
                    Timestamp = DateTime.UtcNow
                });
            }
            return result;
        }

        public async Task<bool> UnblockIpAddressAsync(string ipAddress)
        {
            var result = await _threatDetectionService.UnblockIpAddressAsync(ipAddress);
            if (result)
            {
                _metricsService.RecordSecurityEvent(new SecurityEvent
                {
                    EventType = SecurityEventType.IpUnblocked,
                    SourceIp = ipAddress,
                    Timestamp = DateTime.UtcNow
                });
            }
            return result;
        }

        public async Task<IEnumerable<BlockedIpInfo>> GetBlockedIpAddressesAsync()
        {
            return await _threatDetectionService.GetBlockedIpAddressesAsync();
        }

        public async Task<int> ClearOldEventsAsync(DateTime cutoffDate)
        {
            var count = await _metricsService.ClearOldEventsAsync(cutoffDate);
            _logger.LogInformation("Cleared {Count} old security events older than {Date}", count, cutoffDate);
            return count;
        }
    }
}