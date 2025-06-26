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
        /// Subscribe to updates for a specific video generation request
        /// </summary>
        public async Task SubscribeToRequest(string requestId)
        {
            var virtualKeyId = RequireVirtualKeyId();
            
            // Verify request ownership using the base class method
            if (!await CanAccessTaskAsync(requestId))
            {
                Logger.LogWarning("Virtual Key {KeyId} attempted to subscribe to unauthorized request {RequestId}", 
                    virtualKeyId, requestId);
                throw new HubException("Unauthorized access to request");
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