namespace ConduitLLM.Configuration.DTOs.Monitoring
{
    /// <summary>
    /// Data transfer object for cache alert information
    /// </summary>
    public class CacheAlertDto
    {
        /// <summary>
        /// Machine-readable alert identifier (e.g., cache_high_memory)
        /// </summary>
        public string AlertType { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable explanation of the alert
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level of the alert (info, warning, critical)
        /// </summary>
        public string Severity { get; set; } = string.Empty;

        /// <summary>
        /// Optional cache region associated with the alert
        /// </summary>
        public string? Region { get; set; }

        /// <summary>
        /// Timestamp when the alert was raised
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Additional context data for the alert
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Additional details for the alert
        /// </summary>
        public Dictionary<string, object> Details { get; set; } = new();
    }
}