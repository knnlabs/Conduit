using FluentAssertions;

using Moq;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// IP blocking and unblocking tests for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
    {
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
    }
}