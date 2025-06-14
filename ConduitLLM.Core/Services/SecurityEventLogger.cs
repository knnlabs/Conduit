using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Middleware;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Default implementation of security event logging.
    /// </summary>
    /// <remarks>
    /// This implementation logs security events to the standard logging infrastructure
    /// and maintains an in-memory buffer for recent events. In production, this should
    /// be replaced with a persistent storage implementation.
    /// </remarks>
    public class SecurityEventLogger : ISecurityEventLogger
    {
        private readonly ILogger<SecurityEventLogger> _logger;
        private readonly ConcurrentQueue<SecurityEvent> _recentEvents = new();
        private const int MaxRecentEvents = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityEventLogger"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public SecurityEventLogger(ILogger<SecurityEventLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task LogAuthenticationFailureAsync(
            string virtualKey, 
            string ipAddress, 
            string? reason = null,
            Dictionary<string, object>? additionalContext = null)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationFailure,
                Severity = SecurityEventSeverity.Medium,
                Description = "Authentication failed for virtual key from " + 
                             InputSanitizationMiddleware.SanitizeString(ipAddress) + 
                             (reason != null ? ": " + InputSanitizationMiddleware.SanitizeString(reason) : ""),
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = additionalContext ?? new Dictionary<string, object>()
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogAuthenticationSuccessAsync(
            string virtualKey, 
            string ipAddress,
            Dictionary<string, object>? additionalContext = null)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.AuthenticationSuccess,
                Severity = SecurityEventSeverity.Low,
                Description = "Authentication successful for virtual key from " + InputSanitizationMiddleware.SanitizeString(ipAddress),
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = additionalContext ?? new Dictionary<string, object>()
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogRateLimitExceededAsync(
            string virtualKey, 
            string endpoint, 
            int limit, 
            TimeSpan window,
            string ipAddress)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.RateLimitExceeded,
                Severity = SecurityEventSeverity.Medium,
                Description = "Rate limit exceeded for " + InputSanitizationMiddleware.SanitizeString(endpoint) + 
                             ": " + limit + " requests per " + window.TotalSeconds + "s",
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = new Dictionary<string, object>
                {
                    ["endpoint"] = endpoint,
                    ["limit"] = limit,
                    ["windowSeconds"] = window.TotalSeconds
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogSuspiciousActivityAsync(
            string description, 
            SecurityEventSeverity severity,
            Dictionary<string, object> context)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.SuspiciousActivity,
                Severity = severity,
                Description = description,
                Context = context
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogAuthorizationViolationAsync(
            string virtualKey,
            string resource,
            string action,
            string ipAddress)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.AuthorizationViolation,
                Severity = SecurityEventSeverity.High,
                Description = "Unauthorized access attempt to " + InputSanitizationMiddleware.SanitizeString(resource) + 
                             " for action " + InputSanitizationMiddleware.SanitizeString(action),
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = new Dictionary<string, object>
                {
                    ["resource"] = resource,
                    ["action"] = action
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogIpFilteringEventAsync(
            string ipAddress,
            IpFilterAction action,
            string reason,
            string? virtualKey = null)
        {
            var severity = action == IpFilterAction.Blocked ? 
                SecurityEventSeverity.Medium : SecurityEventSeverity.Low;

            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.IpFiltering,
                Severity = severity,
                Description = "IP " + InputSanitizationMiddleware.SanitizeString(ipAddress) + 
                             " was " + action.ToString() + ": " + InputSanitizationMiddleware.SanitizeString(reason),
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = new Dictionary<string, object>
                {
                    ["action"] = action.ToString(),
                    ["reason"] = reason
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogValidationFailureAsync(
            string endpoint,
            string fieldName,
            string invalidValue,
            string ipAddress,
            string? virtualKey = null)
        {
            // Sanitize the invalid value to prevent log injection
            var sanitizedValue = InputSanitizationMiddleware.SanitizeString(invalidValue);

            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.ValidationFailure,
                Severity = SecurityEventSeverity.Low,
                Description = "Validation failed for " + InputSanitizationMiddleware.SanitizeString(fieldName) + 
                             " at " + InputSanitizationMiddleware.SanitizeString(endpoint),
                VirtualKey = virtualKey,
                IpAddress = ipAddress,
                Context = new Dictionary<string, object>
                {
                    ["endpoint"] = endpoint,
                    ["fieldName"] = fieldName,
                    ["invalidValue"] = sanitizedValue
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogApiKeyRotationAsync(
            string virtualKey,
            string reason,
            string performedBy)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.ApiKeyRotation,
                Severity = SecurityEventSeverity.Low,
                Description = "API key rotated for " + InputSanitizationMiddleware.SanitizeString(virtualKey) + 
                             " by " + InputSanitizationMiddleware.SanitizeString(performedBy) + 
                             ": " + InputSanitizationMiddleware.SanitizeString(reason),
                VirtualKey = virtualKey,
                Context = new Dictionary<string, object>
                {
                    ["reason"] = reason,
                    ["performedBy"] = performedBy
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task LogSecurityConfigurationChangeAsync(
            string setting,
            string oldValue,
            string newValue,
            string changedBy)
        {
            var evt = new SecurityEvent
            {
                EventType = SecurityEventType.ConfigurationChange,
                Severity = SecurityEventSeverity.Medium,
                Description = "Security setting '" + InputSanitizationMiddleware.SanitizeString(setting) + 
                             "' changed by " + InputSanitizationMiddleware.SanitizeString(changedBy),
                Context = new Dictionary<string, object>
                {
                    ["setting"] = setting,
                    ["oldValue"] = oldValue,
                    ["newValue"] = newValue,
                    ["changedBy"] = changedBy
                }
            };

            LogEvent(evt);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IEnumerable<SecurityEvent>> GetSecurityEventsAsync(
            DateTime startTime,
            DateTime endTime,
            SecurityEventType[]? eventTypes = null,
            SecurityEventSeverity? severity = null)
        {
            var events = _recentEvents.Where(e => 
                e.Timestamp >= startTime && 
                e.Timestamp <= endTime);

            if (eventTypes?.Length > 0)
            {
                events = events.Where(e => eventTypes.Contains(e.EventType));
            }

            if (severity.HasValue)
            {
                events = events.Where(e => e.Severity >= severity.Value);
            }

            return Task.FromResult<IEnumerable<SecurityEvent>>(events.OrderByDescending(e => e.Timestamp).ToList());
        }

        /// <inheritdoc/>
        public Task<SecurityEventStatistics> GetStatisticsAsync(
            DateTime startTime,
            DateTime endTime)
        {
            var events = _recentEvents
                .Where(e => e.Timestamp >= startTime && e.Timestamp <= endTime)
                .ToList();

            var stats = new SecurityEventStatistics
            {
                StartTime = startTime,
                EndTime = endTime,
                TotalEvents = events.Count
            };

            // Group by event type
            stats.EventsByType = events
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());

            // Group by severity
            stats.EventsBySeverity = events
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());

            // Top IP addresses
            stats.TopIpAddresses = events
                .Where(e => !string.IsNullOrEmpty(e.IpAddress))
                .GroupBy(e => e.IpAddress!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => (g.Key, g.Count()))
                .ToList();

            // Top virtual keys
            stats.TopVirtualKeys = events
                .Where(e => !string.IsNullOrEmpty(e.VirtualKey))
                .GroupBy(e => e.VirtualKey!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => (g.Key, g.Count()))
                .ToList();

            // Calculate authentication failure rate
            var authEvents = events.Where(e => 
                e.EventType == SecurityEventType.AuthenticationSuccess || 
                e.EventType == SecurityEventType.AuthenticationFailure).ToList();
            
            if (authEvents.Any())
            {
                var failures = authEvents.Count(e => 
                    e.EventType == SecurityEventType.AuthenticationFailure);
                stats.AuthenticationFailureRate = (double)failures / authEvents.Count;
            }

            // Count rate limit violations
            stats.RateLimitViolations = events.Count(e => 
                e.EventType == SecurityEventType.RateLimitExceeded);

            return Task.FromResult(stats);
        }

        private void LogEvent(SecurityEvent evt)
        {
            // Add to recent events queue
            _recentEvents.Enqueue(evt);

            // Maintain queue size limit
            while (_recentEvents.Count > MaxRecentEvents)
            {
                _recentEvents.TryDequeue(out _);
            }

            // Log to standard logging infrastructure
            var logLevel = evt.Severity switch
            {
                SecurityEventSeverity.Critical => LogLevel.Critical,
                SecurityEventSeverity.High => LogLevel.Error,
                SecurityEventSeverity.Medium => LogLevel.Warning,
                _ => LogLevel.Information
            };

            _logger.Log(logLevel, 
                "Security Event: {EventType} - {Description} [VirtualKey: {VirtualKey}, IP: {IpAddress}]",
                evt.EventType, evt.Description, evt.VirtualKey ?? "N/A", evt.IpAddress ?? "N/A");
        }
    }
}