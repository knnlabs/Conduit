using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Http.Hubs;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending real-time virtual key management notifications via SignalR.
    /// </summary>
    public interface IVirtualKeyManagementNotificationService
    {
        /// <summary>
        /// Notifies about a virtual key creation.
        /// </summary>
        Task NotifyKeyCreatedAsync(VirtualKeyCreatedNotification notification);
        
        /// <summary>
        /// Notifies about a virtual key update.
        /// </summary>
        Task NotifyKeyUpdatedAsync(int virtualKeyId, VirtualKeyUpdatedNotification notification);
        
        /// <summary>
        /// Notifies about a virtual key deletion.
        /// </summary>
        Task NotifyKeyDeletedAsync(int virtualKeyId, VirtualKeyDeletedNotification notification);
        
        /// <summary>
        /// Notifies about a virtual key status change.
        /// </summary>
        Task NotifyKeyStatusChangedAsync(int virtualKeyId, VirtualKeyStatusChangedNotification notification);
    }

    /// <summary>
    /// Implementation of virtual key management notification service using SignalR.
    /// </summary>
    public class VirtualKeyManagementNotificationService : IVirtualKeyManagementNotificationService
    {
        private readonly IHubContext<VirtualKeyManagementHub> _hubContext;
        private readonly ILogger<VirtualKeyManagementNotificationService> _logger;

        public VirtualKeyManagementNotificationService(
            IHubContext<VirtualKeyManagementHub> hubContext,
            ILogger<VirtualKeyManagementNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NotifyKeyCreatedAsync(VirtualKeyCreatedNotification notification)
        {
            try
            {
                // Notify admin group about new key creation
                await _hubContext.Clients.Group("admin").SendAsync("VirtualKeyCreated", notification);
                
                _logger.LogInformation(
                    "Sent VirtualKeyCreated notification for key {KeyName} (ID: {KeyId})",
                    notification.KeyName,
                    notification.KeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VirtualKeyCreated notification for key {KeyId}", notification.KeyId);
            }
        }

        public async Task NotifyKeyUpdatedAsync(int virtualKeyId, VirtualKeyUpdatedNotification notification)
        {
            try
            {
                // Notify the key's own group
                await _hubContext.Clients.Group($"vkey-{virtualKeyId}").SendAsync("VirtualKeyUpdated", notification);
                
                // Notify management subscribers
                await _hubContext.Clients.Group($"vkey-mgmt-{virtualKeyId}").SendAsync("VirtualKeyUpdated", notification);
                
                // Notify admin group
                await _hubContext.Clients.Group("admin").SendAsync("VirtualKeyUpdated", notification);
                
                _logger.LogInformation(
                    "Sent VirtualKeyUpdated notification for key {KeyId}: {UpdatedProperties}",
                    virtualKeyId,
                    string.Join(", ", notification.UpdatedProperties));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VirtualKeyUpdated notification for key {KeyId}", virtualKeyId);
            }
        }

        public async Task NotifyKeyDeletedAsync(int virtualKeyId, VirtualKeyDeletedNotification notification)
        {
            try
            {
                // Notify the key's own group
                await _hubContext.Clients.Group($"vkey-{virtualKeyId}").SendAsync("VirtualKeyDeleted", notification);
                
                // Notify management subscribers
                await _hubContext.Clients.Group($"vkey-mgmt-{virtualKeyId}").SendAsync("VirtualKeyDeleted", notification);
                
                // Notify admin group
                await _hubContext.Clients.Group("admin").SendAsync("VirtualKeyDeleted", notification);
                
                _logger.LogInformation(
                    "Sent VirtualKeyDeleted notification for key {KeyName} (ID: {KeyId})",
                    notification.KeyName,
                    virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VirtualKeyDeleted notification for key {KeyId}", virtualKeyId);
            }
        }

        public async Task NotifyKeyStatusChangedAsync(int virtualKeyId, VirtualKeyStatusChangedNotification notification)
        {
            try
            {
                // Notify the key's own group
                await _hubContext.Clients.Group($"vkey-{virtualKeyId}").SendAsync("VirtualKeyStatusChanged", notification);
                
                // Notify management subscribers
                await _hubContext.Clients.Group($"vkey-mgmt-{virtualKeyId}").SendAsync("VirtualKeyStatusChanged", notification);
                
                // Notify admin group if it's a critical status change
                if (notification.NewStatus == "disabled" || notification.NewStatus == "suspended")
                {
                    await _hubContext.Clients.Group("admin").SendAsync("VirtualKeyStatusChanged", notification);
                }
                
                _logger.LogInformation(
                    "Sent VirtualKeyStatusChanged notification for key {KeyId}: {PreviousStatus} -> {NewStatus}",
                    virtualKeyId,
                    notification.PreviousStatus,
                    notification.NewStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send VirtualKeyStatusChanged notification for key {KeyId}", virtualKeyId);
            }
        }
    }
}