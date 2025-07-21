using System;
using System.Threading.Tasks;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.SignalR.Messages;
using ConduitLLM.Http.SignalR.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.SignalR.Hubs
{
    /// <summary>
    /// Enhanced SignalR hub for video generation with message acknowledgment support
    /// </summary>
    public class EnhancedVideoGenerationHub : AcknowledgmentHub
    {
        private readonly IAsyncTaskService _taskService;
        private readonly ILogger<EnhancedVideoGenerationHub> _logger;

        public EnhancedVideoGenerationHub(
            ILogger<EnhancedVideoGenerationHub> logger,
            IServiceProvider serviceProvider,
            ISignalRAcknowledgmentService acknowledgmentService,
            IAsyncTaskService taskService)
            : base(logger, serviceProvider, acknowledgmentService)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _logger = logger;
        }

        protected override string GetHubName() => "EnhancedVideoGenerationHub";

        /// <summary>
        /// Subscribe to updates for a specific video generation task
        /// </summary>
        public async Task SubscribeToTask(string taskId)
        {
            var virtualKeyId = GetVirtualKeyId();
            if (!virtualKeyId.HasValue)
            {
                throw new HubException("Authentication required");
            }

            // Verify task ownership
            var task = await _taskService.GetTaskStatusAsync(taskId);
            if (task == null || task.Metadata?.VirtualKeyId != virtualKeyId.Value)
            {
                _logger.LogWarning(
                    "Virtual Key {KeyId} attempted to subscribe to unauthorized task {TaskId}",
                    virtualKeyId, taskId);
                throw new HubException("Unauthorized access to task");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, SignalRConstants.Groups.VideoTask(taskId));
            
            _logger.LogDebug(
                "Virtual Key {KeyId} subscribed to video task {TaskId}",
                virtualKeyId, taskId);
        }

        /// <summary>
        /// Unsubscribe from updates for a specific video generation task
        /// </summary>
        public async Task UnsubscribeFromTask(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SignalRConstants.Groups.VideoTask(taskId));
            
            _logger.LogDebug(
                "Client {ConnectionId} unsubscribed from video task {TaskId}",
                Context.ConnectionId, taskId);
        }

        /// <summary>
        /// Send task progress update with acknowledgment
        /// </summary>
        public async Task SendTaskProgressWithAck(string taskId, int progress, string status)
        {
            var message = new TaskProgressMessage
            {
                TaskId = taskId,
                ProgressPercentage = progress,
                StatusMessage = status,
                CorrelationId = taskId
            };

            // Send to task group with acknowledgment required
            await SendToGroupWithAcknowledgmentAsync(
                SignalRConstants.Groups.VideoTask(taskId),
                "TaskProgress",
                message,
                TimeSpan.FromSeconds(10)); // 10 second timeout for progress updates
        }

        /// <summary>
        /// Send task completion notification with acknowledgment
        /// </summary>
        public async Task SendTaskCompletedWithAck(string taskId, bool success, object? result, string? error)
        {
            var message = new TaskCompletedMessage
            {
                TaskId = taskId,
                IsSuccess = success,
                Result = result,
                ErrorMessage = error,
                CorrelationId = taskId,
                DurationMilliseconds = 0 // Would be calculated from task start time
            };

            // Send to task group with acknowledgment required
            await SendToGroupWithAcknowledgmentAsync(
                SignalRConstants.Groups.VideoTask(taskId),
                "TaskCompleted",
                message,
                TimeSpan.FromSeconds(30)); // 30 second timeout for completion notifications
        }
    }
}