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
    public class ImageGenerationHub : BaseVirtualKeyHub
    {
        private readonly IAsyncTaskService _taskService;

        public ImageGenerationHub(
            ILogger<ImageGenerationHub> logger,
            IAsyncTaskService taskService)
            : base(logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        protected override string GetHubName() => "ImageGenerationHub";

        /// <summary>
        /// Subscribe to updates for a specific image generation task
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Verify task ownership
            var taskStatus = await _taskService.GetTaskStatusAsync(taskId);
            if (taskStatus == null)
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to non-existent task {TaskId}", 
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
                        Logger.LogWarning("Task {TaskId} has invalid virtual key metadata type", taskId);
                        throw new HubException("Invalid task");
                    }

                    if (taskVirtualKeyId != virtualKeyId)
                    {
                        Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to task {TaskId} owned by Virtual Key {OwnerKeyId}", 
                            virtualKeyId, taskId, taskVirtualKeyId);
                        throw new HubException("Unauthorized access to task");
                    }
                }
                else
                {
                    Logger.LogWarning("Task {TaskId} has no virtual key metadata", taskId);
                    throw new HubException("Invalid task");
                }
            }
            else
            {
                Logger.LogWarning("Task {TaskId} has no metadata", taskId);
                throw new HubException("Invalid task");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"image-{taskId}");
            Logger.LogDebug("Virtual Key {KeyId} subscribed to image task {TaskId}", 
                virtualKeyId, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific image generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"image-{taskId}");
            Logger.LogDebug("Client {ConnectionId} unsubscribed from image task {TaskId}", 
                Context.ConnectionId, taskId);
        }
    }
}