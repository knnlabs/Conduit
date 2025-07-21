using System;
using System.Collections.Generic;

namespace ConduitLLM.Core.Alerts
{
    /// <summary>
    /// Defines the types of cache-related alerts
    /// </summary>
    public enum CacheAlertType
    {
        /// <summary>
        /// Cache hit rate is below configured threshold
        /// </summary>
        LowHitRate,

        /// <summary>
        /// Cache memory usage is above configured threshold
        /// </summary>
        HighMemoryUsage,

        /// <summary>
        /// Cache eviction rate is above configured threshold
        /// </summary>
        HighEvictionRate,

        /// <summary>
        /// Cache response time is above configured threshold
        /// </summary>
        HighResponseTime,

        /// <summary>
        /// Cache infrastructure is unhealthy
        /// </summary>
        CacheUnhealthy,

        /// <summary>
        /// Redis connection lost
        /// </summary>
        RedisConnectionLost,

        /// <summary>
        /// Cache region is disabled or failing
        /// </summary>
        RegionFailure
    }

    /// <summary>
    /// Severity levels for cache alerts
    /// </summary>
    public enum CacheAlertSeverity
    {
        /// <summary>
        /// Informational alert
        /// </summary>
        Info,

        /// <summary>
        /// Warning that may require attention
        /// </summary>
        Warning,

        /// <summary>
        /// Error requiring immediate attention
        /// </summary>
        Error,

        /// <summary>
        /// Critical failure affecting service availability
        /// </summary>
        Critical
    }

    /// <summary>
    /// Cache alert definition
    /// </summary>
    public class CacheAlertDefinition
    {
        /// <summary>
        /// Unique identifier for the alert type
        /// </summary>
        public CacheAlertType Type { get; set; }

        /// <summary>
        /// Human-readable name for the alert
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Default severity level
        /// </summary>
        public CacheAlertSeverity DefaultSeverity { get; set; }

        /// <summary>
        /// Description of what triggers this alert
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Recommended actions to resolve the alert
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>
        /// Whether this alert should trigger notifications
        /// </summary>
        public bool NotificationEnabled { get; set; } = true;

        /// <summary>
        /// Cooldown period before the same alert can be triggered again
        /// </summary>
        public TimeSpan CooldownPeriod { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Static definitions for cache alerts
    /// </summary>
    public static class CacheAlertDefinitions
    {
        /// <summary>
        /// Gets all defined cache alerts
        /// </summary>
        public static readonly Dictionary<CacheAlertType, CacheAlertDefinition> Alerts = new()
        {
            [CacheAlertType.LowHitRate] = new CacheAlertDefinition
            {
                Type = CacheAlertType.LowHitRate,
                Name = "Low Cache Hit Rate",
                DefaultSeverity = CacheAlertSeverity.Warning,
                Description = "Cache hit rate has fallen below the configured threshold, indicating poor cache effectiveness",
                RecommendedActions = new List<string>
                {
                    "Review cache key generation logic",
                    "Analyze cache miss patterns",
                    "Consider increasing cache TTL",
                    "Verify cache warming processes"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(15)
            },

            [CacheAlertType.HighMemoryUsage] = new CacheAlertDefinition
            {
                Type = CacheAlertType.HighMemoryUsage,
                Name = "High Cache Memory Usage",
                DefaultSeverity = CacheAlertSeverity.Warning,
                Description = "Cache memory usage is approaching or exceeding configured limits",
                RecommendedActions = new List<string>
                {
                    "Review cache eviction policies",
                    "Consider increasing cache memory limits",
                    "Analyze large cache entries",
                    "Implement cache entry compression"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(10)
            },

            [CacheAlertType.HighEvictionRate] = new CacheAlertDefinition
            {
                Type = CacheAlertType.HighEvictionRate,
                Name = "High Cache Eviction Rate",
                DefaultSeverity = CacheAlertSeverity.Warning,
                Description = "Cache entries are being evicted at a high rate, potentially impacting performance",
                RecommendedActions = new List<string>
                {
                    "Increase cache memory allocation",
                    "Review cache TTL settings",
                    "Optimize cache entry sizes",
                    "Consider using tiered caching"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(10)
            },

            [CacheAlertType.HighResponseTime] = new CacheAlertDefinition
            {
                Type = CacheAlertType.HighResponseTime,
                Name = "High Cache Response Time",
                DefaultSeverity = CacheAlertSeverity.Warning,
                Description = "Cache retrieval operations are taking longer than expected",
                RecommendedActions = new List<string>
                {
                    "Check Redis/distributed cache performance",
                    "Review serialization overhead",
                    "Monitor network latency",
                    "Consider local cache layer"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(5)
            },

            [CacheAlertType.CacheUnhealthy] = new CacheAlertDefinition
            {
                Type = CacheAlertType.CacheUnhealthy,
                Name = "Cache Infrastructure Unhealthy",
                DefaultSeverity = CacheAlertSeverity.Error,
                Description = "Cache infrastructure is reporting unhealthy status",
                RecommendedActions = new List<string>
                {
                    "Check cache service logs",
                    "Verify Redis connectivity",
                    "Review memory allocation",
                    "Restart cache services if necessary"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(5)
            },

            [CacheAlertType.RedisConnectionLost] = new CacheAlertDefinition
            {
                Type = CacheAlertType.RedisConnectionLost,
                Name = "Redis Connection Lost",
                DefaultSeverity = CacheAlertSeverity.Critical,
                Description = "Connection to Redis distributed cache has been lost",
                RecommendedActions = new List<string>
                {
                    "Check Redis server status",
                    "Verify network connectivity",
                    "Review Redis configuration",
                    "Check authentication credentials"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(2)
            },

            [CacheAlertType.RegionFailure] = new CacheAlertDefinition
            {
                Type = CacheAlertType.RegionFailure,
                Name = "Cache Region Failure",
                DefaultSeverity = CacheAlertSeverity.Error,
                Description = "A cache region has failed or been disabled",
                RecommendedActions = new List<string>
                {
                    "Check region configuration",
                    "Review region-specific errors",
                    "Verify memory allocation",
                    "Re-enable region if safe"
                },
                NotificationEnabled = true,
                CooldownPeriod = TimeSpan.FromMinutes(10)
            }
        };

        /// <summary>
        /// Gets the alert definition for a specific type
        /// </summary>
        public static CacheAlertDefinition GetDefinition(CacheAlertType type)
        {
            return Alerts.TryGetValue(type, out var definition) 
                ? definition 
                : throw new ArgumentException($"No definition found for alert type: {type}");
        }

        /// <summary>
        /// Maps string alert type to enum
        /// </summary>
        public static CacheAlertType? ParseAlertType(string alertType)
        {
            return alertType?.ToLowerInvariant() switch
            {
                "lowhitrate" => CacheAlertType.LowHitRate,
                "highmemoryusage" => CacheAlertType.HighMemoryUsage,
                "highevictionrate" => CacheAlertType.HighEvictionRate,
                "highresponsetime" => CacheAlertType.HighResponseTime,
                "cacheunhealthy" => CacheAlertType.CacheUnhealthy,
                "redisconnectionlost" => CacheAlertType.RedisConnectionLost,
                "regionfailure" => CacheAlertType.RegionFailure,
                _ => null
            };
        }

        /// <summary>
        /// Maps severity string to enum
        /// </summary>
        public static CacheAlertSeverity ParseSeverity(string severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                "info" => CacheAlertSeverity.Info,
                "warning" => CacheAlertSeverity.Warning,
                "error" => CacheAlertSeverity.Error,
                "critical" => CacheAlertSeverity.Critical,
                _ => CacheAlertSeverity.Warning
            };
        }
    }
}