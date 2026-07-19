using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Hubs;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Implementation of ISystemNotificationService that uses SignalR hub context.
    /// Inherits from SignalRNotificationServiceBase for common functionality.
    /// </summary>
    public class SystemNotificationService
        : SignalRNotificationServiceBase<SystemNotificationHub>,
          ISystemNotificationService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SystemNotificationService"/> class.
        /// </summary>
        public SystemNotificationService(
            IHubContext<SystemNotificationHub> hubContext,
            ILogger<SystemNotificationService> logger)
            : base(hubContext, logger)
        {
        }

        /// <inheritdoc />
        public async Task NotifyRateLimitWarning(int remaining, DateTime resetTime, string endpoint)
        {
            await ExecuteWithThrowAsync(
                async () => await HubContext.Clients.All.SendAsync("RateLimitWarning", remaining, resetTime, endpoint),
                nameof(NotifyRateLimitWarning),
                "all clients");

            Logger.LogInformation(
                "Sent rate limit warning: {Remaining} requests remaining for {Endpoint}, resets at {ResetTime}",
                remaining,
                endpoint,
                resetTime);
        }

        /// <inheritdoc />
        public async Task NotifySystemAnnouncement(string message, object priority)
        {
            await ExecuteWithThrowAsync(
                async () => await HubContext.Clients.All.SendAsync("SystemAnnouncement", message, priority.ToString()),
                nameof(NotifySystemAnnouncement),
                "all clients");

            Logger.LogInformation(
                "Sent system announcement with {Priority} priority: {Message}",
                priority,
                message);
        }

        /// <inheritdoc />
        public async Task NotifyServiceDegraded(string service, string reason)
        {
            await ExecuteWithThrowAsync(
                async () => await HubContext.Clients.All.SendAsync("ServiceDegraded", service, reason),
                nameof(NotifyServiceDegraded),
                "all clients");

            Logger.LogWarning(
                "Sent service degradation notification: {Service} is degraded - {Reason}",
                service,
                reason);
        }

        /// <inheritdoc />
        public async Task NotifyServiceRestored(string service)
        {
            await ExecuteWithThrowAsync(
                async () => await HubContext.Clients.All.SendAsync("ServiceRestored", service),
                nameof(NotifyServiceRestored),
                "all clients");

            Logger.LogInformation(
                "Sent service restoration notification: {Service} has been restored",
                service);
        }

        /// <inheritdoc />
        public async Task NotifyConfigurationChangedAsync(int virtualKeyId, string configurationType, List<string> changedProperties)
        {
            await ExecuteWithThrowAsync(
                async () => await HubContext.Clients.All.SendAsync("ConfigurationChanged", virtualKeyId, configurationType, changedProperties),
                nameof(NotifyConfigurationChangedAsync),
                "all clients");

            Logger.LogInformation(
                "Sent configuration change notification for VirtualKey {VirtualKeyId}: {ConfigurationType} - {Changes}",
                virtualKeyId,
                configurationType,
                string.Join(", ", changedProperties));
        }
    }
}