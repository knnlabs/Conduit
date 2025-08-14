using System;

namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Alert suppression configuration
    /// </summary>
    public class AlertSuppression
    {
        /// <summary>
        /// Suppression rule ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Alert pattern to suppress
        /// </summary>
        public string AlertPattern { get; set; } = string.Empty;

        /// <summary>
        /// Start time
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Reason for suppression
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Created by user
        /// </summary>
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// Is active
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}