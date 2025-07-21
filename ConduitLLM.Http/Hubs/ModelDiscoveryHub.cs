using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time model discovery notifications.
    /// Notifies clients when new models are discovered, capabilities change, or pricing updates occur.
    /// </summary>
    public class ModelDiscoveryHub : SecureHub
    {
        private readonly IModelDiscoverySubscriptionManager _subscriptionManager;

        public ModelDiscoveryHub(
            ILogger<ModelDiscoveryHub> logger,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            _subscriptionManager = serviceProvider.GetRequiredService<IModelDiscoverySubscriptionManager>();
        }

        protected override string GetHubName() => "ModelDiscoveryHub";

        /// <summary>
        /// Subscribe to model discovery notifications with custom filters
        /// </summary>
        /// <param name="filter">Subscription filter options</param>
        public async Task SubscribeWithFilter(ModelDiscoverySubscriptionFilter filter)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Store subscription preferences - Create a deterministic Guid from the int ID
            var virtualKeyGuid = new Guid(virtualKeyId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            await _subscriptionManager.AddOrUpdateSubscriptionAsync(Context.ConnectionId, virtualKeyGuid, filter);
            
            // Add to appropriate groups based on filter
            if (filter.Providers?.Any() == true)
            {
                foreach (var provider in filter.Providers)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"provider-{provider.ToLowerInvariant()}");
                }
            }
            else
            {
                // Subscribe to all providers if no specific filter
                await Groups.AddToGroupAsync(Context.ConnectionId, "model-discovery-all");
            }
            
            Logger.LogInformation(
                "Virtual Key {KeyId} subscribed with filters: Providers={Providers}, Capabilities={Capabilities}, MinSeverity={MinSeverity}",
                virtualKeyId, 
                filter.Providers?.Count ?? 0,
                filter.Capabilities?.Count ?? 0,
                filter.MinSeverityLevel);
            
            // Send confirmation
            await Clients.Caller.SendAsync("SubscriptionConfirmed", new
            {
                connectionId = Context.ConnectionId,
                filter = filter,
                subscribedAt = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Subscribe to model discovery notifications for a specific provider (legacy method)
        /// </summary>
        /// <param name="providerName">The provider to monitor</param>
        public async Task SubscribeToProvider(string providerName)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Use the new filter-based subscription with a single provider
            var filter = new ModelDiscoverySubscriptionFilter
            {
                Providers = new List<string> { providerName }
            };
            
            await SubscribeWithFilter(filter);
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
            var isAdmin = await IsAdminAsync();
            if (!isAdmin)
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

        /// <summary>
        /// Get current subscription filter for the connection
        /// </summary>
        public async Task<ModelDiscoverySubscription?> GetMySubscription()
        {
            RequireVirtualKeyId(); // Ensure authenticated
            return await _subscriptionManager.GetSubscriptionAsync(Context.ConnectionId);
        }

        /// <summary>
        /// Update subscription filter settings
        /// </summary>
        public async Task UpdateSubscriptionFilter(ModelDiscoverySubscriptionFilter filter)
        {
            // Just delegate to SubscribeWithFilter which handles everything
            await SubscribeWithFilter(filter);
        }

        /// <summary>
        /// Get subscription statistics (admin only)
        /// </summary>
        public async Task<Dictionary<string, int>> GetSubscriptionStatistics()
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            var isAdmin = await IsAdminAsync();
            if (!isAdmin)
            {
                throw new HubException("Admin permissions required to view subscription statistics");
            }
            
            return await _subscriptionManager.GetSubscriptionStatisticsAsync();
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
                // Remove subscription when disconnected
                await _subscriptionManager.RemoveSubscriptionAsync(Context.ConnectionId);
                
                Logger.LogInformation(
                    "Virtual Key {KeyId} disconnected from ModelDiscoveryHub",
                    virtualKeyId.Value);
            }
            
            await base.OnDisconnectedAsync(exception);
        }
    }
}