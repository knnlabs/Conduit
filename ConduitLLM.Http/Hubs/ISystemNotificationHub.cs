using System;
using System.Threading.Tasks;
using ConduitLLM.Configuration.DTOs.SignalR;

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
        /// <param name="providerId">The provider ID.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="status">The health status.</param>
        /// <param name="responseTime">The response time if available.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ProviderHealthChanged(int providerId, string providerName, HealthStatus status, TimeSpan? responseTime);

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

        /// <summary>
        /// Notifies clients of a model mapping change.
        /// </summary>
        /// <param name="mappingId">The mapping ID.</param>
        /// <param name="modelAlias">The model alias.</param>
        /// <param name="changeType">The type of change (Created, Updated, Deleted).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ModelMappingChanged(int mappingId, string modelAlias, string changeType);

        /// <summary>
        /// Notifies clients of model capabilities discovery.
        /// </summary>
        /// <param name="providerId">The provider ID.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="modelCount">Total number of models.</param>
        /// <param name="embeddingCount">Number of embedding models.</param>
        /// <param name="visionCount">Number of vision models.</param>
        /// <param name="imageGenCount">Number of image generation models.</param>
        /// <param name="videoGenCount">Number of video generation models.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ModelCapabilitiesDiscovered(int providerId, string providerName, int modelCount, int embeddingCount = 0, int visionCount = 0, int imageGenCount = 0, int videoGenCount = 0);

        /// <summary>
        /// Notifies clients of model availability change.
        /// </summary>
        /// <param name="modelId">The model identifier.</param>
        /// <param name="isAvailable">Whether the model is available.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ModelAvailabilityChanged(string modelId, bool isAvailable);
    }
}