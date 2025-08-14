using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Constants;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// SignalR hub for real-time image generation status updates
    /// </summary>
    public class ImageGenerationHub : SecureHub
    {
        private readonly IAsyncTaskService _taskService;

        public ImageGenerationHub(
            ILogger<ImageGenerationHub> logger,
            IAsyncTaskService taskService,
            IServiceProvider serviceProvider)
            : base(logger, serviceProvider)
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
            var groupName = SignalRConstants.Groups.ImageTask(taskId);
            
            Logger.LogInformation("SubscribeToTask called - VirtualKeyId: {KeyId}, TaskId: {TaskId}, GroupName: {GroupName}, ConnectionId: {ConnectionId}", 
                virtualKeyId, taskId, groupName, Context.ConnectionId);
            
            // Verify task ownership using the base class method
            if (!await CanAccessTaskAsync(taskId))
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to unauthorized task {TaskId}", 
                    virtualKeyId, taskId);
                throw new HubException("Unauthorized access to task");
            }
            
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Logger.LogInformation("Virtual Key {KeyId} successfully subscribed to image task {TaskId} in group {GroupName}, ConnectionId: {ConnectionId}", 
                virtualKeyId, taskId, groupName, Context.ConnectionId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific image generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.ImageTask(taskId));
            Logger.LogDebug("Client {ConnectionId} unsubscribed from image task {TaskId}", 
                Context.ConnectionId, taskId);
        }
    }
}