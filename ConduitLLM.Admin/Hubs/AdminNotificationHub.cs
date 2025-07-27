using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Admin.Interfaces;

namespace ConduitLLM.Admin.Hubs
{
    /// <summary>
    /// SignalR hub for administrative notifications requiring master key authentication.
    /// This hub allows administrators to receive real-time notifications about system events,
    /// virtual key changes, provider health updates, and other administrative concerns.
    /// </summary>
    [Authorize(Policy = "MasterKeyPolicy")]
    public class AdminNotificationHub : Hub, IAdminNotificationHub
    {
        private readonly ILogger<AdminNotificationHub> _logger;
        private readonly IAdminProviderHealthService _providerHealthService;
        private readonly IAdminVirtualKeyService _virtualKeyService;
        private readonly IAdminNotificationService _notificationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminNotificationHub"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="providerHealthService">Provider health service for status queries.</param>
        /// <param name="virtualKeyService">Virtual key service for key management notifications.</param>
        /// <param name="notificationService">Notification service for administrative alerts.</param>
        public AdminNotificationHub(
            ILogger<AdminNotificationHub> logger,
            IAdminProviderHealthService providerHealthService,
            IAdminVirtualKeyService virtualKeyService,
            IAdminNotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerHealthService = providerHealthService ?? throw new ArgumentNullException(nameof(providerHealthService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Called when a client connects to the hub.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Admin client connected to AdminNotificationHub: {ConnectionId}", Context.ConnectionId);
            
            // Add to admin group for receiving broadcast notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
            
            // Send initial provider health status
            await SendInitialProviderHealthStatus();
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub.
        /// </summary>
        /// <param name="exception">The exception that caused the disconnect, if any.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Admin client disconnected from AdminNotificationHub: {ConnectionId}", Context.ConnectionId);
            
            // Remove from admin group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin");
            
            if (exception != null)
            {
                _logger.LogError(exception, "Admin client disconnected due to error");
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribes to notifications for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToVirtualKey(int virtualKeyId)
        {
            try
            {
                // Verify the virtual key exists
                var virtualKey = await _virtualKeyService.GetVirtualKeyInfoAsync(virtualKeyId);
                if (virtualKey == null)
                {
                    await Clients.Caller.SendAsync("Error", new { message = $"Virtual key {virtualKeyId} not found" });
                    return;
                }
                
                var groupName = $"admin-vkey-{virtualKeyId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin subscribed to virtual key {VirtualKeyId} notifications", virtualKeyId);
                
                await Clients.Caller.SendAsync("SubscribedToVirtualKey", virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to virtual key {VirtualKeyId}", virtualKeyId);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to subscribe to virtual key notifications" });
            }
        }

        /// <summary>
        /// Unsubscribes from notifications for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to unsubscribe from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UnsubscribeFromVirtualKey(int virtualKeyId)
        {
            try
            {
                var groupName = $"admin-vkey-{virtualKeyId}";
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin unsubscribed from virtual key {VirtualKeyId} notifications", virtualKeyId);
                
                await Clients.Caller.SendAsync("UnsubscribedFromVirtualKey", virtualKeyId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from virtual key {VirtualKeyId}", virtualKeyId);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to unsubscribe from virtual key notifications" });
            }
        }

        /// <summary>
        /// Subscribes to notifications for a specific provider.
        /// </summary>
        /// <param name="providerName">The provider name to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToProvider(string providerName)
        {
            try
            {
                // Parse provider name to ProviderType
                if (!Enum.TryParse<ProviderType>(providerName, true, out var providerType))
                {
                    _logger.LogWarning("Unknown provider type: {Provider}", providerName);
                    await Clients.Caller.SendAsync("SubscriptionError", $"Unknown provider: {providerName}");
                    return;
                }
                
                var groupName = $"admin-provider-{providerName}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin subscribed to provider {ProviderName} notifications", providerName);
                
                // Send current provider health status
                var healthStatus = await _providerHealthService.GetLatestStatusAsync(providerType);
                if (healthStatus != null)
                {
                    await Clients.Caller.SendAsync("ProviderHealthStatus", healthStatus);
                }
                
                await Clients.Caller.SendAsync("SubscribedToProvider", providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to provider {ProviderName}", providerName);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to subscribe to provider notifications" });
            }
        }

        /// <summary>
        /// Unsubscribes from notifications for a specific provider.
        /// </summary>
        /// <param name="providerName">The provider name to unsubscribe from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UnsubscribeFromProvider(string providerName)
        {
            try
            {
                var groupName = $"admin-provider-{providerName}";
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin unsubscribed from provider {ProviderName} notifications", providerName);
                
                await Clients.Caller.SendAsync("UnsubscribedFromProvider", providerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from provider {ProviderName}", providerName);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to unsubscribe from provider notifications" });
            }
        }

        /// <summary>
        /// Requests a refresh of provider health status.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task RefreshProviderHealth()
        {
            try
            {
                _logger.LogInformation("Admin requested provider health refresh");
                
                // Get all provider health statuses
                var healthStatuses = await _providerHealthService.GetAllLatestStatusesAsync();
                
                await Clients.Caller.SendAsync("ProviderHealthRefreshed", healthStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing provider health");
                await Clients.Caller.SendAsync("Error", new { message = "Failed to refresh provider health" });
            }
        }

        /// <summary>
        /// Sends the initial provider health status to the connected client.
        /// </summary>
        private async Task SendInitialProviderHealthStatus()
        {
            try
            {
                var healthStatuses = await _providerHealthService.GetAllLatestStatusesAsync();
                await Clients.Caller.SendAsync("InitialProviderHealth", healthStatuses);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending initial provider health status");
            }
        }
    }

    /// <summary>
    /// Interface defining the client methods for the AdminNotificationHub.
    /// </summary>
    public interface IAdminNotificationHub
    {
        /// <summary>
        /// Subscribes to notifications for a specific virtual key.
        /// </summary>
        Task SubscribeToVirtualKey(int virtualKeyId);

        /// <summary>
        /// Unsubscribes from notifications for a specific virtual key.
        /// </summary>
        Task UnsubscribeFromVirtualKey(int virtualKeyId);

        /// <summary>
        /// Subscribes to notifications for a specific provider.
        /// </summary>
        Task SubscribeToProvider(string providerName);

        /// <summary>
        /// Unsubscribes from notifications for a specific provider.
        /// </summary>
        Task UnsubscribeFromProvider(string providerName);

        /// <summary>
        /// Requests a refresh of provider health status.
        /// </summary>
        Task RefreshProviderHealth();
    }
}