using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Interfaces
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
}