using Microsoft.Extensions.Logging;
using Moq;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// RecordEvent tests for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
    {
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
    }
}