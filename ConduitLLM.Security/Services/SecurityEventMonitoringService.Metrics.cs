using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.Security;
using ConduitLLM.Security.Models;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Security metrics and statistics methods for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringService
    {
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
                TotalEvents = recentEvents.Count(),
                AuthenticationFailures = recentEvents.Count(e => e.EventType == SecurityEventType.AuthenticationFailure),
                RateLimitViolations = recentEvents.Count(e => e.EventType == SecurityEventType.RateLimitViolation),
                SuspiciousActivities = recentEvents.Count(e => e.EventType == SecurityEventType.SuspiciousActivity),
                DataExfiltrationAttempts = recentEvents.Count(e => e.EventType == SecurityEventType.DataExfiltration),
                AnomalousAccessPatterns = recentEvents.Count(e => e.EventType == SecurityEventType.AnomalousAccess),
                ActiveIpBans = _ipProfiles.Count(p => p.Value.IsBanned),
                UniqueIpsMonitored = _ipProfiles.Count(),
                UniqueKeysMonitored = _keyProfiles.Count(),
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
        /// Calculate threat level based on recent security events
        /// </summary>
        private ThreatLevel CalculateThreatLevel(List<SecurityEvent> recentEvents)
        {
            if (recentEvents.Count() == 0)
                return ThreatLevel.None;

            var failureRate = (double)recentEvents.Count(e => e.EventType == SecurityEventType.AuthenticationFailure) / recentEvents.Count();
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

        /// <summary>
        /// Calculate risk score for an IP activity profile
        /// </summary>
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

        /// <summary>
        /// Determine event severity based on security event type
        /// </summary>
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
    }
}