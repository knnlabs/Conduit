using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time navigation state updates to replace WebUI polling
    /// </summary>
    public class NavigationStateHub : Hub
    {
        private readonly ILogger<NavigationStateHub> _logger;

        /// <summary>
        /// Initializes a new instance of the NavigationStateHub
        /// </summary>
        /// <param name="logger">Logger instance</param>
        public NavigationStateHub(ILogger<NavigationStateHub> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Called when a client connects to the hub
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected to NavigationStateHub: {ConnectionId}", Context.ConnectionId);
            
            // Add the client to a group for receiving updates
            await Groups.AddToGroupAsync(Context.ConnectionId, "navigation-updates");
            
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when a client disconnects from the hub
        /// </summary>
        /// <param name="exception">Exception that caused the disconnect, if any</param>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "Client disconnected from NavigationStateHub with error: {ConnectionId}", Context.ConnectionId);
            }
            else
            {
                _logger.LogInformation("Client disconnected from NavigationStateHub: {ConnectionId}", Context.ConnectionId);
            }
            
            // Remove the client from the updates group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "navigation-updates");
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Allows clients to subscribe to specific model updates
        /// </summary>
        /// <param name="modelId">The model ID to subscribe to</param>
        public async Task SubscribeToModel(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
            {
                _logger.LogWarning("Client {ConnectionId} attempted to subscribe to empty model ID", Context.ConnectionId);
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"model-{modelId}");
            _logger.LogDebug("Client {ConnectionId} subscribed to model updates: {ModelId}", Context.ConnectionId, modelId);
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

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"model-{modelId}");
            _logger.LogDebug("Client {ConnectionId} unsubscribed from model updates: {ModelId}", Context.ConnectionId, modelId);
        }

        /// <summary>
        /// Allows clients to request current navigation state
        /// </summary>
        public async Task RequestCurrentState()
        {
            _logger.LogDebug("Client {ConnectionId} requested current navigation state", Context.ConnectionId);
            
            // In a real implementation, this would fetch the current state from services
            // For now, we'll just acknowledge the request
            await Clients.Caller.SendAsync("StateUpdateRequested", new { timestamp = DateTime.UtcNow });
        }
    }
}