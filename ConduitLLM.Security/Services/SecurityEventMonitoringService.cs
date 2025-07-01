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
using ConduitLLM.Configuration.DTOs.Security;
using ConduitLLM.Security.Interfaces;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Implementation of security event monitoring service
    /// </summary>
    public class SecurityEventMonitoringService : ISecurityEventMonitoringService, IHostedService, IDisposable
    {
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
            IMemoryCache cache,
            ILogger<SecurityEventMonitoringService> logger,
            IOptions<SecurityMonitoringOptions> options)
        {
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

            if (!string.IsNullOrEmpty(virtualKey))
            {
                UpdateKeyProfile(virtualKey, profile => profile.AuthenticationSuccesses++);
            }

            TrimEventQueue();
        }

        /// <summary>
        /// Record a rate limit violation event
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
                Details = $"Data size: {dataSize} bytes",
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
        /// Record anomalous access patterns
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
        /// Record an IP ban event
        /// </summary>
        public void RecordIpBan(string ipAddress, string reason, int failedAttempts)
        {
            var @event = new SecurityEvent
            {
                EventType = SecurityEventType.IpBanned,
                IpAddress = ipAddress,
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
        public Task<SecurityMetricsDto> GetSecurityMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMinutes(-_options.MetricsWindowMinutes);
            
            var recentEvents = _recentEvents
                .Where(e => e.Timestamp >= windowStart)
                .ToList();

            var metrics = new SecurityMetricsDto
            {
                TotalEvents = recentEvents.Count,
                AuthenticationFailures = recentEvents.Count(e => e.EventType == SecurityEventType.AuthenticationFailure),
                RateLimitViolations = recentEvents.Count(e => e.EventType == SecurityEventType.RateLimitViolation),
                SuspiciousActivities = recentEvents.Count(e => e.EventType == SecurityEventType.SuspiciousActivity),
                DataExfiltrationAttempts = recentEvents.Count(e => e.EventType == SecurityEventType.DataExfiltration),
                AnomalousAccessPatterns = recentEvents.Count(e => e.EventType == SecurityEventType.AnomalousAccess),
                ActiveIpBans = _ipProfiles.Count(p => p.Value.IsBanned),
                UniqueIpsMonitored = _ipProfiles.Count,
                UniqueKeysMonitored = _keyProfiles.Count,
                ThreatLevel = CalculateThreatLevel(recentEvents),
                MetricsStartTime = windowStart,
                MetricsEndTime = now
            };

            // Add event type breakdown
            foreach (var eventType in Enum.GetValues<SecurityEventType>())
            {
                var count = recentEvents.Count(e => e.EventType == eventType);
                if (count > 0)
                {
                    metrics.EventTypeBreakdown[eventType.ToString()] = count;
                }
            }

            // Add top threats
            var topThreatIps = _ipProfiles
                .Where(p => p.Value.AuthenticationFailures > 0 || 
                           p.Value.SuspiciousActivities > 0 ||
                           p.Value.DataExfiltrationAttempts > 0)
                .OrderByDescending(p => p.Value.AuthenticationFailures + 
                                       p.Value.SuspiciousActivities + 
                                       p.Value.DataExfiltrationAttempts)
                .Take(10)
                .Select(p => new IpThreatInfo
                {
                    IpAddress = p.Key,
                    ThreatCount = p.Value.AuthenticationFailures + 
                                 p.Value.SuspiciousActivities + 
                                 p.Value.DataExfiltrationAttempts,
                    IsBanned = p.Value.IsBanned,
                    LastActivity = p.Value.LastActivity,
                    RiskScore = CalculateRiskScore(p.Value)
                })
                .ToList();

            metrics.TopThreats = topThreatIps;

            return Task.FromResult(metrics);
        }

        /// <summary>
        /// Get recent security events
        /// </summary>
        public Task<List<SecurityEventDto>> GetRecentSecurityEventsAsync(int minutes = 60)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
            var events = _recentEvents
                .Where(e => e.Timestamp >= cutoff)
                .OrderByDescending(e => e.Timestamp)
                .Select(e => new SecurityEventDto
                {
                    Id = e.Id,
                    EventType = e.EventType,
                    Timestamp = e.Timestamp,
                    IpAddress = e.IpAddress,
                    VirtualKey = e.VirtualKey,
                    Endpoint = e.Endpoint,
                    Activity = e.Activity,
                    Details = e.Details,
                    DataSize = e.DataSize,
                    Severity = DetermineEventSeverity(e)
                })
                .ToList();

            return Task.FromResult(events);
        }

        /// <summary>
        /// Start the background analysis service
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Security Event Monitoring Service");

            // Start periodic analysis
            _analysisTimer = new Timer(
                PerformSecurityAnalysis,
                null,
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds),
                TimeSpan.FromSeconds(_options.AnalysisIntervalSeconds));

            // Start cleanup timer
            _cleanupTimer = new Timer(
                PerformCleanup,
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(1));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop the background service
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Security Event Monitoring Service");

            _analysisTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _analysisTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _analysisSemaphore?.Dispose();
        }

        private void UpdateIpProfile(string ipAddress, Action<IpActivityProfile> updateAction)
        {
            _ipProfiles.AddOrUpdate(ipAddress,
                key =>
                {
                    var profile = new IpActivityProfile { IpAddress = key };
                    updateAction(profile);
                    return profile;
                },
                (key, existing) =>
                {
                    updateAction(existing);
                    existing.LastActivity = DateTime.UtcNow;
                    return existing;
                });
        }

        private void UpdateKeyProfile(string virtualKey, Action<VirtualKeyActivityProfile> updateAction)
        {
            _keyProfiles.AddOrUpdate(virtualKey,
                key =>
                {
                    var profile = new VirtualKeyActivityProfile { VirtualKey = key };
                    updateAction(profile);
                    return profile;
                },
                (key, existing) =>
                {
                    updateAction(existing);
                    existing.LastActivity = DateTime.UtcNow;
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

        private ThreatLevel CalculateThreatLevel(List<SecurityEvent> recentEvents)
        {
            if (recentEvents.Count == 0)
                return ThreatLevel.None;

            var failureRate = (double)recentEvents.Count(e => e.EventType == SecurityEventType.AuthenticationFailure) / recentEvents.Count;
            var suspiciousCount = recentEvents.Count(e => e.EventType == SecurityEventType.SuspiciousActivity);
            var exfiltrationCount = recentEvents.Count(e => e.EventType == SecurityEventType.DataExfiltration);

            if (exfiltrationCount > 5 || suspiciousCount > 20 || failureRate > 0.8)
                return ThreatLevel.Critical;
            if (exfiltrationCount > 2 || suspiciousCount > 10 || failureRate > 0.6)
                return ThreatLevel.High;
            if (suspiciousCount > 5 || failureRate > 0.4)
                return ThreatLevel.Medium;
            if (suspiciousCount > 0 || failureRate > 0.2)
                return ThreatLevel.Low;

            return ThreatLevel.None;
        }

        private int CalculateRiskScore(IpActivityProfile profile)
        {
            var score = 0;
            
            score += Math.Min(profile.AuthenticationFailures * 5, 40);
            score += Math.Min(profile.SuspiciousActivities * 10, 30);
            score += Math.Min(profile.DataExfiltrationAttempts * 15, 20);
            score += Math.Min(profile.RateLimitViolations * 2, 10);
            
            if (profile.IsBanned) score = 100;
            
            return Math.Min(score, 100);
        }

        private SecurityEventSeverity DetermineEventSeverity(SecurityEvent securityEvent)
        {
            return securityEvent.EventType switch
            {
                SecurityEventType.AuthenticationSuccess => SecurityEventSeverity.Info,
                SecurityEventType.AuthenticationFailure => SecurityEventSeverity.Low,
                SecurityEventType.RateLimitViolation => SecurityEventSeverity.Medium,
                SecurityEventType.SuspiciousActivity => SecurityEventSeverity.High,
                SecurityEventType.DataExfiltration => SecurityEventSeverity.Critical,
                SecurityEventType.AnomalousAccess => SecurityEventSeverity.High,
                SecurityEventType.IpBanned => SecurityEventSeverity.High,
                _ => SecurityEventSeverity.Info
            };
        }

        private async void PerformSecurityAnalysis(object? state)
        {
            if (!await _analysisSemaphore.WaitAsync(0))
            {
                _logger.LogDebug("Security analysis already in progress, skipping");
                return;
            }

            try
            {
                await DetectBruteForceAttacks();
                await DetectDistributedAttacks();
                await DetectAnomalousPatterns();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security analysis");
            }
            finally
            {
                _analysisSemaphore.Release();
            }
        }

        private async Task DetectBruteForceAttacks()
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-_options.BruteForceDetectionWindowMinutes);
            
            foreach (var ipProfile in _ipProfiles.Where(p => !p.Value.IsBanned))
            {
                var recentFailures = _recentEvents
                    .Where(e => e.IpAddress == ipProfile.Key &&
                               e.EventType == SecurityEventType.AuthenticationFailure &&
                               e.Timestamp >= windowStart)
                    .Count();

                if (recentFailures >= _options.BruteForceThreshold)
                {
                    _logger.LogWarning("Brute force attack detected from IP {IpAddress} with {Failures} failures",
                        ipProfile.Key, recentFailures);
                    
                    RecordSuspiciousActivity(ipProfile.Key, "Brute Force Attack",
                        $"Detected {recentFailures} authentication failures in {_options.BruteForceDetectionWindowMinutes} minutes");
                }
            }

            await Task.CompletedTask;
        }

        private async Task DetectDistributedAttacks()
        {
            var windowStart = DateTime.UtcNow.AddMinutes(-_options.DistributedAttackWindowMinutes);
            
            // Group by targeted virtual keys
            var keyTargets = _recentEvents
                .Where(e => e.Timestamp >= windowStart &&
                           e.EventType == SecurityEventType.AuthenticationFailure &&
                           !string.IsNullOrEmpty(e.VirtualKey))
                .GroupBy(e => e.VirtualKey)
                .Where(g => g.Select(e => e.IpAddress).Distinct().Count() >= _options.DistributedAttackIpThreshold)
                .ToList();

            foreach (var target in keyTargets)
            {
                var attackingIps = target.Select(e => e.IpAddress).Distinct().ToList();
                
                _logger.LogWarning("Distributed attack detected on virtual key {VirtualKey} from {IpCount} IPs",
                    target.Key, attackingIps.Count);
                
                foreach (var ip in attackingIps)
                {
                    RecordSuspiciousActivity(ip, "Distributed Attack",
                        $"Part of distributed attack on virtual key from {attackingIps.Count} IPs");
                }
            }

            await Task.CompletedTask;
        }

        private async Task DetectAnomalousPatterns()
        {
            var windowMinutes = 15;
            var windowStart = DateTime.UtcNow.AddMinutes(-windowMinutes);
            
            // Detect unusual endpoint access patterns
            foreach (var profile in _ipProfiles.Where(p => !p.Value.IsBanned))
            {
                var identifier = $"ip:{profile.Key}";
                var state = _anomalyStates.AddOrUpdate(identifier,
                    key => new AnomalyDetectionState
                    {
                        Identifier = key,
                        WindowStart = windowStart
                    },
                    (key, existing) =>
                    {
                        if (existing.WindowStart < windowStart.AddMinutes(-windowMinutes))
                        {
                            existing.EndpointAccess.Clear();
                            existing.WindowStart = windowStart;
                            existing.TotalRequests = 0;
                        }
                        return existing;
                    });

                // Count endpoint accesses
                var recentAccesses = _recentEvents
                    .Where(e => e.IpAddress == profile.Key &&
                               e.Timestamp >= state.WindowStart &&
                               !string.IsNullOrEmpty(e.Endpoint))
                    .ToList();

                foreach (var access in recentAccesses)
                {
                    state.EndpointAccess.TryGetValue(access.Endpoint!, out var count);
                    state.EndpointAccess[access.Endpoint!] = count + 1;
                    state.TotalRequests++;
                }

                // Detect anomalies
                if (state.EndpointAccess.Count > _options.AnomalousEndpointThreshold &&
                    state.TotalRequests > 50)
                {
                    RecordAnomalousAccess(profile.Key, "", "Endpoint Scanning",
                        $"Accessed {state.EndpointAccess.Count} different endpoints in {windowMinutes} minutes");
                }
            }

            await Task.CompletedTask;
        }

        private void PerformCleanup(object? state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-_options.DataRetentionHours);
                
                // Clean old events
                var eventsToKeep = new List<SecurityEvent>();
                while (_recentEvents.TryDequeue(out var evt))
                {
                    if (evt.Timestamp >= cutoff)
                    {
                        eventsToKeep.Add(evt);
                    }
                }
                
                foreach (var evt in eventsToKeep)
                {
                    _recentEvents.Enqueue(evt);
                }

                // Clean inactive IP profiles
                var inactiveIps = _ipProfiles
                    .Where(p => p.Value.LastActivity < cutoff && !p.Value.IsBanned)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var ip in inactiveIps)
                {
                    _ipProfiles.TryRemove(ip, out _);
                }

                // Clean inactive key profiles
                var inactiveKeys = _keyProfiles
                    .Where(p => p.Value.LastActivity < cutoff)
                    .Select(p => p.Key)
                    .ToList();

                foreach (var key in inactiveKeys)
                {
                    _keyProfiles.TryRemove(key, out _);
                }

                _logger.LogInformation("Security monitoring cleanup completed. Removed {IpCount} inactive IPs and {KeyCount} inactive keys",
                    inactiveIps.Count, inactiveKeys.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during security monitoring cleanup");
            }
        }
    }
}