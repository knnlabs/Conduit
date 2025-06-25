using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time image generation status updates
    /// </summary>
    [Authorize]
    public class ImageGenerationHub : Hub
    {
        private readonly ILogger<ImageGenerationHub> _logger;
        private readonly IAsyncTaskService _taskService;

        public ImageGenerationHub(
            ILogger<ImageGenerationHub> logger,
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
            
            _logger.LogInformation("Virtual Key {KeyName} (ID: {KeyId}) connected to ImageGenerationHub: {ConnectionId}", 
                virtualKeyName, virtualKeyId, Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var virtualKeyName = GetVirtualKeyName();
            _logger.LogInformation("Virtual Key {KeyName} disconnected from ImageGenerationHub: {ConnectionId}", 
                virtualKeyName, Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Subscribe to updates for a specific image generation task
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            var virtualKeyId = GetVirtualKeyId();
            
            if (!virtualKeyId.HasValue)
            {
                throw new HubException("Unauthorized");
            }
            
            // Verify task ownership
            var taskStatus = await _taskService.GetTaskStatusAsync(taskId);
            if (taskStatus == null)
            {
                _logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to non-existent task {TaskId}", 
                    virtualKeyId, taskId);
                throw new HubException("Task not found");
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
                        _logger.LogWarning("Task {TaskId} has invalid virtual key metadata type", taskId);
                        throw new HubException("Invalid task");
                    }

                    if (taskVirtualKeyId != virtualKeyId.Value)
                    {
                        _logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to task {TaskId} owned by Virtual Key {OwnerKeyId}", 
                            virtualKeyId, taskId, taskVirtualKeyId);
                        throw new HubException("Unauthorized access to task");
                    }
                }
                else
                {
                    _logger.LogWarning("Task {TaskId} has no virtual key metadata", taskId);
                    throw new HubException("Invalid task");
                }
            }
            else
            {
                _logger.LogWarning("Task {TaskId} has no metadata", taskId);
                throw new HubException("Invalid task");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"image-{taskId}");
            _logger.LogDebug("Virtual Key {KeyId} subscribed to image task {TaskId}", 
                virtualKeyId, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific image generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"image-{taskId}");
            _logger.LogDebug("Client {ConnectionId} unsubscribed from image task {TaskId}", 
                Context.ConnectionId, taskId);
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