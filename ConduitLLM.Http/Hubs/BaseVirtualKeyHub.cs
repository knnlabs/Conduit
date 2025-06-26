using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Base class for all SignalR hubs that require virtual key authentication.
    /// Provides common functionality for connection management and virtual key extraction.
    /// </summary>
    [Authorize]
    public abstract class BaseVirtualKeyHub : Hub
    {
        protected readonly ILogger Logger;

        protected BaseVirtualKeyHub(ILogger logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override async Task OnConnectedAsync()
        {
            var virtualKeyId = GetVirtualKeyId();
            var virtualKeyName = GetVirtualKeyName();
            
            if (!virtualKeyId.HasValue)
            {
                Logger.LogWarning("Connection without valid virtual key ID to {HubName}", GetHubName());
                Context.Abort();
                return;
            }
            
            // Add to virtual-key-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
            
            Logger.LogInformation("Virtual Key {KeyName} (ID: {KeyId}) connected to {HubName}: {ConnectionId}", 
                virtualKeyName, virtualKeyId, GetHubName(), Context.ConnectionId);
            
            await OnVirtualKeyConnectedAsync(virtualKeyId.Value, virtualKeyName);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyId = GetVirtualKeyId();
            var virtualKeyName = GetVirtualKeyName();
            
            Logger.LogInformation("Virtual Key {KeyName} disconnected from {HubName}: {ConnectionId}", 
                virtualKeyName, GetHubName(), Context.ConnectionId);
            
            if (virtualKeyId.HasValue)
            {
                await OnVirtualKeyDisconnectedAsync(virtualKeyId.Value, virtualKeyName, exception);
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Called when a virtual key successfully connects. Override to implement hub-specific logic.
        /// </summary>
        protected virtual Task OnVirtualKeyConnectedAsync(int virtualKeyId, string virtualKeyName)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a virtual key disconnects. Override to implement hub-specific cleanup.
        /// </summary>
        protected virtual Task OnVirtualKeyDisconnectedAsync(int virtualKeyId, string virtualKeyName, Exception? exception)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the virtual key ID from the connection context
        /// </summary>
        protected int? GetVirtualKeyId()
        {
            // Try from Items first (set by hub filter)
            if (Context.Items.TryGetValue("VirtualKeyId", out var itemValue) && itemValue is int itemId)
            {
                return itemId;
            }
            
            // Try from User claims (set by authentication handler)
            var claim = Context.User?.FindFirst("VirtualKeyId");
            if (claim != null && int.TryParse(claim.Value, out var claimId))
            {
                return claimId;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the virtual key name from the connection context
        /// </summary>
        protected string GetVirtualKeyName()
        {
            // Try from Items first (set by hub filter)
            if (Context.Items.TryGetValue("VirtualKeyName", out var itemValue) && itemValue is string itemName)
            {
                return itemName;
            }
            
            // Try from User claims (set by authentication handler)
            return Context.User?.Identity?.Name ?? "Unknown";
        }

        /// <summary>
        /// Ensures the current connection has a valid virtual key ID, throwing a HubException if not
        /// </summary>
        protected int RequireVirtualKeyId()
        {
            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                throw new HubException("Unauthorized");
            }
            return virtualKeyId.Value;
        }

        /// <summary>
        /// Converts various object types to int, useful for parsing metadata
        /// </summary>
        protected static int? ConvertToInt(object value)
        {
            if (value is int intValue)
                return intValue;
            
            if (value is long longValue)
                return (int)longValue;
            
            if (value is string stringValue && int.TryParse(stringValue, out var parsedValue))
                return parsedValue;
            
            return null;
        }

        /// <summary>
        /// Gets the name of the hub for logging purposes
        /// </summary>
        protected abstract string GetHubName();
    }
}