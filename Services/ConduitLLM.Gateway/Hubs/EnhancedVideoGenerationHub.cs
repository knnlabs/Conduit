using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Models;
using ConduitLLM.Gateway.Services;

using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Gateway.Hubs
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
        /// Send task progress update to the task group (fire-and-forget; group sends
        /// are not individually acknowledged)
        /// </summary>
        public async Task SendTaskProgressWithAck(string taskId, int progress, string status)
        {
            _logger.LogDebug("Sending progress for video task {TaskId}: {Progress}% - {Status}",
                taskId, progress, status);

            var message = new TaskProgressMessage
            {
                TaskId = taskId,
                ProgressPercentage = progress,
                StatusMessage = status,
                CorrelationId = taskId
            };

            await SendToGroupAsync(
                SignalRConstants.Groups.VideoTask(taskId),
                "TaskProgress",
                message);
        }

        /// <summary>
        /// Send task completion notification to the task group (fire-and-forget; group
        /// sends are not individually acknowledged)
        /// </summary>
        public async Task SendTaskCompletedWithAck(string taskId, bool success, object? result, string? error)
        {
            if (success)
            {
                _logger.LogInformation("Sending completion for video task {TaskId}: succeeded", taskId);
            }
            else
            {
                _logger.LogWarning("Sending completion for video task {TaskId}: failed - {Error}",
                    taskId, error);
            }

            // Compute the actual task duration from the task record
            var task = await _taskService.GetTaskStatusAsync(taskId);
            var durationMs = task != null
                ? (long)(((task.CompletedAt ?? DateTime.UtcNow) - task.CreatedAt).TotalMilliseconds)
                : 0;

            var message = new TaskCompletedMessage
            {
                TaskId = taskId,
                IsSuccess = success,
                Result = result,
                ErrorMessage = error,
                CorrelationId = taskId,
                DurationMilliseconds = durationMs
            };

            await SendToGroupAsync(
                SignalRConstants.Groups.VideoTask(taskId),
                "TaskCompleted",
                message);
        }
    }
}