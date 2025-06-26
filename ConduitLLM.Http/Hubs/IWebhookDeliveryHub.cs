using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Interface for the WebhookDeliveryHub that provides real-time webhook delivery tracking.
    /// </summary>
    public interface IWebhookDeliveryHub
    {
        /// <summary>
        /// Notifies when a webhook delivery attempt is made.
        /// </summary>
        /// <param name="attempt">The delivery attempt details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeliveryAttempted(WebhookDeliveryAttempt attempt);

        /// <summary>
        /// Notifies when a webhook is successfully delivered.
        /// </summary>
        /// <param name="success">The successful delivery details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeliverySucceeded(WebhookDeliverySuccess success);

        /// <summary>
        /// Notifies when a webhook delivery fails.
        /// </summary>
        /// <param name="failure">The delivery failure details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeliveryFailed(WebhookDeliveryFailure failure);

        /// <summary>
        /// Notifies when a retry is scheduled for a failed webhook.
        /// </summary>
        /// <param name="retry">The retry scheduling details.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RetryScheduled(WebhookRetryInfo retry);

        /// <summary>
        /// Notifies when webhook delivery statistics are updated.
        /// </summary>
        /// <param name="stats">The updated statistics.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeliveryStatisticsUpdated(WebhookStatistics stats);

        /// <summary>
        /// Notifies when circuit breaker state changes for a webhook endpoint.
        /// </summary>
        /// <param name="circuitBreaker">The circuit breaker state change.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CircuitBreakerStateChanged(WebhookCircuitBreakerState circuitBreaker);
    }

    /// <summary>
    /// Details about a webhook delivery attempt.
    /// </summary>
    public class WebhookDeliveryAttempt
    {
        /// <summary>
        /// Unique webhook identifier.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;
        
        /// <summary>
        /// Task identifier that triggered the webhook.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// Type of task (e.g., "video", "image").
        /// </summary>
        public string TaskType { get; set; } = string.Empty;
        
        /// <summary>
        /// The webhook URL being called.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// When the attempt was made.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Current attempt number (1-based).
        /// </summary>
        public int AttemptNumber { get; set; }
        
        /// <summary>
        /// Type of webhook event.
        /// </summary>
        public string EventType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Details about a successful webhook delivery.
    /// </summary>
    public class WebhookDeliverySuccess
    {
        /// <summary>
        /// Unique webhook identifier.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;
        
        /// <summary>
        /// Task identifier that triggered the webhook.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// The webhook URL that was called.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// When the successful delivery occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// HTTP status code returned.
        /// </summary>
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Response time in milliseconds.
        /// </summary>
        public long ResponseTimeMs { get; set; }
        
        /// <summary>
        /// Total number of attempts made.
        /// </summary>
        public int TotalAttempts { get; set; }
    }

    /// <summary>
    /// Details about a failed webhook delivery.
    /// </summary>
    public class WebhookDeliveryFailure
    {
        /// <summary>
        /// Unique webhook identifier.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;
        
        /// <summary>
        /// Task identifier that triggered the webhook.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// The webhook URL that failed.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// When the failure occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// HTTP status code if available.
        /// </summary>
        public int? StatusCode { get; set; }
        
        /// <summary>
        /// Error message describing the failure.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// When the next retry is scheduled (if any).
        /// </summary>
        public DateTime? NextRetryTime { get; set; }
        
        /// <summary>
        /// Current attempt number.
        /// </summary>
        public int AttemptNumber { get; set; }
        
        /// <summary>
        /// Whether this failure is permanent (no more retries).
        /// </summary>
        public bool IsPermanentFailure { get; set; }
    }

    /// <summary>
    /// Information about a scheduled webhook retry.
    /// </summary>
    public class WebhookRetryInfo
    {
        /// <summary>
        /// Unique webhook identifier.
        /// </summary>
        public string WebhookId { get; set; } = string.Empty;
        
        /// <summary>
        /// Task identifier that triggered the webhook.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;
        
        /// <summary>
        /// The webhook URL to be retried.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// When the retry is scheduled.
        /// </summary>
        public DateTime ScheduledTime { get; set; }
        
        /// <summary>
        /// Which retry attempt this will be.
        /// </summary>
        public int RetryNumber { get; set; }
        
        /// <summary>
        /// Maximum number of retries allowed.
        /// </summary>
        public int MaxRetries { get; set; }
        
        /// <summary>
        /// Delay in seconds until retry.
        /// </summary>
        public double DelaySeconds { get; set; }
    }

    /// <summary>
    /// Webhook delivery statistics.
    /// </summary>
    public class WebhookStatistics
    {
        /// <summary>
        /// Time period for these statistics.
        /// </summary>
        public string Period { get; set; } = "last_hour";
        
        /// <summary>
        /// Total number of deliveries attempted.
        /// </summary>
        public int TotalAttempts { get; set; }
        
        /// <summary>
        /// Number of successful deliveries.
        /// </summary>
        public int SuccessfulDeliveries { get; set; }
        
        /// <summary>
        /// Number of failed deliveries.
        /// </summary>
        public int FailedDeliveries { get; set; }
        
        /// <summary>
        /// Number of deliveries currently being retried.
        /// </summary>
        public int PendingRetries { get; set; }
        
        /// <summary>
        /// Success rate percentage.
        /// </summary>
        public double SuccessRate { get; set; }
        
        /// <summary>
        /// Average response time in milliseconds.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Breakdown by webhook URL.
        /// </summary>
        public Dictionary<string, WebhookUrlStatistics> UrlStatistics { get; set; } = new();
    }

    /// <summary>
    /// Statistics for a specific webhook URL.
    /// </summary>
    public class WebhookUrlStatistics
    {
        /// <summary>
        /// The webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// Total attempts to this URL.
        /// </summary>
        public int TotalAttempts { get; set; }
        
        /// <summary>
        /// Successful deliveries to this URL.
        /// </summary>
        public int Successes { get; set; }
        
        /// <summary>
        /// Failed deliveries to this URL.
        /// </summary>
        public int Failures { get; set; }
        
        /// <summary>
        /// Average response time for this URL.
        /// </summary>
        public double AverageResponseTimeMs { get; set; }
        
        /// <summary>
        /// Circuit breaker state for this URL.
        /// </summary>
        public string CircuitBreakerState { get; set; } = "closed";
    }

    /// <summary>
    /// Circuit breaker state information for a webhook endpoint.
    /// </summary>
    public class WebhookCircuitBreakerState
    {
        /// <summary>
        /// The webhook URL.
        /// </summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>
        /// Current state (open, closed, half-open).
        /// </summary>
        public string State { get; set; } = "closed";
        
        /// <summary>
        /// Previous state.
        /// </summary>
        public string PreviousState { get; set; } = "closed";
        
        /// <summary>
        /// When the state changed.
        /// </summary>
        public DateTime StateChangedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Reason for state change.
        /// </summary>
        public string Reason { get; set; } = string.Empty;
        
        /// <summary>
        /// Failure count that triggered the change.
        /// </summary>
        public int FailureCount { get; set; }
        
        /// <summary>
        /// When the circuit will attempt to close (if open).
        /// </summary>
        public DateTime? NextAttemptAt { get; set; }
    }
}