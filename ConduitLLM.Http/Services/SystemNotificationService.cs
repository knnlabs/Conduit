using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending system notifications through SignalR.
    /// This service is used by other components to broadcast notifications.
    /// </summary>
    public interface ISystemNotificationService
    {
        /// <summary>
        /// Sends a provider health notification to all connected clients.
        /// </summary>
        /// <param name="provider">The provider name.</param>
        /// <param name="status">The health status.</param>
        /// <param name="responseTime">The response time if available.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NotifyProviderHealthChanged(string provider, HealthStatus status, TimeSpan? responseTime);

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
        Task NotifySystemAnnouncement(string message, NotificationPriority priority);

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
    }

    /// <summary>
    /// Implementation of ISystemNotificationService that uses SignalR hub context.
    /// </summary>
    public class SystemNotificationService : ISystemNotificationService
    {
        private readonly IHubContext<SystemNotificationHub> _hubContext;
        private readonly ILogger<SystemNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNotificationService"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public SystemNotificationService(
            IHubContext<SystemNotificationHub> hubContext,
            ILogger<SystemNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task NotifyProviderHealthChanged(string provider, HealthStatus status, TimeSpan? responseTime)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ProviderHealthChanged", provider, status.ToString(), responseTime);
                
                _logger.LogInformation(
                    "Sent provider health notification: {Provider} is {Status} (ResponseTime: {ResponseTime}ms)",
                    provider,
                    status,
                    responseTime?.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending provider health notification");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task NotifyRateLimitWarning(int remaining, DateTime resetTime, string endpoint)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("RateLimitWarning", remaining, resetTime, endpoint);
                
                _logger.LogInformation(
                    "Sent rate limit warning: {Remaining} requests remaining for {Endpoint}, resets at {ResetTime}",
                    remaining,
                    endpoint,
                    resetTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending rate limit warning");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task NotifySystemAnnouncement(string message, NotificationPriority priority)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("SystemAnnouncement", message, priority.ToString());
                
                _logger.LogInformation(
                    "Sent system announcement with {Priority} priority: {Message}",
                    priority,
                    message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system announcement");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task NotifyServiceDegraded(string service, string reason)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ServiceDegraded", service, reason);
                
                _logger.LogWarning(
                    "Sent service degradation notification: {Service} is degraded - {Reason}",
                    service,
                    reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending service degradation notification");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task NotifyServiceRestored(string service)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ServiceRestored", service);
                
                _logger.LogInformation(
                    "Sent service restoration notification: {Service} has been restored",
                    service);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending service restoration notification");
                throw;
            }
        }
    }
}