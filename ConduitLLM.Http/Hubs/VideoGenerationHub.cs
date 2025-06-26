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
    public class VideoGenerationHub : BaseVirtualKeyHub
    {
        private readonly IAsyncTaskService _taskService;

        public VideoGenerationHub(
            ILogger<VideoGenerationHub> logger,
            IAsyncTaskService taskService)
            : base(logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        protected override string GetHubName() => "VideoGenerationHub";

        /// <summary>
        /// Subscribe to updates for a specific video generation request
        /// </summary>
        public async Task SubscribeToRequest(string requestId)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Verify request ownership
            var taskStatus = await _taskService.GetTaskStatusAsync(requestId);
            if (taskStatus == null)
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to non-existent request {RequestId}", 
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
                        Logger.LogWarning("Request {RequestId} has invalid virtual key metadata type", requestId);
                        throw new HubException("Invalid request");
                    }

                    if (taskVirtualKeyId != virtualKeyId)
                    {
                        Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to request {RequestId} owned by Virtual Key {OwnerKeyId}", 
                            virtualKeyId, requestId, taskVirtualKeyId);
                        throw new HubException("Unauthorized access to request");
                    }
                }
                else
                {
                    Logger.LogWarning("Request {RequestId} has no virtual key metadata", requestId);
                    throw new HubException("Invalid request");
                }
            }
            else
            {
                Logger.LogWarning("Request {RequestId} has no metadata", requestId);
                throw new HubException("Invalid request");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{requestId}");
            Logger.LogDebug("Virtual Key {KeyId} subscribed to video request {RequestId}", 
                virtualKeyId, requestId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation request
        /// </summary>
        public async Task UnsubscribeFromRequest(string requestId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video-{requestId}");
            Logger.LogDebug("Client {ConnectionId} unsubscribed from video request {RequestId}", 
                Context.ConnectionId, requestId);
        }
    }
}