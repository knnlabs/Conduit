namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Alert rule configuration
    /// </summary>
    public class AlertRule
    {
        /// <summary>
        /// Rule identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Rule name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Rule description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Component to monitor
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// Metric to evaluate
        /// </summary>
        public string Metric { get; set; } = string.Empty;

        /// <summary>
        /// Comparison operator
        /// </summary>
        public ComparisonOperator Operator { get; set; }

        /// <summary>
        /// Threshold value
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Alert severity
        /// </summary>
        public AlertSeverity Severity { get; set; }

        /// <summary>
        /// Alert type
        /// </summary>
        public AlertType AlertType { get; set; }

        /// <summary>
        /// Evaluation window in seconds
        /// </summary>
        public int EvaluationWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Minimum occurrences before alerting
        /// </summary>
        public int MinOccurrences { get; set; } = 1;

        /// <summary>
        /// Is rule enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Suppression period after alert in minutes
        /// </summary>
        public int SuppressionMinutes { get; set; } = 5;
    }
}