using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time navigation state updates to replace WebUI polling
    /// </summary>
    public class NavigationStateHub : BaseHub
    {
        /// <summary>
        /// Initializes a new instance of the NavigationStateHub
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public NavigationStateHub(ILogger<NavigationStateHub> logger)
            : base(logger)
        {
        }

        protected override string GetHubName() => "NavigationStateHub";

        /// <summary>
        /// Called when a client successfully connects. Override to implement hub-specific logic.
        /// </summary>
        protected override async Task OnClientConnectedAsync()
        {
            // Add the client to a group for receiving updates
            await AddToGroupAsync("navigation-updates");
        }

        /// <summary>
        /// Called when a client disconnects. Override to implement hub-specific cleanup.
        /// </summary>
        protected override async Task OnClientDisconnectedAsync(Exception? exception)
        {
            // Remove the client from the updates group
            await RemoveFromGroupAsync("navigation-updates");
        }

        /// <summary>
        /// Allows clients to subscribe to specific model updates
        /// </summary>
        /// <param name="modelId">The model ID to subscribe to</param>
        public async Task SubscribeToModel(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                Logger.LogWarning("Client {ConnectionId} attempted to subscribe to empty model ID", Context.ConnectionId);
                return;
            }

            await AddToGroupAsync($"model-{modelId}");
        }

        /// <summary>
        /// Allows clients to unsubscribe from specific model updates
        /// </summary>
        /// <param name="modelId">The model ID to unsubscribe from</param>
        public async Task UnsubscribeFromModel(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            await RemoveFromGroupAsync($"model-{modelId}");
        }

        /// <summary>
        /// Allows clients to request current navigation state
        /// </summary>
        public async Task RequestCurrentState()
        {
            Logger.LogDebug("Client {ConnectionId} requested current navigation state", Context.ConnectionId);
            
            // In a real implementation, this would fetch the current state from services
            // For now, we'll just acknowledge the request
            await Clients.Caller.SendAsync("StateUpdateRequested", new { timestamp = DateTime.UtcNow });
        }
    }
}