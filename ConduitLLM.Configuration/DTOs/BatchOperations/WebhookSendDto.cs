using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Individual webhook to send
    /// </summary>
    public class WebhookSendDto
    {
        /// <summary>
        /// Webhook URL
        /// </summary>
        [Required]
        [Url]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Event type
        /// </summary>
        [Required]
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Payload to send
        /// </summary>
        [Required]
        public object Payload { get; set; } = new { };

        /// <summary>
        /// Custom headers
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Webhook secret for signature
        /// </summary>
        public string? Secret { get; set; }
    }
}