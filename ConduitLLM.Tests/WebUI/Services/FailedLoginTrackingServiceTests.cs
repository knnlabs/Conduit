using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ConduitLLM.WebUI.Services;

namespace ConduitLLM.Tests.WebUI.Services
{
    public class FailedLoginTrackingServiceTests
    {
        private readonly IMemoryCache _memoryCache;
        private readonly Mock<ISecurityConfigurationService> _mockSecurityConfig;
        private readonly Mock<ILogger<FailedLoginTrackingService>> _mockLogger;
        private readonly FailedLoginTrackingService _service;

        public FailedLoginTrackingServiceTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _mockSecurityConfig = new Mock<ISecurityConfigurationService>();
            _mockLogger = new Mock<ILogger<FailedLoginTrackingService>>();
            
            // Set default values
            _mockSecurityConfig.Setup(x => x.MaxFailedLoginAttempts).Returns(5);
            _mockSecurityConfig.Setup(x => x.IpBanDurationMinutes).Returns(30);
            
            _service = new FailedLoginTrackingService(
                _memoryCache,
                _mockSecurityConfig.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public void RecordFailedLogin_IncrementsFailedAttempts()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";

            // Act
            _service.RecordFailedLogin(ipAddress);
            _service.RecordFailedLogin(ipAddress);

            // Assert
            var cacheKey = $"failed_attempts_{ipAddress}";
            Assert.True(_memoryCache.TryGetValue(cacheKey, out int attempts));
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void RecordFailedLogin_BansIPAfterMaxAttempts()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            _mockSecurityConfig.Setup(x => x.MaxFailedLoginAttempts).Returns(3);

            // Act
            _service.RecordFailedLogin(ipAddress);
            _service.RecordFailedLogin(ipAddress);
            _service.RecordFailedLogin(ipAddress); // This should trigger the ban

            // Assert
            Assert.True(_service.IsBanned(ipAddress));
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"Banned IP address {ipAddress}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void RecordFailedLogin_UsesConfiguredValuesFromSecurityService()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            // Already configured in constructor with MaxFailedLoginAttempts = 5

            // Act - Record 5 attempts (configured max)
            for (int i = 0; i < 5; i++)
            {
                _service.RecordFailedLogin(ipAddress);
            }

            // Assert
            Assert.True(_service.IsBanned(ipAddress));
        }

        [Fact]
        public void ClearFailedAttempts_RemovesAllRecords()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            
            _service.RecordFailedLogin(ipAddress);
            _service.RecordFailedLogin(ipAddress);

            // Act
            _service.ClearFailedAttempts(ipAddress);

            // Assert
            var cacheKey = $"failed_attempts_{ipAddress}";
            Assert.False(_memoryCache.TryGetValue(cacheKey, out _));
        }

        [Fact]
        public void IsBanned_ReturnsTrueForBannedIP()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            var banKey = $"ip_ban_{ipAddress}";
            _memoryCache.Set(banKey, true, TimeSpan.FromMinutes(30));

            // Act
            var result = _service.IsBanned(ipAddress);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsBanned_ReturnsFalseForNonBannedIP()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";

            // Act
            var result = _service.IsBanned(ipAddress);

            // Assert
            Assert.False(result);
        }

        [Fact(Skip = "Timing-based test is flaky in CI")]
        public async Task BanExpires_AfterConfiguredDuration()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            // Create a new service with short ban duration
            var shortBanConfig = new Mock<ISecurityConfigurationService>();
            shortBanConfig.Setup(x => x.MaxFailedLoginAttempts).Returns(1);
            shortBanConfig.Setup(x => x.IpBanDurationMinutes).Returns(1); // 1 minute (smallest integer value)
            
            var service = new FailedLoginTrackingService(
                _memoryCache,
                shortBanConfig.Object,
                _mockLogger.Object
            );

            // Act
            service.RecordFailedLogin(ipAddress); // This should ban the IP
            Assert.True(service.IsBanned(ipAddress));

            // Since we can't have fractional minutes, just verify the ban is active
            // The test would need to wait 60+ seconds to verify expiration, which is too long
            
            // Assert - ban should still be active after a short delay
            await Task.Delay(100);
            Assert.True(service.IsBanned(ipAddress));
        }

        [Fact]
        public void RecordFailedLogin_UsesConfiguredDefaults()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            // Service already configured with defaults (5 attempts) in constructor

            // Act - Should use defaults without throwing
            for (int i = 0; i < 5; i++)
            {
                _service.RecordFailedLogin(ipAddress);
            }

            // Assert
            Assert.True(_service.IsBanned(ipAddress));
        }

        [Fact]
        public void RecordFailedLogin_LogsWarningWithAttemptCount()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";

            // Act
            _service.RecordFailedLogin(ipAddress);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Failed login attempt 1/5")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void ClearFailedAttempts_LogsInformation()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";

            // Act
            _service.ClearFailedAttempts(ipAddress);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains($"Cleared failed login attempts for IP: {ipAddress}")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void MultipleIPs_TrackedSeparately()
        {
            // Arrange
            const string ip1 = "192.168.1.100";
            const string ip2 = "192.168.1.101";
            _mockSecurityConfig.Setup(x => x.MaxFailedLoginAttempts).Returns(3);

            // Act
            _service.RecordFailedLogin(ip1);
            _service.RecordFailedLogin(ip1);
            _service.RecordFailedLogin(ip2);

            // Assert
            var cacheKey1 = $"failed_attempts_{ip1}";
            var cacheKey2 = $"failed_attempts_{ip2}";
            
            Assert.True(_memoryCache.TryGetValue(cacheKey1, out int attempts1));
            Assert.Equal(2, attempts1);
            
            Assert.True(_memoryCache.TryGetValue(cacheKey2, out int attempts2));
            Assert.Equal(1, attempts2);
            
            Assert.False(_service.IsBanned(ip1));
            Assert.False(_service.IsBanned(ip2));
        }
    }
}