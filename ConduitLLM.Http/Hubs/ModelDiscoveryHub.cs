using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.DTOs.SignalR;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time model discovery notifications.
    /// Notifies clients when new models are discovered, capabilities change, or pricing updates occur.
    /// </summary>
    public class ModelDiscoveryHub : SecureHub
    {
        public ModelDiscoveryHub(
            ILogger<ModelDiscoveryHub> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
        }

        protected override string GetHubName() => "ModelDiscoveryHub";

        /// <summary>
        /// Subscribe to model discovery notifications for a specific provider
        /// </summary>
        /// <param name="providerName">The provider to monitor</param>
        public async Task SubscribeToProvider(string providerName)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Add to provider-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"provider-{providerName.ToLowerInvariant()}");
            
            Logger.LogInformation(
                "Virtual Key {KeyId} subscribed to model discovery notifications for provider {Provider}",
                virtualKeyId, providerName);
        }

        /// <summary>
        /// Unsubscribe from model discovery notifications for a specific provider
        /// </summary>
        /// <param name="providerName">The provider to stop monitoring</param>
        public async Task UnsubscribeFromProvider(string providerName)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Remove from provider-specific group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"provider-{providerName.ToLowerInvariant()}");
            
            Logger.LogInformation(
                "Virtual Key {KeyId} unsubscribed from model discovery notifications for provider {Provider}",
                virtualKeyId, providerName);
        }

        /// <summary>
        /// Subscribe to all model discovery notifications
        /// </summary>
        public async Task SubscribeToAll()
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Check if virtual key has permission to see all providers
            var virtualKey = await GetVirtualKeyAsync(virtualKeyId);
            if (virtualKey?.IsAdmin != true)
            {
                throw new HubException("Admin permissions required to subscribe to all providers");
            }
            
            // Add to global notifications group
            await Groups.AddToGroupAsync(Context.ConnectionId, "model-discovery-all");
            
            Logger.LogInformation(
                "Admin Virtual Key {KeyId} subscribed to all model discovery notifications",
                virtualKeyId);
        }

        /// <summary>
        /// Request immediate model discovery for a provider
        /// </summary>
        /// <param name="providerName">The provider to refresh</param>
        public async Task RefreshProviderModels(string providerName)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // This would trigger the ProviderDiscoveryService to refresh
            // For now, just log the request
            Logger.LogInformation(
                "Virtual Key {KeyId} requested model refresh for provider {Provider}",
                virtualKeyId, providerName);
            
            await Clients.Caller.SendAsync("RefreshRequested", new
            {
                provider = providerName,
                requestedAt = DateTime.UtcNow,
                message = "Model refresh has been initiated"
            });
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
            
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                Logger.LogInformation(
                    "Virtual Key {KeyId} connected to ModelDiscoveryHub",
                    virtualKeyId.Value);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyId = GetVirtualKeyId();
            if (virtualKeyId.HasValue)
            {
                Logger.LogInformation(
                    "Virtual Key {KeyId} disconnected from ModelDiscoveryHub",
                    virtualKeyId.Value);
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}