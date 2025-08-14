using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.Tests.TestHelpers;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Exception handling tests for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
    {
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
}