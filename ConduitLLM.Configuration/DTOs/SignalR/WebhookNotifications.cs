namespace ConduitLLM.Configuration.DTOs.SignalR
{
    /// <summary>
    /// Notification for webhook delivery attempts.
    /// </summary>
    public class WebhookDeliveryAttempt
    {
        /// <summary>
        /// Gets or sets the webhook ID.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delivery ID.
        /// </summary>
        public string DeliveryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the attempt number.
        /// </summary>
        public int AttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the attempt.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the task type.
        /// </summary>
        public string TaskType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Notification for successful webhook delivery.
    /// </summary>
    public class WebhookDeliverySuccess
    {
        /// <summary>
        /// Gets or sets the webhook ID.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delivery ID.
        /// </summary>
        public string DeliveryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the response time in milliseconds.
        /// </summary>
        public double ResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets the number of attempts it took.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of successful delivery.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the total number of attempts.
        /// </summary>
        public int TotalAttempts { get; set; }
    }

    /// <summary>
    /// Notification for failed webhook delivery.
    /// </summary>
    public class WebhookDeliveryFailure
    {
        /// <summary>
        /// Gets or sets the webhook ID.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delivery ID.
        /// </summary>
        public string DeliveryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP status code if available.
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the number of attempts made.
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// Gets or sets whether this is a final failure.
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the failure.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets the task ID.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the attempt number.
        /// </summary>
        public int AttemptNumber { get; set; }
        
        /// <summary>
        /// Gets or sets whether this is a permanent failure.
        /// </summary>
        public bool IsPermanentFailure { get; set; }
    }

    /// <summary>
    /// Notification for webhook retry scheduling.
    /// </summary>
    public class WebhookRetryInfo
    {
        /// <summary>
        /// Gets or sets the webhook ID.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the delivery ID.
        /// </summary>
        public string DeliveryId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the event type.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the next attempt number.
        /// </summary>
        public int NextAttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets when the retry is scheduled.
        /// </summary>
        public DateTime ScheduledAt { get; set; }

        /// <summary>
        /// Gets or sets the delay in seconds.
        /// </summary>
        public double DelaySeconds { get; set; }

        /// <summary>
        /// Gets or sets the reason for retry.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Webhook delivery statistics.
    /// </summary>
    public class WebhookStatistics
    {
        /// <summary>
        /// Gets or sets the time period for these statistics.
        /// </summary>
        public string Period { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total deliveries attempted.
        /// </summary>
        public int TotalDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the successful deliveries.
        /// </summary>
        public int SuccessfulDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the failed deliveries.
        /// </summary>
        public int FailedDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the pending deliveries.
        /// </summary>
        public int PendingDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the average response time in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets statistics per URL.
        /// </summary>
        public List<WebhookUrlStatistics> UrlStatistics { get; set; } = new();

        /// <summary>
        /// Gets or sets the timestamp of the statistics.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics for a specific webhook URL.
    /// </summary>
    public class WebhookUrlStatistics
    {
        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total deliveries to this URL.
        /// </summary>
        public int TotalDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the successful deliveries to this URL.
        /// </summary>
        public int SuccessfulDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the failed deliveries to this URL.
        /// </summary>
        public int FailedDeliveries { get; set; }

        /// <summary>
        /// Gets or sets the success rate for this URL.
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Gets or sets the average response time for this URL.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// Gets or sets whether this URL is currently healthy.
        /// </summary>
        public bool IsHealthy { get; set; }

        /// <summary>
        /// Gets or sets the circuit breaker state for this URL.
        /// </summary>
        public string? CircuitBreakerState { get; set; }
    }

    /// <summary>
    /// Circuit breaker state notification for webhooks.
    /// </summary>
    public class WebhookCircuitBreakerState
    {
        /// <summary>
        /// Gets or sets the webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous state.
        /// </summary>
        public string PreviousState { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current state.
        /// </summary>
        public string CurrentState { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for state change.
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the failure count.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the success count.
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// Gets or sets when the circuit will be tested again (if open).
        /// </summary>
        public DateTime? NextTestAt { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the state change.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}