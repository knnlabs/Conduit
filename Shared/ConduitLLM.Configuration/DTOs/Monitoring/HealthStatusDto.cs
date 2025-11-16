namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object containing health status information
    /// </summary>
    public class HealthStatusDto
    {
        /// <summary>
        /// Overall system status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp when the health check was performed
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Individual component health statuses
        /// </summary>
        public Dictionary<string, ComponentHealth> Checks { get; set; } = new();

        /// <summary>
        /// Total duration of all health checks in milliseconds
        /// </summary>
        public double TotalDuration { get; set; }
    }

    /// <summary>
    /// Component health information
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Component status
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Component description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Duration of the health check in milliseconds
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// Error message if the health check failed
        /// </summary>
        public string? Error { get; set; }
    }
}