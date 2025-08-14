using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Admin.Hubs
{
    /// <summary>
    /// Service for sending administrative notifications through SignalR.
    /// This service is used by other admin components to broadcast notifications.
    /// </summary>
    public class AdminNotificationService
    {
        private readonly IHubContext<AdminNotificationHub> _hubContext;
        private readonly ILogger<AdminNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminNotificationService"/> class.
        /// </summary>
        /// <param name="hubContext">The SignalR hub context.</param>
        /// <param name="logger">The logger instance.</param>
        public AdminNotificationService(
            IHubContext<AdminNotificationHub> hubContext,
            ILogger<AdminNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Sends a virtual key update notification to subscribed admins.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID that was updated.</param>
        /// <param name="updateType">The type of update (created, updated, deleted).</param>
        /// <param name="details">Additional details about the update.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotifyVirtualKeyUpdate(int virtualKeyId, string updateType, object? details = null)
        {
            try
            {
                var notification = new VirtualKeyNotification
                {
                    VirtualKeyId = virtualKeyId,
                    UpdateType = updateType,
                    Details = details,
                    Priority = updateType == "deleted" ? NotificationPriority.High : NotificationPriority.Medium
                };

                // Send to all admins and those subscribed to this specific virtual key
                await _hubContext.Clients.Groups("admin", $"admin-vkey-{virtualKeyId}")
                    .SendAsync("VirtualKeyUpdate", notification);

                _logger.LogInformation(
                    "[SignalR:VirtualKeyUpdate] Sent notification - VirtualKey: {VirtualKeyId}, UpdateType: {UpdateType}, Groups: [admin, admin-vkey-{VirtualKeyId}], Details: {@Details}",
                    virtualKeyId, updateType, virtualKeyId, details);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending virtual key update notification");
                throw;
            }
        }


        /// <summary>
        /// Sends a high spend alert notification to all admins.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key with high spend.</param>
        /// <param name="currentSpend">The current spend amount.</param>
        /// <param name="budget">The budget limit.</param>
        /// <param name="percentageUsed">The percentage of budget used.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotifyHighSpendAlert(int virtualKeyId, decimal currentSpend, decimal budget, double percentageUsed)
        {
            try
            {
                var notification = new BudgetAlertNotification
                {
                    AlertType = percentageUsed >= 100 ? "BudgetExceeded" : "HighSpend",
                    Message = $"Virtual Key {virtualKeyId} has used {percentageUsed:F1}% of its budget",
                    CurrentSpend = currentSpend,
                    BudgetLimit = budget,
                    PercentageUsed = percentageUsed,
                    Severity = percentageUsed >= 100 ? "critical" : "warning",
                    Recommendations = new()
                    {
                        "Review recent usage patterns",
                        "Consider increasing budget if legitimate",
                        "Check for unusual activity"
                    }
                };

                // High spend alerts go to all admins and virtual key subscribers
                await _hubContext.Clients.Groups("admin", $"admin-vkey-{virtualKeyId}")
                    .SendAsync("HighSpendAlert", notification);

                _logger.LogWarning(
                    "[SignalR:HighSpendAlert] Sent notification - VirtualKey: {VirtualKeyId}, CurrentSpend: ${CurrentSpend:F2}, Budget: ${Budget:F2}, PercentageUsed: {PercentageUsed:F1}%, AlertType: {AlertType}, Groups: [admin, admin-vkey-{VirtualKeyId}]",
                    virtualKeyId, currentSpend, budget, percentageUsed, notification.AlertType, virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending high spend alert");
                throw;
            }
        }

        /// <summary>
        /// Sends a system announcement to all admin clients.
        /// </summary>
        /// <param name="message">The announcement message.</param>
        /// <param name="priority">The notification priority.</param>
        /// <param name="category">The announcement category.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotifySystemAnnouncement(string message, NotificationPriority priority, string category = "general")
        {
            try
            {
                var notification = new SystemAnnouncementNotification
                {
                    Message = message,
                    Priority = priority,
                    Category = category
                };

                await _hubContext.Clients.Group("admin").SendAsync("SystemAnnouncement", notification);

                _logger.LogInformation(
                    "Sent system announcement with {Priority} priority: {Message}",
                    priority, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending system announcement");
                throw;
            }
        }

        /// <summary>
        /// Sends a security alert to all admin clients.
        /// </summary>
        /// <param name="alertType">The type of security alert.</param>
        /// <param name="description">Description of the security event.</param>
        /// <param name="affectedResource">The affected resource if applicable.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotifySecurityAlert(string alertType, string description, string? affectedResource = null)
        {
            try
            {
                var notification = new SecurityAlertNotification
                {
                    AlertType = alertType,
                    Description = description,
                    AffectedResource = affectedResource,
                    Priority = NotificationPriority.Critical
                };

                await _hubContext.Clients.Group("admin").SendAsync("SecurityAlert", notification);

                _logger.LogWarning(
                    "Sent security alert: {AlertType} - {Description} (Resource: {AffectedResource})",
                    alertType, description, affectedResource ?? "N/A");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending security alert");
                throw;
            }
        }

        /// <summary>
        /// Sends a model capability update notification.
        /// </summary>
        /// <param name="providerId">The provider ID.</param>
        /// <param name="providerName">The provider name.</param>
        /// <param name="modelCount">Number of models discovered.</param>
        /// <param name="changeDescription">Description of what changed.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task NotifyModelCapabilityUpdate(int providerId, string providerName, int modelCount, string changeDescription)
        {
            try
            {
                var notification = new ModelCapabilitiesNotification
                {
                    ProviderId = providerId,
                    ProviderName = providerName,
                    ModelCount = modelCount,
                    Priority = NotificationPriority.Low,
                    Details = changeDescription
                };

                // Send to all admins and provider subscribers
                await _hubContext.Clients.Groups("admin", $"admin-provider-{providerId}")
                    .SendAsync("ModelCapabilityUpdate", notification);

                _logger.LogInformation(
                    "Sent model capability update: {ProviderId} ({ProviderName}) - {ModelCount} models ({ChangeDescription})",
                    providerId, providerName, modelCount, changeDescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending model capability update");
                throw;
            }
        }
    }
}