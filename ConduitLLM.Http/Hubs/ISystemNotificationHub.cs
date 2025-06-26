using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Interface for the SystemNotificationHub that broadcasts system-wide notifications.
    /// </summary>
    public interface ISystemNotificationHub
    {
        /// <summary>
        /// Notifies clients about provider health status changes.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="status">The health status.</param>
        /// <param name="responseTime">The response time if available.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProviderHealthChanged(string provider, HealthStatus status, TimeSpan? responseTime);

        /// <summary>
        /// Sends rate limit warnings to connected clients.
        /// </summary>
        /// <param name="remaining">Number of requests remaining.</param>
        /// <param name="resetTime">When the rate limit resets.</param>
        /// <param name="endpoint">The affected endpoint.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RateLimitWarning(int remaining, DateTime resetTime, string endpoint);

        /// <summary>
        /// Broadcasts system announcements.
        /// </summary>
        /// <param name="message">The announcement message.</param>
        /// <param name="priority">The notification priority.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SystemAnnouncement(string message, NotificationPriority priority);

        /// <summary>
        /// Notifies about service degradation.
        /// </summary>
        /// <param name="service">The degraded service.</param>
        /// <param name="reason">The reason for degradation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ServiceDegraded(string service, string reason);

        /// <summary>
        /// Notifies about service restoration.
        /// </summary>
        /// <param name="service">The restored service.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ServiceRestored(string service);
    }

    /// <summary>
    /// Health status enumeration for provider health notifications.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// Service is healthy and responding normally.
        /// </summary>
        Healthy,

        /// <summary>
        /// Service is experiencing degraded performance.
        /// </summary>
        Degraded,

        /// <summary>
        /// Service is unhealthy or not responding.
        /// </summary>
        Unhealthy
    }

    /// <summary>
    /// Priority levels for system notifications.
    /// </summary>
    public enum NotificationPriority
    {
        /// <summary>
        /// Low priority notification.
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority notification.
        /// </summary>
        Medium,

        /// <summary>
        /// High priority notification.
        /// </summary>
        High,

        /// <summary>
        /// Critical priority notification requiring immediate attention.
        /// </summary>
        Critical
    }
}