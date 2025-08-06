using System;

namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Alert history entry
    /// </summary>
    public class AlertHistoryEntry
    {
        /// <summary>
        /// Entry ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Alert ID
        /// </summary>
        public string AlertId { get; set; } = string.Empty;

        /// <summary>
        /// Action taken
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// User who took action
        /// </summary>
        public string? User { get; set; }

        /// <summary>
        /// Action timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional notes
        /// </summary>
        public string? Notes { get; set; }
    }
}