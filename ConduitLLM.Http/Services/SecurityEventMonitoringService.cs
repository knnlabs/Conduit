using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.DTOs.HealthMonitoring;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for monitoring security events and generating alerts
    /// </summary>
    public interface ISecurityEventMonitoringService
    {
        void RecordAuthenticationFailure(string ipAddress, string virtualKey, string endpoint);
        void RecordAuthenticationSuccess(string ipAddress, string virtualKey, string endpoint);
        void RecordRateLimitViolation(string ipAddress, string virtualKey, string endpoint, string limitType);
        void RecordSuspiciousActivity(string ipAddress, string activity, string details);
        void RecordDataExfiltrationAttempt(string ipAddress, string virtualKey, long dataSize, string endpoint);
        void RecordAnomalousAccess(string ipAddress, string virtualKey, string anomaly, string details);
        void RecordIpBan(string ipAddress, string reason, int failedAttempts);
        Task<SecurityMetrics> GetSecurityMetricsAsync();
        Task<List<SecurityEvent>> GetRecentSecurityEventsAsync(int minutes = 60);
    }

    /// <summary>
    /// Implementation of security event monitoring service
    /// </summary>
    public class SecurityEventMonitoringService : ISecurityEventMonitoringService, IHostedService, IDisposable
    {
        private readonly IAlertManagementService _alertManagementService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SecurityEventMonitoringService> _logger;
        private readonly SecurityMonitoringOptions _options;

        // Event storage
        private readonly ConcurrentQueue<SecurityEvent> _recentEvents;
        private readonly ConcurrentDictionary<string, IpActivityProfile> _ipProfiles;
        private readonly ConcurrentDictionary<string, VirtualKeyActivityProfile> _keyProfiles;
        private readonly ConcurrentDictionary<string, AnomalyDetectionState> _anomalyStates;

        private Timer? _analysisTimer;
        private Timer? _cleanupTimer;
        private readonly SemaphoreSlim _analysisSemaphore;

        public SecurityEventMonitoringService(
            IAlertManagementService alertManagementService,
            IMemoryCache cache,
            ILogger<SecurityEventMonitoringService> logger,
            IOptions<SecurityMonitoringOptions> options)
        {
            _alertManagementService = alertManagementService;
            _cache = cache;
            _logger = logger;
            _options = options.Value;

            _recentEvents = new ConcurrentQueue<SecurityEvent>();
            _ipProfiles = new ConcurrentDictionary<string, IpActivityProfile>();
            _keyProfiles = new ConcurrentDictionary<string, VirtualKeyActivityProfile>();
            _anomalyStates = new ConcurrentDictionary<string, AnomalyDetectionState>();
            _analysisSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Record an authentication failure event
        /// </summary>
        public void RecordAuthenticationFailure(string ipAddress, string virtualKey, string endpoint)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationFailure,
                IpAddress = ipAddress,
                VirtualKey = virtualKey,
                Endpoint = endpoint,
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => profile.AuthenticationFailures++);
            
            if (!string.IsNullOrEmpty(virtualKey))
            {
                UpdateKeyProfile(virtualKey, profile => profile.AuthenticationFailures++);
            }

            TrimEventQueue();
        }

        /// <summary>
        /// Record an authentication success event
        /// </summary>
        public void RecordAuthenticationSuccess(string ipAddress, string virtualKey, string endpoint)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationSuccess,
                IpAddress = ipAddress,
                VirtualKey = virtualKey,
                Endpoint = endpoint,
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => 
            {
                profile.AuthenticationSuccesses++;
                profile.LastSuccessfulAuth = DateTime.UtcNow;
            });
            
            UpdateKeyProfile(virtualKey, profile => 
            {
                profile.AuthenticationSuccesses++;
                profile.LastActivity = DateTime.UtcNow;
            });

            TrimEventQueue();
        }

        /// <summary>
        /// Record a rate limit violation
        /// </summary>
        public void RecordRateLimitViolation(string ipAddress, string virtualKey, string endpoint, string limitType)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.RateLimitViolation,
                IpAddress = ipAddress,
                VirtualKey = virtualKey,
                Endpoint = endpoint,
                Details = $"Rate limit type: {limitType}",
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => profile.RateLimitViolations++);
            
            if (!string.IsNullOrEmpty(virtualKey))
            {
                UpdateKeyProfile(virtualKey, profile => profile.RateLimitViolations++);
            }

            TrimEventQueue();
        }

        /// <summary>
        /// Record suspicious activity
        /// </summary>
        public void RecordSuspiciousActivity(string ipAddress, string activity, string details)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                IpAddress = ipAddress,
                Activity = activity,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => profile.SuspiciousActivities++);

            TrimEventQueue();
        }

        /// <summary>
        /// Record potential data exfiltration attempt
        /// </summary>
        public void RecordDataExfiltrationAttempt(string ipAddress, string virtualKey, long dataSize, string endpoint)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.DataExfiltration,
                IpAddress = ipAddress,
                VirtualKey = virtualKey,
                Endpoint = endpoint,
                DataSize = dataSize,
                Details = $"Unusual data volume: {dataSize:N0} bytes",
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => profile.DataExfiltrationAttempts++);
            
            if (!string.IsNullOrEmpty(virtualKey))
            {
                UpdateKeyProfile(virtualKey, profile => profile.DataExfiltrationAttempts++);
            }

            TrimEventQueue();
        }

        /// <summary>
        /// Record anomalous access pattern
        /// </summary>
        public void RecordAnomalousAccess(string ipAddress, string virtualKey, string anomaly, string details)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.AnomalousAccess,
                IpAddress = ipAddress,
                VirtualKey = virtualKey,
                Activity = anomaly,
                Details = details,
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => profile.AnomalousActivities++);
            
            if (!string.IsNullOrEmpty(virtualKey))
            {
                UpdateKeyProfile(virtualKey, profile => profile.AnomalousActivities++);
            }

            TrimEventQueue();
        }

        /// <summary>
        /// Record IP ban event
        /// </summary>
        public void RecordIpBan(string ipAddress, string reason, int failedAttempts)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.IpBanned,
                IpAddress = ipAddress,
                Activity = "IP Banned",
                Details = $"Reason: {reason}, Failed attempts: {failedAttempts}",
                Timestamp = DateTime.UtcNow
            };

            _recentEvents.Enqueue(@event);
            UpdateIpProfile(ipAddress, profile => 
            {
                profile.IsBanned = true;
                profile.BannedAt = DateTime.UtcNow;
            });

            TrimEventQueue();
        }

        /// <summary>
        /// Get current security metrics
        /// </summary>
        public async Task<SecurityMetrics> GetSecurityMetricsAsync()
        {
            await _analysisSemaphore.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var recentWindow = now.AddMinutes(-_options.MetricsWindowMinutes);

                var recentEventsList = _recentEvents
                    .Where(e => e.Timestamp > recentWindow)
                    .ToList();

                var metrics = new SecurityMetrics
                {
                    TotalEvents = recentEventsList.Count,
                    AuthenticationFailures = recentEventsList.Count(e => e.EventType == SecurityEventType.AuthenticationFailure),
                    RateLimitViolations = recentEventsList.Count(e => e.EventType == SecurityEventType.RateLimitViolation),
                    SuspiciousActivities = recentEventsList.Count(e => e.EventType == SecurityEventType.SuspiciousActivity),
                    ActiveIpBans = _ipProfiles.Values.Count(p => p.IsBanned),
                    UniqueIpsMonitored = _ipProfiles.Count,
                    UniqueKeysMonitored = _keyProfiles.Count,
                    DataExfiltrationAttempts = recentEventsList.Count(e => e.EventType == SecurityEventType.DataExfiltration),
                    AnomalousAccessPatterns = recentEventsList.Count(e => e.EventType == SecurityEventType.AnomalousAccess)
                };

                // Calculate threat level
                metrics.ThreatLevel = CalculateThreatLevel(metrics);

                return metrics;
            }
            finally
            {
                _analysisSemaphore.Release();
            }
        }

        /// <summary>
        /// Get recent security events
        /// </summary>
        public Task<List<SecurityEvent>> GetRecentSecurityEventsAsync(int minutes = 60)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            var events = _recentEvents
                .Where(e => e.Timestamp > cutoff)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            return Task.FromResult(events);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Security event monitoring service started");

            // Start analysis timer
            _analysisTimer = new Timer(
                async _ => await AnalyzeSecurityEventsAsync(),
                null,
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds),
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds));

            // Start cleanup timer
            _cleanupTimer = new Timer(
                _ => CleanupOldData(),
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Security event monitoring service stopping");

            _analysisTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private async Task AnalyzeSecurityEventsAsync()
        {
            try
            {
                var metrics = await GetSecurityMetricsAsync();

                // Check for brute force attacks
                await DetectBruteForceAttacksAsync();

                // Check for distributed attacks
                await DetectDistributedAttacksAsync();

                // Check for anomalous patterns
                await DetectAnomalousPatterns();

                // Check for data exfiltration
                await DetectDataExfiltration();

                // Update threat level and trigger alerts if needed
                if (metrics.ThreatLevel >= ThreatLevel.High)
                {
                    await TriggerThreatLevelAlert(metrics);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing security events");
            }
        }

        private async Task DetectBruteForceAttacksAsync()
        {
            var window = DateTime.UtcNow.AddMinutes(-_options.BruteForceDetectionWindowMinutes);
            
            foreach (var ipProfile in _ipProfiles.Values)
            {
                var recentFailures = _recentEvents
                    .Where(e => e.IpAddress == ipProfile.IpAddress && 
                               e.EventType == SecurityEventType.AuthenticationFailure &&
                               e.Timestamp > window)
                    .Count();

                if (recentFailures >= _options.BruteForceThreshold)
                {
                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Critical,
                        Type = AlertType.SecurityEvent,
                        Component = "Authentication",
                        Title = "Brute Force Attack Detected",
                        Message = $"IP {ipProfile.IpAddress} has {recentFailures} failed authentication attempts in {_options.BruteForceDetectionWindowMinutes} minutes",
                        Context = new Dictionary<string, object>
                        {
                            ["ipAddress"] = ipProfile.IpAddress,
                            ["failedAttempts"] = recentFailures,
                            ["windowMinutes"] = _options.BruteForceDetectionWindowMinutes
                        },
                        SuggestedActions = new List<string>
                        {
                            "Review authentication logs",
                            "Consider blocking the IP address",
                            "Check for compromised credentials",
                            "Enable additional authentication requirements"
                        }
                    });
                }
            }
        }

        private async Task DetectDistributedAttacksAsync()
        {
            var window = DateTime.UtcNow.AddMinutes(-_options.DistributedAttackWindowMinutes);
            
            // Check for multiple IPs targeting the same key
            var keyTargets = _recentEvents
                .Where(e => e.EventType == SecurityEventType.AuthenticationFailure && 
                           e.Timestamp > window &&
                           !string.IsNullOrEmpty(e.VirtualKey))
                .GroupBy(e => e.VirtualKey)
                .Where(g => g.Select(e => e.IpAddress).Distinct().Count() >= _options.DistributedAttackIpThreshold)
                .ToList();

            foreach (var target in keyTargets)
            {
                var uniqueIps = target.Select(e => e.IpAddress).Distinct().Count();
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.SecurityEvent,
                    Component = "Authentication",
                    Title = "Distributed Attack Detected",
                    Message = $"Virtual key {target.Key.Substring(0, 10)}... targeted from {uniqueIps} different IPs",
                    Context = new Dictionary<string, object>
                    {
                        ["virtualKey"] = target.Key.Substring(0, 10) + "...",
                        ["uniqueIps"] = uniqueIps,
                        ["totalAttempts"] = target.Count()
                    },
                    SuggestedActions = new List<string>
                    {
                        "Temporarily disable the virtual key",
                        "Review all access from involved IPs",
                        "Check for credential compromise",
                        "Consider implementing geo-blocking"
                    }
                });
            }
        }

        private async Task DetectAnomalousPatterns()
        {
            // Detect unusual access patterns (e.g., accessing many different models rapidly)
            var window = DateTime.UtcNow.AddMinutes(-_options.AnomalyDetectionWindowMinutes);
            
            foreach (var keyProfile in _keyProfiles.Values)
            {
                var recentEvents = _recentEvents
                    .Where(e => e.VirtualKey == keyProfile.VirtualKey && 
                               e.Timestamp > window &&
                               e.EventType == SecurityEventType.AuthenticationSuccess)
                    .ToList();

                if (recentEvents.Count < 10) continue; // Need minimum data

                // Check for rapid endpoint switching
                var uniqueEndpoints = recentEvents.Select(e => e.Endpoint).Distinct().Count();
                var eventRate = recentEvents.Count / _options.AnomalyDetectionWindowMinutes;

                if (uniqueEndpoints > _options.EndpointDiversityThreshold && 
                    eventRate > _options.RapidAccessThreshold)
                {
                    RecordAnomalousAccess(
                        recentEvents.First().IpAddress,
                        keyProfile.VirtualKey,
                        "Rapid endpoint switching",
                        $"Accessed {uniqueEndpoints} different endpoints at {eventRate:F1} req/min"
                    );

                    await _alertManagementService.TriggerAlertAsync(new HealthAlert
                    {
                        Severity = AlertSeverity.Warning,
                        Type = AlertType.SecurityEvent,
                        Component = "Access Pattern",
                        Title = "Anomalous Access Pattern Detected",
                        Message = $"Virtual key accessing {uniqueEndpoints} different endpoints rapidly",
                        Context = new Dictionary<string, object>
                        {
                            ["virtualKey"] = keyProfile.VirtualKey.Substring(0, 10) + "...",
                            ["uniqueEndpoints"] = uniqueEndpoints,
                            ["requestRate"] = eventRate
                        }
                    });
                }
            }
        }

        private async Task DetectDataExfiltration()
        {
            // This would typically integrate with request/response size monitoring
            // For now, we'll check for patterns in recorded exfiltration attempts
            var window = DateTime.UtcNow.AddMinutes(-_options.DataExfiltrationWindowMinutes);
            
            var exfiltrationAttempts = _recentEvents
                .Where(e => e.EventType == SecurityEventType.DataExfiltration && e.Timestamp > window)
                .GroupBy(e => e.VirtualKey ?? e.IpAddress)
                .Where(g => g.Sum(e => e.DataSize ?? 0) > _options.DataExfiltrationThresholdBytes)
                .ToList();

            foreach (var attempt in exfiltrationAttempts)
            {
                var totalData = attempt.Sum(e => e.DataSize ?? 0);
                await _alertManagementService.TriggerAlertAsync(new HealthAlert
                {
                    Severity = AlertSeverity.Critical,
                    Type = AlertType.SecurityEvent,
                    Component = "Data Security",
                    Title = "Potential Data Exfiltration Detected",
                    Message = $"Unusual data volume ({totalData:N0} bytes) transferred",
                    Context = new Dictionary<string, object>
                    {
                        ["identifier"] = attempt.Key.Substring(0, Math.Min(10, attempt.Key.Length)) + "...",
                        ["totalBytes"] = totalData,
                        ["requestCount"] = attempt.Count()
                    },
                    SuggestedActions = new List<string>
                    {
                        "Review data transfer logs",
                        "Check for unauthorized data access",
                        "Consider rate limiting large responses",
                        "Investigate the virtual key usage"
                    }
                });
            }
        }

        private async Task TriggerThreatLevelAlert(SecurityMetrics metrics)
        {
            await _alertManagementService.TriggerAlertAsync(new HealthAlert
            {
                Severity = metrics.ThreatLevel == ThreatLevel.Critical ? AlertSeverity.Critical : AlertSeverity.Error,
                Type = AlertType.SecurityEvent,
                Component = "Security Monitoring",
                Title = $"Elevated Security Threat Level: {metrics.ThreatLevel}",
                Message = $"Multiple security indicators suggest {metrics.ThreatLevel} threat level",
                Context = new Dictionary<string, object>
                {
                    ["authFailures"] = metrics.AuthenticationFailures,
                    ["rateLimitViolations"] = metrics.RateLimitViolations,
                    ["suspiciousActivities"] = metrics.SuspiciousActivities,
                    ["activeBans"] = metrics.ActiveIpBans
                },
                SuggestedActions = new List<string>
                {
                    "Review security event logs",
                    "Check for coordinated attacks",
                    "Consider enabling stricter security policies",
                    "Alert security team for manual review"
                }
            });
        }

        private ThreatLevel CalculateThreatLevel(SecurityMetrics metrics)
        {
            var score = 0;

            // Weight different factors
            score += metrics.AuthenticationFailures * 2;
            score += metrics.RateLimitViolations;
            score += metrics.SuspiciousActivities * 3;
            score += metrics.DataExfiltrationAttempts * 5;
            score += metrics.AnomalousAccessPatterns * 2;
            score += metrics.ActiveIpBans * 2;

            if (score >= _options.ThreatLevelCriticalThreshold)
                return ThreatLevel.Critical;
            if (score >= _options.ThreatLevelHighThreshold)
                return ThreatLevel.High;
            if (score >= _options.ThreatLevelMediumThreshold)
                return ThreatLevel.Medium;
            if (score >= _options.ThreatLevelLowThreshold)
                return ThreatLevel.Low;
            
            return ThreatLevel.None;
        }

        private void UpdateIpProfile(string ipAddress, Action<IpActivityProfile> update)
        {
            _ipProfiles.AddOrUpdate(ipAddress,
                ip => 
                {
                    var profile = new IpActivityProfile { IpAddress = ip };
                    update(profile);
                    return profile;
                },
                (ip, existing) =>
                {
                    update(existing);
                    existing.LastActivity = DateTime.UtcNow;
                    return existing;
                });
        }

        private void UpdateKeyProfile(string virtualKey, Action<VirtualKeyActivityProfile> update)
        {
            if (string.IsNullOrEmpty(virtualKey)) return;

            _keyProfiles.AddOrUpdate(virtualKey,
                key => 
                {
                    var profile = new VirtualKeyActivityProfile { VirtualKey = key };
                    update(profile);
                    return profile;
                },
                (key, existing) =>
                {
                    update(existing);
                    return existing;
                });
        }

        private void TrimEventQueue()
        {
            while (_recentEvents.Count > _options.MaxEventsRetention)
            {
                _recentEvents.TryDequeue(out _);
            }
        }

        private void CleanupOldData()
        {
            var cutoff = DateTime.UtcNow.AddHours(-_options.DataRetentionHours);

            // Clean old events
            while (_recentEvents.TryPeek(out var oldestEvent) && oldestEvent.Timestamp < cutoff)
            {
                _recentEvents.TryDequeue(out _);
            }

            // Clean inactive IP profiles
            var inactiveIps = _ipProfiles
                .Where(kvp => kvp.Value.LastActivity < cutoff && !kvp.Value.IsBanned)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var ip in inactiveIps)
            {
                _ipProfiles.TryRemove(ip, out _);
            }

            // Clean inactive key profiles
            var inactiveKeys = _keyProfiles
                .Where(kvp => kvp.Value.LastActivity < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in inactiveKeys)
            {
                _keyProfiles.TryRemove(key, out _);
            }

            _logger.LogDebug("Cleaned up {IpCount} inactive IPs and {KeyCount} inactive keys", 
                inactiveIps.Count, inactiveKeys.Count);
        }

        public void Dispose()
        {
            _analysisTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _analysisSemaphore?.Dispose();
        }
    }

    // Supporting classes
    public class SecurityEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public SecurityEventType EventType { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? VirtualKey { get; set; }
        public string? Endpoint { get; set; }
        public string? Activity { get; set; }
        public string? Details { get; set; }
        public long? DataSize { get; set; }
    }

    public enum SecurityEventType
    {
        AuthenticationFailure,
        AuthenticationSuccess,
        RateLimitViolation,
        SuspiciousActivity,
        DataExfiltration,
        AnomalousAccess,
        IpBanned
    }

    public class SecurityMetrics
    {
        public int TotalEvents { get; set; }
        public int AuthenticationFailures { get; set; }
        public int RateLimitViolations { get; set; }
        public int SuspiciousActivities { get; set; }
        public int ActiveIpBans { get; set; }
        public int UniqueIpsMonitored { get; set; }
        public int UniqueKeysMonitored { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int AnomalousAccessPatterns { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
    }

    public enum ThreatLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }

    public class IpActivityProfile
    {
        public string IpAddress { get; set; } = string.Empty;
        public int AuthenticationFailures { get; set; }
        public int AuthenticationSuccesses { get; set; }
        public int RateLimitViolations { get; set; }
        public int SuspiciousActivities { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int AnomalousActivities { get; set; }
        public bool IsBanned { get; set; }
        public DateTime? BannedAt { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public DateTime? LastSuccessfulAuth { get; set; }
    }

    public class VirtualKeyActivityProfile
    {
        public string VirtualKey { get; set; } = string.Empty;
        public int AuthenticationFailures { get; set; }
        public int AuthenticationSuccesses { get; set; }
        public int RateLimitViolations { get; set; }
        public int DataExfiltrationAttempts { get; set; }
        public int AnomalousActivities { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    }

    public class AnomalyDetectionState
    {
        public string Identifier { get; set; } = string.Empty;
        public Dictionary<string, int> EndpointAccess { get; set; } = new();
        public DateTime WindowStart { get; set; }
        public int TotalRequests { get; set; }
    }

    public class SecurityMonitoringOptions
    {
        // Event retention
        public int MaxEventsRetention { get; set; } = 100000;
        public int DataRetentionHours { get; set; } = 24;

        // Analysis intervals
        public int AnalysisIntervalSeconds { get; set; } = 60;
        public int MetricsWindowMinutes { get; set; } = 60;

        // Brute force detection
        public int BruteForceThreshold { get; set; } = 10;
        public int BruteForceDetectionWindowMinutes { get; set; } = 10;

        // Distributed attack detection
        public int DistributedAttackIpThreshold { get; set; } = 5;
        public int DistributedAttackWindowMinutes { get; set; } = 30;

        // Anomaly detection
        public int AnomalyDetectionWindowMinutes { get; set; } = 15;
        public int EndpointDiversityThreshold { get; set; } = 20;
        public double RapidAccessThreshold { get; set; } = 10; // requests per minute

        // Data exfiltration detection
        public int DataExfiltrationWindowMinutes { get; set; } = 60;
        public long DataExfiltrationThresholdBytes { get; set; } = 1_073_741_824; // 1GB

        // Threat level thresholds
        public int ThreatLevelLowThreshold { get; set; } = 10;
        public int ThreatLevelMediumThreshold { get; set; } = 25;
        public int ThreatLevelHighThreshold { get; set; } = 50;
        public int ThreatLevelCriticalThreshold { get; set; } = 100;
    }
}