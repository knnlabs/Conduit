namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for alert definition information
    /// </summary>
    public class AlertDefinitionDto
    {
        /// <summary>
        /// Alert identifier
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Alert name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Alert description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Alert severity level
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Whether the alert is enabled
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Threshold values for the alert
        /// </summary>
        public Dictionary<string, object> Thresholds { get; set; } = new();

        /// <summary>
        /// Alert type
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Default severity for the alert
        /// </summary>
        public string DefaultSeverity { get; set; } = string.Empty;

        /// <summary>
        /// Recommended actions to take when alert is triggered
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>
        /// Whether notifications are enabled for this alert
        /// </summary>
        public bool NotificationEnabled { get; set; }

        /// <summary>
        /// Cooldown period in minutes before the alert can be triggered again
        /// </summary>
        public int CooldownPeriodMinutes { get; set; }
    }
}