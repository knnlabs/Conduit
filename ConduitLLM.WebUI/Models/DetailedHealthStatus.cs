using System;
using System.Collections.Generic;

namespace ConduitLLM.WebUI.Models
{
    /// <summary>
    /// Represents detailed health status information from the Admin API
    /// </summary>
    public class DetailedHealthStatus
    {
        /// <summary>
        /// Overall health status
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// Individual health check results
        /// </summary>
        public List<HealthCheckResult> Checks { get; set; } = new List<HealthCheckResult>();

        /// <summary>
        /// Total duration of all health checks in milliseconds
        /// </summary>
        public double TotalDuration { get; set; }

        /// <summary>
        /// Timestamp when the health check was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents an individual health check result
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Name of the health check
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Status of the health check
        /// </summary>
        public string Status { get; set; } = "Unknown";

        /// <summary>
        /// Description of the health check
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Duration of the health check in milliseconds
        /// </summary>
        public double Duration { get; set; }

        /// <summary>
        /// Exception message if the health check failed
        /// </summary>
        public string? Exception { get; set; }

        /// <summary>
        /// Additional data from the health check
        /// </summary>
        public Dictionary<string, object>? Data { get; set; }
    }
}