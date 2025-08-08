using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Constants;
using ConduitLLM.Http.Services;

namespace ConduitLLM.Http.Hubs
{
    /// <summary>
    /// Public SignalR hub for video generation status updates using ephemeral keys.
    /// This hub allows browser clients to receive real-time updates without exposing virtual keys.
    /// </summary>
    public class PublicVideoGenerationHub : Hub
    {
        private readonly ILogger<PublicVideoGenerationHub> _logger;
        private readonly IEphemeralKeyService _ephemeralKeyService;

        public PublicVideoGenerationHub(
            ILogger<PublicVideoGenerationHub> logger,
            IEphemeralKeyService ephemeralKeyService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ephemeralKeyService = ephemeralKeyService ?? throw new ArgumentNullException(nameof(ephemeralKeyService));
        }

        /// <summary>
        /// Subscribe to updates for a specific video generation task using an ephemeral key
        /// </summary>
        /// <param name="taskId">The task ID to subscribe to</param>
        /// <param name="ephemeralKey">The ephemeral key for authentication</param>
        public async Task SubscribeToTask(string taskId, string ephemeralKey)
        {
            if (string.IsNullOrEmpty(taskId))
            {
                _logger.LogWarning("Invalid subscription attempt with empty task ID");
                throw new HubException("Task ID is required");
            }

            if (string.IsNullOrEmpty(ephemeralKey))
            {
                _logger.LogWarning("Invalid subscription attempt without ephemeral key for task {TaskId}", taskId);
                throw new HubException("Ephemeral key is required");
            }

            // Validate the ephemeral key
            var virtualKeyId = await _ephemeralKeyService.ValidateAndConsumeKeyAsync(ephemeralKey);
            if (!virtualKeyId.HasValue)
            {
                _logger.LogWarning("Invalid ephemeral key provided for task {TaskId}", taskId);
                throw new HubException("Invalid or expired ephemeral key");
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