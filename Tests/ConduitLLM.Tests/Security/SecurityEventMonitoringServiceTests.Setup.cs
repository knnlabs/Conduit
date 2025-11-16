using Microsoft.Extensions.Logging;

using Moq;

using Xunit.Abstractions;

namespace ConduitLLM.Tests.Security
{
    /// <summary>
    /// Setup, mocks, helper methods and test models for SecurityEventMonitoringService tests
    /// </summary>
    public partial class SecurityEventMonitoringServiceTests
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
            ArgumentNullException.ThrowIfNull(securityEvent);

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