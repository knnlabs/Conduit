using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
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
        public async Task TaskStarted(string taskId, string taskType, Dictionary<string, object>? metadata = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskStarted", taskId, taskType, metadata);
                _logger.LogInformation("Task {TaskId} of type {TaskType} started", taskId, taskType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskStarted notification for task {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TaskProgress(string taskId, int progressPercentage, string? message = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskProgress", taskId, progressPercentage, message);
                _logger.LogDebug("Task {TaskId} progress: {Progress}%", taskId, progressPercentage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskProgress notification for task {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TaskCompleted(string taskId, object? result = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskCompleted", taskId, result);
                _logger.LogInformation("Task {TaskId} completed successfully", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskCompleted notification for task {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TaskFailed(string taskId, string error, bool isRetryable = false)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskFailed", taskId, error, isRetryable);
                _logger.LogWarning("Task {TaskId} failed: {Error}. Retryable: {IsRetryable}", 
                    taskId, error, isRetryable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskFailed notification for task {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TaskCancelled(string taskId, string? reason = null)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskCancelled", taskId, reason);
                _logger.LogInformation("Task {TaskId} cancelled. Reason: {Reason}", taskId, reason ?? "User requested");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskCancelled notification for task {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task TaskRetrying(string taskId, int attemptNumber, TimeSpan nextRetryDelay)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("TaskRetrying", taskId, attemptNumber, nextRetryDelay);
                _logger.LogInformation("Task {TaskId} retrying. Attempt {AttemptNumber}, next retry in {Delay}s", 
                    taskId, attemptNumber, nextRetryDelay.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending TaskRetrying notification for task {TaskId}", taskId);
                throw;
            }
        }
    }
}