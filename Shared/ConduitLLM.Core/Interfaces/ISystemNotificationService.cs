namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for sending system-wide operational notifications.
    /// This service handles rate limits, service degradation, and system announcements.
    /// </summary>
    public interface ISystemNotificationService
    {
        /// <summary>
        /// Sends a rate limit warning to all connected clients.
        /// </summary>
        /// <param name="remaining">Number of requests remaining.</param>
        /// <param name="resetTime">When the rate limit resets.</param>
        /// <param name="endpoint">The affected endpoint.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyRateLimitWarning(int remaining, DateTime resetTime, string endpoint);

        /// <summary>
        /// Sends a system announcement to all connected clients.
        /// </summary>
        /// <param name="message">The announcement message.</param>
        /// <param name="priority">The notification priority.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifySystemAnnouncement(string message, object priority);

        /// <summary>
        /// Notifies about service degradation.
        /// </summary>
        /// <param name="service">The degraded service.</param>
        /// <param name="reason">The reason for degradation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyServiceDegraded(string service, string reason);

        /// <summary>
        /// Notifies about service restoration.
        /// </summary>
        /// <param name="service">The restored service.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyServiceRestored(string service);

        /// <summary>
        /// Notifies about configuration changes for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID.</param>
        /// <param name="configurationType">Type of configuration changed.</param>
        /// <param name="changedProperties">List of changed properties.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyConfigurationChangedAsync(int virtualKeyId, string configurationType, List<string> changedProperties);
    }
}