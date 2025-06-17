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
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<FailedLoginTrackingService>> _mockLogger;
        private readonly FailedLoginTrackingService _service;

        public FailedLoginTrackingServiceTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<FailedLoginTrackingService>>();
            
            _service = new FailedLoginTrackingService(
                _memoryCache,
                _mockConfiguration.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public void RecordFailedLogin_IncrementsFailedAttempts()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("5");

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
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("3");
            _mockConfiguration.Setup(x => x["CONDUIT_IP_BAN_DURATION_MINUTES"]).Returns("30");

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
        public void RecordFailedLogin_UsesDefaultMaxAttemptsWhenNotConfigured()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns((string?)null);
            _mockConfiguration.Setup(x => x["CONDUIT_IP_BAN_DURATION_MINUTES"]).Returns((string?)null);

            // Act - Record 5 attempts (default max)
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
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("5");
            
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
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("1");
            _mockConfiguration.Setup(x => x["CONDUIT_IP_BAN_DURATION_MINUTES"]).Returns("0.0167"); // 1 second

            // Act
            _service.RecordFailedLogin(ipAddress); // This should ban the IP
            Assert.True(_service.IsBanned(ipAddress));

            // Wait for ban to expire (plus a buffer for timing variance)
            await Task.Delay(1500);

            // Assert
            Assert.False(_service.IsBanned(ipAddress));
        }

        [Fact]
        public void RecordFailedLogin_HandlesInvalidConfiguration()
        {
            // Arrange
            const string ipAddress = "192.168.1.100";
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("invalid");
            _mockConfiguration.Setup(x => x["CONDUIT_IP_BAN_DURATION_MINUTES"]).Returns("invalid");

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
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("5");

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
            _mockConfiguration.Setup(x => x["CONDUIT_MAX_FAILED_ATTEMPTS"]).Returns("3");

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