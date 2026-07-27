namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Represents the delivery status of a webhook.
    /// </summary>
    public enum WebhookDeliveryStatus
    {
        /// <summary>
        /// Webhook delivery is pending.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Webhook was delivered successfully.
        /// </summary>
        Delivered = 1,

        /// <summary>
        /// Webhook delivery failed.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// Webhook delivery was retried.
        /// </summary>
        Retrying = 3,

        /// <summary>
        /// Webhook delivery abandoned after max retries.
        /// </summary>
        Abandoned = 4
    }
}
