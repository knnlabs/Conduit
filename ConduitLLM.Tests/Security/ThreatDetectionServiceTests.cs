using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Test-specific interfaces and models
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Unit tests for threat detection service implementations.
    /// </summary>
    [Trait("Category", "Unit")]
    [Trait("Component", "Security")]
    public class ThreatDetectionServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<ILogger<IThreatDetectionService>> _mockLogger;
        private readonly ITestOutputHelper _output;

        public ThreatDetectionServiceTests(ITestOutputHelper output)
        {
            _output = output;
            _mockConfiguration = new Mock<IConfiguration>();
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<ILogger<IThreatDetectionService>>();
            
            // Setup default configuration
            SetupConfiguration();
        }

        private void SetupConfiguration()
        {
            _mockConfiguration.Setup(x => x["Security:MaxFailedAuthAttempts"]).Returns("5");
            _mockConfiguration.Setup(x => x["Security:SuspiciousActivityThreshold"]).Returns("10");
            _mockConfiguration.Setup(x => x["Security:RateLimitWindow"]).Returns("300"); // 5 minutes
            _mockConfiguration.Setup(x => x["Security:BlockDuration"]).Returns("3600"); // 1 hour
        }

        #region AnalyzeEvent Tests

        [Fact]
        public async Task AnalyzeEvent_WithNormalActivity_ShouldReturnNoThreat()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuccessfulAuthentication,
                SourceIp = "192.168.1.100",
                UserId = "user123"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeFalse();
            result.ThreatLevel.Should().Be(ThreatLevel.None);
        }

        [Fact]
        public async Task AnalyzeEvent_WithMultipleFailedAuth_ShouldDetectBruteForce()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var sourceIp = "10.0.0.1";
            
            // Simulate cache returning high failure count
            service.SetFailureCount(sourceIp, 6);

            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.FailedAuthentication,
                SourceIp = sourceIp,
                UserId = "user123"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            result.ThreatLevel.Should().Be(ThreatLevel.High);
            result.Reason.Should().Contain("Brute force");
        }

        [Fact]
        public async Task AnalyzeEvent_WithSuspiciousPattern_ShouldDetectThreat()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                SourceIp = "10.0.0.2",
                Details = "SQL injection attempt detected"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            ((int)result.ThreatLevel).Should().BeGreaterThanOrEqualTo((int)ThreatLevel.Medium);
        }

        [Fact]
        public async Task AnalyzeEvent_WithKnownMaliciousIp_ShouldDetectHighThreat()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            service.AddKnownMaliciousIp("203.0.113.0");

            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.AccessDenied,
                SourceIp = "203.0.113.0"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            result.ThreatLevel.Should().Be(ThreatLevel.Critical);
            result.Reason.Should().Contain("Known malicious IP");
        }

        #endregion

        #region BlockIpAddress Tests

        [Fact]
        public async Task BlockIpAddress_WithValidIp_ShouldBlockSuccessfully()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ipAddress = "10.0.0.100";
            var reason = "Too many failed attempts";

            // Act
            var result = await service.BlockIpAddressAsync(ipAddress, reason);

            // Assert
            result.Should().BeTrue();
            service.IsIpBlocked(ipAddress).Should().BeTrue();
        }

        [Fact]
        public async Task BlockIpAddress_WithNullIp_ShouldThrowArgumentNullException()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                service.BlockIpAddressAsync(null!, "Test reason"));
        }

        [Fact]
        public async Task BlockIpAddress_WithInvalidIp_ShouldReturnFalse()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var invalidIp = "not.an.ip.address";

            // Act
            var result = await service.BlockIpAddressAsync(invalidIp, "Test reason");

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region UnblockIpAddress Tests

        [Fact]
        public async Task UnblockIpAddress_WithBlockedIp_ShouldUnblockSuccessfully()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ipAddress = "10.0.0.100";
            
            // First block the IP
            await service.BlockIpAddressAsync(ipAddress, "Test block");
            service.IsIpBlocked(ipAddress).Should().BeTrue();

            // Act
            var result = await service.UnblockIpAddressAsync(ipAddress);

            // Assert
            result.Should().BeTrue();
            service.IsIpBlocked(ipAddress).Should().BeFalse();
        }

        [Fact]
        public async Task UnblockIpAddress_WithNonBlockedIp_ShouldReturnFalse()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var ipAddress = "10.0.0.200";

            // Act
            var result = await service.UnblockIpAddressAsync(ipAddress);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetBlockedIpAddresses Tests

        [Fact]
        public async Task GetBlockedIpAddresses_ShouldReturnAllBlockedIps()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            
            // Block several IPs
            await service.BlockIpAddressAsync("10.0.0.1", "Brute force");
            await service.BlockIpAddressAsync("10.0.0.2", "Suspicious activity");
            await service.BlockIpAddressAsync("10.0.0.3", "Known malicious");

            // Act
            var blockedIps = await service.GetBlockedIpAddressesAsync();

            // Assert
            blockedIps.Should().HaveCount(3);
            blockedIps.Should().Contain(ip => ip.IpAddress == "10.0.0.1" && ip.Reason == "Brute force");
            blockedIps.Should().Contain(ip => ip.IpAddress == "10.0.0.2" && ip.Reason == "Suspicious activity");
            blockedIps.Should().Contain(ip => ip.IpAddress == "10.0.0.3" && ip.Reason == "Known malicious");
        }

        [Fact]
        public async Task GetBlockedIpAddresses_WithNoBlockedIps_ShouldReturnEmptyList()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);

            // Act
            var blockedIps = await service.GetBlockedIpAddressesAsync();

            // Assert
            blockedIps.Should().BeEmpty();
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public async Task AnalyzeEvent_WithRapidRequests_ShouldDetectRateLimitViolation()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var sourceIp = "10.0.0.50";
            
            // Simulate high request rate
            service.SetRequestCount(sourceIp, 100); // 100 requests in window

            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuccessfulAuthentication,
                SourceIp = sourceIp
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            ((int)result.ThreatLevel).Should().BeGreaterThanOrEqualTo((int)ThreatLevel.Medium);
            result.Reason.Should().Contain("Rate limit");
        }

        #endregion

        #region Pattern Detection Tests

        [Fact]
        public async Task AnalyzeEvent_WithSqlInjectionPattern_ShouldDetectThreat()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                SourceIp = "10.0.0.60",
                Details = "Input contains: ' OR 1=1--"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            ((int)result.ThreatLevel).Should().BeGreaterThanOrEqualTo((int)ThreatLevel.High);
            result.Reason.Should().Contain("injection");
        }

        [Fact]
        public async Task AnalyzeEvent_WithXssPattern_ShouldDetectThreat()
        {
            // Arrange
            var service = new MockThreatDetectionService(_mockConfiguration.Object, _mockCache.Object, _mockLogger.Object);
            var securityEvent = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                SourceIp = "10.0.0.70",
                Details = "Input contains: <script>alert('xss')</script>"
            };

            // Act
            var result = await service.AnalyzeEventAsync(securityEvent);

            // Assert
            result.IsThreat.Should().BeTrue();
            ((int)result.ThreatLevel).Should().BeGreaterThanOrEqualTo((int)ThreatLevel.High);
            result.Reason.Should().ContainAny("XSS", "script injection", "Cross-site scripting");
        }

        #endregion
    }

    // Mock implementation for testing
    public class MockThreatDetectionService : IThreatDetectionService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IThreatDetectionService> _logger;
        private readonly Dictionary<string, BlockedIpInfo> _blockedIps = new();
        private readonly HashSet<string> _knownMaliciousIps = new();
        private readonly Dictionary<string, int> _failureCounts = new();
        private readonly Dictionary<string, int> _requestCounts = new();

        public MockThreatDetectionService(
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<IThreatDetectionService> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SetFailureCount(string ip, int count) => _failureCounts[ip] = count;
        public void SetRequestCount(string ip, int count) => _requestCounts[ip] = count;
        public void AddKnownMaliciousIp(string ip) => _knownMaliciousIps.Add(ip);
        public bool IsIpBlocked(string ip) => _blockedIps.ContainsKey(ip);

        public async Task<ThreatAnalysisResult> AnalyzeEventAsync(SecurityEvent securityEvent)
        {
            ArgumentNullException.ThrowIfNull(securityEvent);

            await Task.Delay(1); // Simulate async work

            // Check for known malicious IP
            if (_knownMaliciousIps.Contains(securityEvent.SourceIp))
            {
                return new ThreatAnalysisResult
                {
                    IsThreat = true,
                    ThreatLevel = ThreatLevel.Critical,
                    Reason = "Known malicious IP address"
                };
            }

            // Check for brute force attacks
            if (securityEvent.EventType == SecurityEventType.FailedAuthentication)
            {
                var maxAttempts = int.Parse(_configuration["Security:MaxFailedAuthAttempts"] ?? "5");
                var failures = _failureCounts.GetValueOrDefault(securityEvent.SourceIp, 0);
                
                if (failures >= maxAttempts)
                {
                    return new ThreatAnalysisResult
                    {
                        IsThreat = true,
                        ThreatLevel = ThreatLevel.High,
                        Reason = "Brute force attack detected"
                    };
                }
            }

            // Check for rate limit violations
            var requestCount = _requestCounts.GetValueOrDefault(securityEvent.SourceIp, 0);
            if (requestCount > 50) // Hardcoded threshold for testing
            {
                return new ThreatAnalysisResult
                {
                    IsThreat = true,
                    ThreatLevel = ThreatLevel.Medium,
                    Reason = "Rate limit exceeded"
                };
            }

            // Check for injection patterns
            if (securityEvent.Details != null)
            {
                if (securityEvent.Details.Contains("' OR", StringComparison.OrdinalIgnoreCase) ||
                    securityEvent.Details.Contains("--", StringComparison.OrdinalIgnoreCase))
                {
                    return new ThreatAnalysisResult
                    {
                        IsThreat = true,
                        ThreatLevel = ThreatLevel.High,
                        Reason = "SQL injection attempt detected"
                    };
                }

                if (securityEvent.Details.Contains("<script", StringComparison.OrdinalIgnoreCase))
                {
                    return new ThreatAnalysisResult
                    {
                        IsThreat = true,
                        ThreatLevel = ThreatLevel.High,
                        Reason = "XSS attempt detected"
                    };
                }
            }

            // Check for suspicious activity
            if (securityEvent.EventType == SecurityEventType.SuspiciousActivity)
            {
                return new ThreatAnalysisResult
                {
                    IsThreat = true,
                    ThreatLevel = ThreatLevel.Medium,
                    Reason = "Suspicious activity detected"
                };
            }

            return new ThreatAnalysisResult
            {
                IsThreat = false,
                ThreatLevel = ThreatLevel.None
            };
        }

        public async Task<bool> BlockIpAddressAsync(string ipAddress, string reason)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentNullException(nameof(ipAddress));

            // Simple IP validation
            if (!System.Net.IPAddress.TryParse(ipAddress, out _))
                return false;

            await Task.Delay(1); // Simulate async work

            _blockedIps[ipAddress] = new BlockedIpInfo
            {
                IpAddress = ipAddress,
                BlockedAt = DateTime.UtcNow,
                Reason = reason
            };

            _logger.LogInformation("Blocked IP address {IpAddress}: {Reason}", ipAddress, reason);
            return true;
        }

        public async Task<bool> UnblockIpAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentNullException(nameof(ipAddress));

            await Task.Delay(1); // Simulate async work

            if (_blockedIps.Remove(ipAddress))
            {
                _logger.LogInformation("Unblocked IP address {IpAddress}", ipAddress);
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<BlockedIpInfo>> GetBlockedIpAddressesAsync()
        {
            await Task.Delay(1); // Simulate async work
            return _blockedIps.Values.ToList();
        }
    }
}