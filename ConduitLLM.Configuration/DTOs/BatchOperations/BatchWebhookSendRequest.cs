using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ConduitLLM.Configuration.DTOs.BatchOperations
{
    /// <summary>
    /// Request to send webhooks in batch
    /// </summary>
    public class BatchWebhookSendRequest
    {
        /// <summary>
        /// List of webhooks to send
        /// </summary>
        [Required]
        public List<WebhookSendDto> Webhooks { get; set; } = new();
    }
}