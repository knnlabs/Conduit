using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Constants;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Public SignalR hub for video generation status updates using task-scoped tokens.
    /// This hub allows browser clients to receive real-time updates without exposing virtual keys.
    /// </summary>
    public class PublicVideoGenerationHub : Hub
    {
        private readonly ILogger<PublicVideoGenerationHub> _logger;
        private readonly ITaskAuthenticationService _taskAuthService;

        public PublicVideoGenerationHub(
            ILogger<PublicVideoGenerationHub> logger,
            ITaskAuthenticationService taskAuthService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskAuthService = taskAuthService ?? throw new ArgumentNullException(nameof(taskAuthService));
        }

        /// <summary>
        /// Subscribe to updates for a specific video generation task using a task token
        /// </summary>
        /// <param name="taskId">The task ID to subscribe to</param>
        /// <param name="token">The authentication token for this task</param>
        public async Task SubscribeToTask(string taskId, string token)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                _logger.LogWarning("Invalid subscription attempt with empty task ID");
                throw new HubException("Task ID is required");
            }

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Invalid subscription attempt without token for task {TaskId}", taskId);
                throw new HubException("Authentication token is required");
            }

            // Validate the token
            var virtualKeyId = await _taskAuthService.ValidateTaskTokenAsync(taskId, token);
            if (!virtualKeyId.HasValue)
            {
                _logger.LogWarning("Invalid token provided for task {TaskId}", taskId);
                throw new HubException("Invalid or expired token");
            }

            // Add to task-specific group
            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.VideoTask(taskId));
            
            // Store task association for this connection
            Context.Items["TaskId"] = taskId;
            Context.Items["VirtualKeyId"] = virtualKeyId.Value;
            
            _logger.LogDebug("Client {ConnectionId} subscribed to video task {TaskId} (VirtualKey: {VirtualKeyId})", 
                Context.ConnectionId, taskId, virtualKeyId.Value);

            // Send initial status update
            await Clients.Caller.SendAsync("taskSubscribed", new { taskId, message = "Successfully subscribed to task updates" });
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation task
        /// </summary>
        /// <param name="taskId">The task ID to unsubscribe from</param>
        public async Task UnsubscribeFromTask(string taskId)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                return;
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.VideoTask(taskId));
            
            _logger.LogDebug("Client {ConnectionId} unsubscribed from video task {TaskId}", 
                Context.ConnectionId, taskId);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected to PublicVideoGenerationHub: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception != null)
            {
                _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
            }
            else
            {
                _logger.LogDebug("Client {ConnectionId} disconnected normally", Context.ConnectionId);
            }

            // Clean up any task associations
            if (Context.Items.TryGetValue("TaskId", out var taskId) && taskId is string taskIdStr)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.VideoTask(taskIdStr));
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}