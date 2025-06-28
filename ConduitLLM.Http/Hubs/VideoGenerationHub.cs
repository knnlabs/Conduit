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
    public class VideoGenerationHub : SecureHub
    {
        private readonly IAsyncTaskService _taskService;

        public VideoGenerationHub(
            ILogger<VideoGenerationHub> logger,
            IAsyncTaskService taskService,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
        }

        protected override string GetHubName() => "VideoGenerationHub";

        /// <summary>
        /// Subscribe to updates for a specific video generation task
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Verify task ownership using the base class method
            if (!await CanAccessTaskAsync(taskId))
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to unauthorized task {TaskId}", 
                    virtualKeyId, taskId);
                throw new HubException("Unauthorized access to task");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"video-{taskId}");
            Logger.LogDebug("Virtual Key {KeyId} subscribed to video task {TaskId}", 
                virtualKeyId, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"video-{taskId}");
            Logger.LogDebug("Client {ConnectionId} unsubscribed from video task {TaskId}", 
                Context.ConnectionId, taskId);
        }
    }
}