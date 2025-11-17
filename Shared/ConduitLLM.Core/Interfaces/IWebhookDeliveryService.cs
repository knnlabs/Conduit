namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for tracking webhook delivery status and metrics
    /// </summary>
    public interface IWebhookDeliveryService
    {
        /// <summary>
        /// Notifies that a webhook was successfully delivered
        /// </summary>
        /// <param name="webhookUrl">The webhook URL</param>
        /// <param name="statusCode">HTTP status code returned</param>
        /// <param name="responseTime">Time taken to deliver the webhook</param>
        Task NotifyDeliverySuccessAsync(string webhookUrl, int statusCode, TimeSpan responseTime);

        /// <summary>
        /// Notifies that a webhook delivery failed
        /// </summary>
        /// <param name="webhookUrl">The webhook URL</param>
        /// <param name="statusCode">HTTP status code returned (0 if no response)</param>
        /// <param name="errorMessage">Error message or response body</param>
        /// <param name="isRetryable">Whether the webhook can be retried</param>
        Task NotifyDeliveryFailureAsync(string webhookUrl, int statusCode, string errorMessage, bool isRetryable);
    }
}