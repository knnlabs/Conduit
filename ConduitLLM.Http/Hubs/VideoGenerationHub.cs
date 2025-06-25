using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time video generation status updates
    /// </summary>
    [Authorize]
    public class VideoGenerationHub : Hub
    {
        private readonly ILogger<VideoGenerationHub> _logger;
        private readonly IAsyncTaskService _taskService;

        public VideoGenerationHub(
            ILogger<VideoGenerationHub> logger,
            IAsyncTaskService taskService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        public override async Task OnConnectedAsync()
        {
            var virtualKeyId = GetVirtualKeyId();
            var virtualKeyName = GetVirtualKeyName();
            
            if (!virtualKeyId.HasValue)
            {
                _logger.LogWarning("Connection without valid virtual key ID");
                Context.Abort();
                return;
            }
            
            // Add to virtual-key-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}");
            
            _logger.LogInformation("Virtual Key {KeyName} (ID: {KeyId}) connected to VideoGenerationHub: {ConnectionId}", 
                virtualKeyName, virtualKeyId, Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyName = GetVirtualKeyName();
            _logger.LogInformation("Virtual Key {KeyName} disconnected from VideoGenerationHub: {ConnectionId}", 
                virtualKeyName, Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to updates for a specific video generation request
        /// </summary>
        public async Task SubscribeToRequest(string requestId)
        {
            var virtualKeyId = GetVirtualKeyId();
            
            if (!virtualKeyId.HasValue)
            {
                throw new HubException("Unauthorized");
            }
            
            // Verify request ownership
            var taskStatus = await _taskService.GetTaskStatusAsync(requestId);
            if (taskStatus == null)
            {
                _logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to non-existent request {RequestId}", 
                    virtualKeyId, requestId);
                throw new HubException("Request not found");
            }
            
            // Extract virtual key ID from task metadata
            if (taskStatus.Metadata != null && taskStatus.Metadata is IDictionary<string, object> metadata)
            {
                if (metadata.TryGetValue("virtualKeyId", out var taskVirtualKeyIdObj))
                {
                    int taskVirtualKeyId;
                    if (taskVirtualKeyIdObj is int intValue)
                    {
                        taskVirtualKeyId = intValue;
                    }
                    else if (taskVirtualKeyIdObj is long longValue)
                    {
                        taskVirtualKeyId = (int)longValue;
                    }
                    else if (taskVirtualKeyIdObj is string stringValue && int.TryParse(stringValue, out var parsedValue))
                    {
                        taskVirtualKeyId = parsedValue;
                    }
                    else
                    {
                        _logger.LogWarning("Request {RequestId} has invalid virtual key metadata type", requestId);
                        throw new HubException("Invalid request");
                    }

                    if (taskVirtualKeyId != virtualKeyId.Value)
                    {
                        _logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to request {RequestId} owned by Virtual Key {OwnerKeyId}", 
                            virtualKeyId, requestId, taskVirtualKeyId);
                        throw new HubException("Unauthorized access to request");
                    }
                }
                else
                {
                    _logger.LogWarning("Request {RequestId} has no virtual key metadata", requestId);
                    throw new HubException("Invalid request");
                }
            }
            else
            {
                _logger.LogWarning("Request {RequestId} has no metadata", requestId);
                throw new HubException("Invalid request");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{requestId}");
            _logger.LogDebug("Virtual Key {KeyId} subscribed to video request {RequestId}", 
                virtualKeyId, requestId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation request
        /// </summary>
        public async Task UnsubscribeFromRequest(string requestId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video-{requestId}");
            _logger.LogDebug("Client {ConnectionId} unsubscribed from video request {RequestId}", 
                Context.ConnectionId, requestId);
        }
        
        /// <summary>
        /// Gets the virtual key ID from the connection context
        /// </summary>
        private int? GetVirtualKeyId()
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
        private string GetVirtualKeyName()
        {
            // Try from Items first (set by hub filter)
            if (Context.Items.TryGetValue("VirtualKeyName", out var itemValue) && itemValue is string itemName)
            {
                return itemName;
            }
            
            // Try from User claims (set by authentication handler)
            return Context.User?.Identity?.Name ?? "Unknown";
        }
    }
}