using ConduitLLM.Configuration.DTOs.Security;
using ConduitLLM.Security.Models;

namespace ConduitLLM.Security.Services
{
    /// <summary>
    /// Security event recording methods for SecurityEventMonitoringService
    /// </summary>
    public partial class SecurityEventMonitoringService
    {
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
    }
}