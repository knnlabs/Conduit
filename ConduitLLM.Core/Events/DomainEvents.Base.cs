namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Base interface for all domain events in the Conduit system
    /// </summary>
    public interface IDomainEvent
    {
        /// <summary>
        /// Unique identifier for the event
        /// </summary>
        string EventId { get; }
        
        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        DateTime Timestamp { get; }
        
        /// <summary>
        /// Correlation ID for tracking related events
        /// </summary>
        string CorrelationId { get; }
    }

    /// <summary>
    /// Base record for domain events with common properties
    /// </summary>
    public abstract record DomainEvent : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string CorrelationId { get; init; } = string.Empty;
    }


    /// <summary>
    /// Represents the status of a video generation task
    /// </summary>
    public enum VideoGenerationStatus
    {
        /// <summary>
        /// Task has been submitted and is waiting to be processed
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// Task is currently being processed
        /// </summary>
        Processing = 1,
        
        /// <summary>
        /// Task completed successfully
        /// </summary>
        Completed = 2,
        
        /// <summary>
        /// Task failed with an error
        /// </summary>
        Failed = 3,
        
        /// <summary>
        /// Task was cancelled by user or system
        /// </summary>
        Cancelled = 4
    }

    /// <summary>
    /// Represents the status of an image generation task
    /// </summary>
    public enum ImageGenerationStatus
    {
        /// <summary>
        /// Task has been submitted and is waiting to be processed
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// Task is currently being processed
        /// </summary>
        Processing = 1,
        
        /// <summary>
        /// Task completed successfully
        /// </summary>
        Completed = 2,
        
        /// <summary>
        /// Task failed with an error
        /// </summary>
        Failed = 3,
        
        /// <summary>
        /// Task was cancelled by user or system
        /// </summary>
        Cancelled = 4
    }


    /// <summary>
    /// Represents the delivery status of a webhook
    /// </summary>
    public enum WebhookDeliveryStatus
    {
        /// <summary>
        /// Webhook delivery is pending
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// Webhook was delivered successfully
        /// </summary>
        Delivered = 1,
        
        /// <summary>
        /// Webhook delivery failed
        /// </summary>
        Failed = 2,
        
        /// <summary>
        /// Webhook delivery was retried
        /// </summary>
        Retrying = 3,
        
        /// <summary>
        /// Webhook delivery abandoned after max retries
        /// </summary>
        Abandoned = 4
    }
}