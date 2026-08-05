using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Hubs;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service implementation of ITaskHub that sends notifications through SignalR TaskHub
    /// </summary>
    public class TaskHubService : ITaskHub
    {
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ILogger<TaskHubService> _logger;

        public TaskHubService(
            IHubContext<TaskHub> hubContext,
            ILogger<TaskHubService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task TaskStarted(string taskId, string taskType, object metadata)
        {
            await _hubContext.Clients.All.SendAsync("TaskStarted", taskId, taskType, metadata);
            _logger.LogInformation("Task {TaskId} of type {TaskType} started", taskId, taskType);
        }

        /// <inheritdoc />
        public async Task TaskProgress(string taskId, int progressPercentage, string? message = null)
        {
            await _hubContext.Clients.All.SendAsync("TaskProgress", taskId, progressPercentage, message);
            _logger.LogDebug("Task {TaskId} progress: {Progress}%", taskId, progressPercentage);
        }

        /// <inheritdoc />
        public async Task TaskCompleted(string taskId, object result)
        {
            await _hubContext.Clients.All.SendAsync("TaskCompleted", taskId, result);
            _logger.LogInformation("Task {TaskId} completed successfully", taskId);
        }

        /// <inheritdoc />
        public async Task TaskFailed(string taskId, string error, bool isRetryable = false)
        {
            await _hubContext.Clients.All.SendAsync("TaskFailed", taskId, error, isRetryable);
            _logger.LogWarning("Task {TaskId} failed: {Error}. Retryable: {IsRetryable}",
                taskId, error, isRetryable);
        }

        /// <inheritdoc />
        public async Task TaskCancelled(string taskId, string? reason = null)
        {
            await _hubContext.Clients.All.SendAsync("TaskCancelled", taskId, reason);
            _logger.LogInformation("Task {TaskId} cancelled. Reason: {Reason}", taskId, reason ?? "User requested");
        }

        /// <inheritdoc />
        public async Task TaskRetrying(string taskId, int attemptNumber, TimeSpan nextRetryDelay)
        {
            await _hubContext.Clients.All.SendAsync("TaskRetrying", taskId, attemptNumber, nextRetryDelay);
            _logger.LogInformation("Task {TaskId} retrying. Attempt {AttemptNumber}, next retry in {Delay}s",
                taskId, attemptNumber, nextRetryDelay.TotalSeconds);
        }

        /// <inheritdoc />
        public async Task TaskTimedOut(string taskId, int timeoutSeconds)
        {
            await _hubContext.Clients.All.SendAsync("TaskTimedOut", taskId, timeoutSeconds);
            _logger.LogWarning("Task {TaskId} timed out after {TimeoutSeconds} seconds", taskId, timeoutSeconds);
        }
    }
}