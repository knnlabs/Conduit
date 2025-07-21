using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Unified SignalR hub for real-time content generation status updates.
    /// Handles both image and video generation notifications.
    /// </summary>
    public class ContentGenerationHub : SecureHub
    {
        private readonly IAsyncTaskService _taskService;

        public ContentGenerationHub(
            ILogger<ContentGenerationHub> logger,
            IAsyncTaskService taskService,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        protected override string GetHubName() => "ContentGenerationHub";

        /// <summary>
        /// Subscribe to updates for a specific content generation task.
        /// Supports both image and video generation tasks.
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="contentType">The type of content (image or video)</param>
        public async Task SubscribeToTask(string taskId, string contentType)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Validate content type
            if (contentType != "image" && contentType != "video")
            {
                throw new HubException("Invalid content type. Must be 'image' or 'video'");
            }
            
            // Verify task ownership using the base class method
            if (!await CanAccessTaskAsync(taskId))
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to unauthorized {ContentType} task {TaskId}", 
                    virtualKeyId, contentType, taskId);
                throw new HubException("Unauthorized access to task");
            }
            
            // Add to content-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{contentType}-{taskId}");
            
            // Also add to unified content group for cross-content notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"content-{taskId}");
            
            Logger.LogDebug("Virtual Key {KeyId} subscribed to {ContentType} task {TaskId}", 
                virtualKeyId, contentType, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific content generation task.
        /// </summary>
        /// <param name="taskId">The task identifier</param>
        /// <param name="contentType">The type of content (image or video)</param>
        public async Task UnsubscribeFromTask(string taskId, string contentType)
        {
            // Validate content type
            if (contentType != "image" && contentType != "video")
            {
                throw new HubException("Invalid content type. Must be 'image' or 'video'");
            }
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{contentType}-{taskId}");
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"content-{taskId}");
            
            Logger.LogDebug("Client {ConnectionId} unsubscribed from {ContentType} task {TaskId}", 
                Context.ConnectionId, contentType, taskId);
        }

        /// <summary>
        /// Subscribe to all content generation updates for the current virtual key.
        /// Useful for dashboard views that need to monitor all generation tasks.
        /// </summary>
        public async Task SubscribeToAllTasks()
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}-content");
            
            Logger.LogDebug("Virtual Key {KeyId} subscribed to all content generation updates", 
                virtualKeyId);
        }

        /// <summary>
        /// Unsubscribe from all content generation updates.
        /// </summary>
        public async Task UnsubscribeFromAllTasks()
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vkey-{virtualKeyId}-content");
            
            Logger.LogDebug("Virtual Key {KeyId} unsubscribed from all content generation updates", 
                virtualKeyId);
        }
    }
}