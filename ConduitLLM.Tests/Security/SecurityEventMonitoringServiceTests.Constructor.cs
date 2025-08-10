using System;
using Moq;
using Xunit;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Constructor tests for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
    {
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
    }
}