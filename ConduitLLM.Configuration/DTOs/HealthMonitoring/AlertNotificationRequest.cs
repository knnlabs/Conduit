using System.Collections.Generic;

namespace ConduitLLM.Configuration.DTOs.HealthMonitoring
{
    /// <summary>
    /// Alert notification request
    /// </summary>
    public class AlertNotificationRequest
    {
        /// <summary>
        /// Alert to send
        /// </summary>
        public HealthAlert Alert { get; set; } = new();

        /// <summary>
        /// Notification channels
        /// </summary>
        public List<string> Channels { get; set; } = new();

        /// <summary>
        /// Additional recipients
        /// </summary>
        public List<string> Recipients { get; set; } = new();
    }
}