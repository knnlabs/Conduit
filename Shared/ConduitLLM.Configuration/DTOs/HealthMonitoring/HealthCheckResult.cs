namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Health check result
    /// </summary>
    public class HealthCheckResult
    {
        /// <summary>
        /// Check name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Check duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// Additional data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Error description if unhealthy
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Exception details if failed
        /// </summary>
        public string? Exception { get; set; }
    }
}