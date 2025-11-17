using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
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
        public async Task NotifySystemAnnouncement(string message, object priority)
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

        /// <inheritdoc />
        public async Task NotifyConfigurationChangedAsync(int virtualKeyId, string configurationType, System.Collections.Generic.List<string> changedProperties)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("ConfigurationChanged", virtualKeyId, configurationType, changedProperties);
                
                _logger.LogInformation(
                    "Sent configuration change notification for VirtualKey {VirtualKeyId}: {ConfigurationType} - {Changes}",
                    virtualKeyId,
                    configurationType,
                    string.Join(", ", changedProperties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending configuration change notification");
                throw;
            }
        }
    }
}