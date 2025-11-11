namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// System health snapshot
    /// </summary>
    public class SystemHealthSnapshot
    {
        /// <summary>
        /// Snapshot timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Overall system health
        /// </summary>
        public HealthStatus OverallStatus { get; set; }

        /// <summary>
        /// Individual component health statuses
        /// </summary>
        public List<ComponentHealth> Components { get; set; } = new();

        /// <summary>
        /// Active alerts
        /// </summary>
        public List<HealthAlert> ActiveAlerts { get; set; } = new();

        /// <summary>
        /// System resource metrics
        /// </summary>
        public ResourceMetrics Resources { get; set; } = new();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public PerformanceMetrics Performance { get; set; } = new();
    }
}