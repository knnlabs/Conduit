using System;
using System.Collections.Generic;

namespace ConduitLLM.Admin.DTOs
{
    /// <summary>
    /// Time range covered by a monitoring response.
    /// </summary>
    public class TimeRangeDto
    {
        /// <summary>
        /// Start of the time range (UTC).
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// End of the time range (UTC).
        /// </summary>
        public DateTime End { get; set; }
    }

    /// <summary>
    /// Response containing current health status for all monitored services.
    /// </summary>
    public class ServiceHealthResponse
    {
        /// <summary>
        /// Timestamp when the health snapshot was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Overall health status across all services (healthy, degraded, or unhealthy).
        /// </summary>
        public string OverallStatus { get; set; } = string.Empty;

        /// <summary>
        /// Summary counts of services by health status.
        /// </summary>
        public ServiceHealthSummary Summary { get; set; } = new();

        /// <summary>
        /// Health details for each monitored service.
        /// </summary>
        public List<ServiceStatusDto> Services { get; set; } = new();
    }

    /// <summary>
    /// Summary counts of services by health status.
    /// </summary>
    public class ServiceHealthSummary
    {
        /// <summary>
        /// Number of healthy services.
        /// </summary>
        public int Healthy { get; set; }

        /// <summary>
        /// Number of degraded services.
        /// </summary>
        public int Degraded { get; set; }

        /// <summary>
        /// Number of unhealthy services.
        /// </summary>
        public int Unhealthy { get; set; }

        /// <summary>
        /// Total number of monitored services.
        /// </summary>
        public int Total { get; set; }
    }

    /// <summary>
    /// Health status for a single monitored service.
    /// </summary>
    public class ServiceStatusDto
    {
        /// <summary>
        /// Service identifier (e.g. core-api, admin-api, database).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the service.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status of the service (healthy, degraded, or unhealthy).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// How long the service has been running.
        /// </summary>
        public TimeSpan Uptime { get; set; }

        /// <summary>
        /// Timestamp of the last health check (UTC).
        /// </summary>
        public DateTime LastCheck { get; set; }

        /// <summary>
        /// Health check response time in milliseconds.
        /// </summary>
        public int ResponseTime { get; set; }

        /// <summary>
        /// Service-specific detail values (shape varies per service).
        /// </summary>
        public object Details { get; set; } = new();
    }

    /// <summary>
    /// Response containing incident history derived from request logs.
    /// </summary>
    public class IncidentsResponse
    {
        /// <summary>
        /// Timestamp when the response was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Time range analyzed for incidents.
        /// </summary>
        public TimeRangeDto TimeRange { get; set; } = new();

        /// <summary>
        /// Total number of incidents in the time range.
        /// </summary>
        public int TotalIncidents { get; set; }

        /// <summary>
        /// Number of incidents that are currently active.
        /// </summary>
        public int ActiveIncidents { get; set; }

        /// <summary>
        /// Incident counts grouped by incident type.
        /// </summary>
        public List<IncidentTypeCountDto> IncidentsByType { get; set; } = new();

        /// <summary>
        /// Incident counts grouped by severity.
        /// </summary>
        public List<IncidentSeverityCountDto> IncidentsBySeverity { get; set; } = new();

        /// <summary>
        /// The individual incidents, most recent first.
        /// </summary>
        public List<IncidentDto> Incidents { get; set; } = new();
    }

    /// <summary>
    /// Number of incidents of a given type.
    /// </summary>
    public class IncidentTypeCountDto
    {
        /// <summary>
        /// Incident type identifier.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Number of incidents of this type.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// Number of incidents of a given severity.
    /// </summary>
    public class IncidentSeverityCountDto
    {
        /// <summary>
        /// Incident severity (critical, major, or minor).
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Number of incidents with this severity.
        /// </summary>
        public int Count { get; set; }
    }

    /// <summary>
    /// A single incident derived from request log analysis.
    /// </summary>
    public class IncidentDto
    {
        /// <summary>
        /// Unique identifier for the incident.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable incident title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Incident type identifier (e.g. service_degradation).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Incident severity (critical, major, or minor).
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Incident status (active or resolved).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the incident started (UTC).
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the incident ended (UTC), or null if still active.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Name of the affected service.
        /// </summary>
        public string AffectedService { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable impact description.
        /// </summary>
        public string Impact { get; set; } = string.Empty;

        /// <summary>
        /// Additional incident detail metrics.
        /// </summary>
        public IncidentDetailsDto Details { get; set; } = new();
    }

    /// <summary>
    /// Detail metrics for an incident.
    /// </summary>
    public class IncidentDetailsDto
    {
        /// <summary>
        /// Number of errors observed during the incident window.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Number of distinct error status codes observed.
        /// </summary>
        public int UniqueErrorTypes { get; set; }
    }

    /// <summary>
    /// Response containing health history time series data.
    /// </summary>
    public class HealthHistoryResponse
    {
        /// <summary>
        /// Timestamp when the response was generated (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Time range covered by the history.
        /// </summary>
        public TimeRangeDto TimeRange { get; set; } = new();

        /// <summary>
        /// Length of each history interval in minutes.
        /// </summary>
        public int IntervalMinutes { get; set; }

        /// <summary>
        /// Health metrics for each interval, oldest first.
        /// </summary>
        public List<HealthHistoryPointDto> History { get; set; } = new();
    }

    /// <summary>
    /// Health metrics for a single time interval.
    /// </summary>
    public class HealthHistoryPointDto
    {
        /// <summary>
        /// Start of the interval (UTC).
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// System health percentage (100 minus error rate).
        /// </summary>
        public double SystemHealth { get; set; }

        /// <summary>
        /// Provider health percentage.
        /// </summary>
        public int ProviderHealth { get; set; }

        /// <summary>
        /// Average response time in milliseconds for the interval.
        /// </summary>
        public double ResponseTime { get; set; }

        /// <summary>
        /// Number of requests handled during the interval.
        /// </summary>
        public int RequestVolume { get; set; }

        /// <summary>
        /// Percentage of requests that resulted in an error.
        /// </summary>
        public double ErrorRate { get; set; }
    }
}
