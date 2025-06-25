using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for tracking webhook delivery status and preventing duplicate deliveries
    /// </summary>
    public interface IWebhookDeliveryTracker
    {
        /// <summary>
        /// Checks if a webhook has already been delivered
        /// </summary>
        /// <param name="deliveryKey">Unique key identifying the webhook delivery (TaskId:EventType:MessageId)</param>
        /// <returns>True if the webhook has already been delivered, false otherwise</returns>
        Task<bool> IsDeliveredAsync(string deliveryKey);
        
        /// <summary>
        /// Marks a webhook as delivered
        /// </summary>
        /// <param name="deliveryKey">Unique key identifying the webhook delivery</param>
        /// <param name="webhookUrl">URL the webhook was delivered to</param>
        /// <returns>Task representing the async operation</returns>
        Task MarkDeliveredAsync(string deliveryKey, string webhookUrl);
        
        /// <summary>
        /// Gets delivery statistics for a specific webhook URL
        /// </summary>
        /// <param name="webhookUrl">The webhook URL to get stats for</param>
        /// <returns>Delivery statistics for the webhook URL</returns>
        Task<WebhookDeliveryStats> GetStatsAsync(string webhookUrl);
        
        /// <summary>
        /// Records a failed delivery attempt
        /// </summary>
        /// <param name="deliveryKey">Unique key identifying the webhook delivery</param>
        /// <param name="webhookUrl">URL the webhook failed to deliver to</param>
        /// <param name="error">Error message</param>
        /// <returns>Task representing the async operation</returns>
        Task RecordFailureAsync(string deliveryKey, string webhookUrl, string error);
    }
    
    /// <summary>
    /// Webhook delivery statistics
    /// </summary>
    public class WebhookDeliveryStats
    {
        /// <summary>
        /// Total number of successful deliveries
        /// </summary>
        public long DeliveredCount { get; init; }
        
        /// <summary>
        /// Total number of failed deliveries
        /// </summary>
        public long FailedCount { get; init; }
        
        /// <summary>
        /// Last successful delivery timestamp
        /// </summary>
        public DateTime? LastDeliveryTime { get; init; }
        
        /// <summary>
        /// Last failed delivery timestamp
        /// </summary>
        public DateTime? LastFailureTime { get; init; }
        
        /// <summary>
        /// Success rate percentage (0-100)
        /// </summary>
        public double SuccessRate => 
            DeliveredCount + FailedCount > 0 
                ? (double)DeliveredCount / (DeliveredCount + FailedCount) * 100 
                : 0;
    }
}