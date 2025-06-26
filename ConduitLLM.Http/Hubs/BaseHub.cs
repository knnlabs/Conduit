using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base class for all SignalR hubs that provides common functionality.
    /// This base class is for hubs that do not require authentication.
    /// </summary>
    public abstract class BaseHub : Hub
    {
        protected readonly ILogger Logger;

        protected BaseHub(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            Logger.LogInformation("Client connected to {HubName}: {ConnectionId}", 
                GetHubName(), Context.ConnectionId);
            
            await OnClientConnectedAsync();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                Logger.LogWarning(exception, "Client disconnected from {HubName} with error: {ConnectionId}", 
                    GetHubName(), Context.ConnectionId);
            }
            else
            {
                Logger.LogInformation("Client disconnected from {HubName}: {ConnectionId}", 
                    GetHubName(), Context.ConnectionId);
            }
            
            await OnClientDisconnectedAsync(exception);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called when a client successfully connects. Override to implement hub-specific logic.
        /// </summary>
        protected virtual Task OnClientConnectedAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a client disconnects. Override to implement hub-specific cleanup.
        /// </summary>
        protected virtual Task OnClientDisconnectedAsync(Exception? exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the name of the hub for logging purposes
        /// </summary>
        protected abstract string GetHubName();

        /// <summary>
        /// Adds the current connection to a named group.
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        protected async Task AddToGroupAsync(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Logger.LogDebug("Added connection {ConnectionId} to group {GroupName} in {HubName}", 
                Context.ConnectionId, groupName, GetHubName());
        }

        /// <summary>
        /// Removes the current connection from a named group.
        /// </summary>
        /// <param name="groupName">The name of the group</param>
        protected async Task RemoveFromGroupAsync(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            Logger.LogDebug("Removed connection {ConnectionId} from group {GroupName} in {HubName}", 
                Context.ConnectionId, groupName, GetHubName());
        }
    }
}