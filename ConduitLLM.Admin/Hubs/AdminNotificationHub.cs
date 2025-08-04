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
        private readonly IAdminVirtualKeyService _virtualKeyService;
        private readonly IAdminNotificationService _notificationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminNotificationHub"/> class.
        /// </summary>
        /// <param name="logger">Logger instance.</param>
        /// <param name="virtualKeyService">Virtual key service for key management notifications.</param>
        /// <param name="notificationService">Notification service for administrative alerts.</param>
        public AdminNotificationHub(
            ILogger<AdminNotificationHub> logger,
            IAdminVirtualKeyService virtualKeyService,
            IAdminNotificationService notificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        /// Subscribes to notifications for a specific provider by name (legacy).
        /// </summary>
        /// <param name="providerName">The provider name to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Obsolete("Use SubscribeToProvider(int providerId) instead")]
        public async Task SubscribeToProviderByName(string providerName)
        {
            _logger.LogWarning("Legacy SubscribeToProviderByName called with {ProviderName}. Clients should use SubscribeToProvider(int) instead.", providerName);
            await Clients.Caller.SendAsync("Error", new { message = "This method is deprecated. Please use SubscribeToProvider with provider ID." });
        }

        /// <summary>
        /// Subscribes to notifications for a specific provider.
        /// </summary>
        /// <param name="providerId">The provider ID to subscribe to.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task SubscribeToProvider(int providerId)
        {
            try
            {
                var groupName = $"admin-provider-{providerId}";
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin subscribed to provider {ProviderId} notifications", providerId);
                
                // Provider health tracking has been removed
                
                await Clients.Caller.SendAsync("SubscribedToProvider", providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to provider {ProviderId}", providerId);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to subscribe to provider notifications" });
            }
        }

        /// <summary>
        /// Unsubscribes from notifications for a specific provider by name (legacy).
        /// </summary>
        /// <param name="providerName">The provider name to unsubscribe from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Obsolete("Use UnsubscribeFromProvider(int providerId) instead")]
        public async Task UnsubscribeFromProviderByName(string providerName)
        {
            _logger.LogWarning("Legacy UnsubscribeFromProviderByName called with {ProviderName}. Clients should use UnsubscribeFromProvider(int) instead.", providerName);
            await Clients.Caller.SendAsync("Error", new { message = "This method is deprecated. Please use UnsubscribeFromProvider with provider ID." });
        }

        /// <summary>
        /// Unsubscribes from notifications for a specific provider.
        /// </summary>
        /// <param name="providerId">The provider ID to unsubscribe from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UnsubscribeFromProvider(int providerId)
        {
            try
            {
                var groupName = $"admin-provider-{providerId}";
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
                
                _logger.LogInformation("Admin unsubscribed from provider {ProviderId} notifications", providerId);
                
                await Clients.Caller.SendAsync("UnsubscribedFromProvider", providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from provider {ProviderId}", providerId);
                await Clients.Caller.SendAsync("Error", new { message = "Failed to unsubscribe from provider notifications" });
            }
        }

        /// <summary>
        /// Requests a refresh of provider health status (deprecated).
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        [Obsolete("Provider health monitoring has been removed")]
        public async Task RefreshProviderHealth()
        {
            _logger.LogWarning("RefreshProviderHealth called but provider health monitoring has been removed");
            await Clients.Caller.SendAsync("Error", new { message = "Provider health monitoring has been removed" });
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
        Task SubscribeToProvider(int providerId);

        /// <summary>
        /// Unsubscribes from notifications for a specific provider.
        /// </summary>
        Task UnsubscribeFromProvider(int providerId);

    }
}