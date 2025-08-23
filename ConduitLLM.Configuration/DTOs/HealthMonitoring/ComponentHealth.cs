namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Component health status
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Component name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Last check timestamp
        /// </summary>
        public DateTime LastCheck { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Response time in milliseconds
        /// </summary>
        public double? ResponseTimeMs { get; set; }

        /// <summary>
        /// Error message if unhealthy
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional health metrics
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// Dependencies health status
        /// </summary>
        public List<ComponentHealth> Dependencies { get; set; } = new();
    }
}