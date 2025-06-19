using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// In-memory implementation of the async task service.
    /// Suitable for development and single-instance deployments.
    /// </summary>
    public class InMemoryAsyncTaskService : IAsyncTaskService
    {
        private readonly ConcurrentDictionary<string, AsyncTaskStatus> _tasks = new();
        private readonly ILogger<InMemoryAsyncTaskService> _logger;

        public InMemoryAsyncTaskService(ILogger<InMemoryAsyncTaskService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public Task<string> CreateTaskAsync(string taskType, object metadata, CancellationToken cancellationToken = default)
        {
            var taskId = Guid.NewGuid().ToString();
            var now = DateTime.UtcNow;
            
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = taskType,
                State = TaskState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                Metadata = metadata
            };

            if (!_tasks.TryAdd(taskId, taskStatus))
            {
                throw new InvalidOperationException($"Failed to create task with ID {taskId}");
            }

            _logger.LogInformation("Created async task {TaskId} of type {TaskType}", taskId, taskType);
            return Task.FromResult(taskId);
        }

        /// <inheritdoc/>
        public Task<AsyncTaskStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (_tasks.TryGetValue(taskId, out var status))
            {
                return Task.FromResult(status);
            }

            throw new InvalidOperationException($"Task with ID {taskId} not found");
        }

        /// <inheritdoc/>
        public Task UpdateTaskStatusAsync(string taskId, TaskState status, object? result = null, string? error = null, CancellationToken cancellationToken = default)
        {
            if (!_tasks.TryGetValue(taskId, out var taskStatus))
            {
                throw new InvalidOperationException($"Task with ID {taskId} not found");
            }

            var now = DateTime.UtcNow;
            taskStatus.State = status;
            taskStatus.UpdatedAt = now;

            if (status == TaskState.Completed || status == TaskState.Failed || status == TaskState.Cancelled || status == TaskState.TimedOut)
            {
                taskStatus.CompletedAt = now;
            }

            if (result != null)
            {
                taskStatus.Result = result;
            }

            if (!string.IsNullOrEmpty(error))
            {
                taskStatus.Error = error;
            }

            _logger.LogInformation("Updated task {TaskId} to state {State}", taskId, status);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task<AsyncTaskStatus> PollTaskUntilCompletedAsync(
            string taskId, 
            TimeSpan pollingInterval, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            var endTime = startTime + timeout;

            while (DateTime.UtcNow < endTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var status = await GetTaskStatusAsync(taskId, cancellationToken);

                switch (status.State)
                {
                    case TaskState.Completed:
                    case TaskState.Failed:
                    case TaskState.Cancelled:
                    case TaskState.TimedOut:
                        return status;
                    
                    case TaskState.Pending:
                    case TaskState.Running:
                        // Continue polling
                        break;
                    
                    default:
                        throw new InvalidOperationException($"Unknown task state: {status.State}");
                }

                await Task.Delay(pollingInterval, cancellationToken);
            }

            // Timeout reached
            await UpdateTaskStatusAsync(taskId, TaskState.TimedOut, error: "Task polling timed out", cancellationToken: cancellationToken);
            return await GetTaskStatusAsync(taskId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            await UpdateTaskStatusAsync(taskId, TaskState.Cancelled, error: "Task was cancelled", cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public Task<int> CleanupOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var tasksToRemove = _tasks
                .Where(kvp => kvp.Value.UpdatedAt < cutoffTime && 
                             (kvp.Value.State == TaskState.Completed || 
                              kvp.Value.State == TaskState.Failed || 
                              kvp.Value.State == TaskState.Cancelled ||
                              kvp.Value.State == TaskState.TimedOut))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var taskId in tasksToRemove)
            {
                _tasks.TryRemove(taskId, out _);
            }

            _logger.LogInformation("Cleaned up {Count} old tasks", tasksToRemove.Count);
            return Task.FromResult(tasksToRemove.Count);
        }

        /// <summary>
        /// Updates task progress information.
        /// </summary>
        /// <param name="taskId">The ID of the task to update</param>
        /// <param name="progressPercentage">Progress percentage (0-100)</param>
        /// <param name="progressMessage">Optional progress message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public Task UpdateTaskProgressAsync(string taskId, int progressPercentage, string? progressMessage = null, CancellationToken cancellationToken = default)
        {
            if (!_tasks.TryGetValue(taskId, out var taskStatus))
            {
                throw new InvalidOperationException($"Task with ID {taskId} not found");
            }

            taskStatus.ProgressPercentage = Math.Clamp(progressPercentage, 0, 100);
            if (!string.IsNullOrEmpty(progressMessage))
            {
                taskStatus.ProgressMessage = progressMessage;
            }
            taskStatus.UpdatedAt = DateTime.UtcNow;

            _logger.LogDebug("Updated task {TaskId} progress to {Progress}%", taskId, progressPercentage);
            return Task.CompletedTask;
        }
    }
}