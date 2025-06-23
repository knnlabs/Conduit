using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of async task management service.
    /// </summary>
    public class AsyncTaskService : IAsyncTaskService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<AsyncTaskService> _logger;
        private const string TASK_KEY_PREFIX = "async:task:";
        private const int DEFAULT_TASK_EXPIRY_HOURS = 24;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskService"/> class.
        /// </summary>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="logger">The logger instance.</param>
        public AsyncTaskService(IDistributedCache cache, ILogger<AsyncTaskService> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, object metadata, CancellationToken cancellationToken = default)
        {
            var taskId = GenerateTaskId();
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = taskType,
                State = TaskState.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Metadata = metadata,
                Progress = 0
            };

            await SaveTaskStatusAsync(taskId, taskStatus, cancellationToken);
            _logger.LogInformation("Created async task {TaskId} of type {TaskType}", taskId, taskType);
            
            return taskId;
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, int virtualKeyId, object metadata, CancellationToken cancellationToken = default)
        {
            // For this implementation, we store virtualKeyId in metadata
            object enrichedMetadata;
            if (metadata is System.Collections.Generic.Dictionary<string, object> dict)
            {
                enrichedMetadata = new System.Collections.Generic.Dictionary<string, object>(dict) { ["virtualKeyId"] = virtualKeyId };
            }
            else
            {
                enrichedMetadata = new { virtualKeyId = virtualKeyId, originalMetadata = metadata };
            }

            return await CreateTaskAsync(taskType, enrichedMetadata, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<AsyncTaskStatus?> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var key = GetTaskKey(taskId);
            var json = await _cache.GetStringAsync(key, cancellationToken);
            
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Task {TaskId} not found", taskId);
                return null;
            }

            return JsonSerializer.Deserialize<AsyncTaskStatus>(json);
        }

        /// <inheritdoc/>
        public async Task UpdateTaskStatusAsync(
            string taskId, 
            TaskState status, 
            int? progress = null,
            object? result = null, 
            string? error = null, 
            CancellationToken cancellationToken = default)
        {
            var taskStatus = await GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                throw new InvalidOperationException($"Task {taskId} not found");
            }

            taskStatus.State = status;
            taskStatus.UpdatedAt = DateTime.UtcNow;
            
            if (progress.HasValue)
            {
                taskStatus.Progress = progress.Value;
            }
            
            if (result != null)
            {
                taskStatus.Result = result;
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                taskStatus.Error = error;
            }
            
            if (status == TaskState.Completed || status == TaskState.Failed || status == TaskState.Cancelled)
            {
                taskStatus.CompletedAt = DateTime.UtcNow;
            }

            await SaveTaskStatusAsync(taskId, taskStatus, cancellationToken);
            
            _logger.LogInformation(
                "Updated task {TaskId} status to {Status} with progress {Progress}%", 
                taskId, 
                status, 
                taskStatus.Progress);
        }

        /// <inheritdoc/>
        public async Task<AsyncTaskStatus> PollTaskUntilCompletedAsync(
            string taskId, 
            TimeSpan pollingInterval, 
            TimeSpan timeout, 
            CancellationToken cancellationToken = default)
        {
            var endTime = DateTime.UtcNow.Add(timeout);
            
            while (DateTime.UtcNow < endTime && !cancellationToken.IsCancellationRequested)
            {
                var status = await GetTaskStatusAsync(taskId, cancellationToken);
                if (status == null)
                {
                    throw new InvalidOperationException($"Task {taskId} not found");
                }

                if (status.State == TaskState.Completed || 
                    status.State == TaskState.Failed || 
                    status.State == TaskState.Cancelled)
                {
                    return status;
                }

                await Task.Delay(pollingInterval, cancellationToken);
            }

            // If we get here, the task timed out
            await UpdateTaskStatusAsync(taskId, TaskState.TimedOut, error: "Task timed out", cancellationToken: cancellationToken);
            
            var finalStatus = await GetTaskStatusAsync(taskId, cancellationToken);
            return finalStatus!;
        }

        /// <inheritdoc/>
        public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            await UpdateTaskStatusAsync(taskId, TaskState.Cancelled, error: "Task was cancelled", cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var key = GetTaskKey(taskId);
            await _cache.RemoveAsync(key, cancellationToken);
            _logger.LogInformation("Deleted task {TaskId}", taskId);
        }

        /// <inheritdoc/>
        public Task<int> CleanupOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            // Note: This is a simplified implementation. In production, you might want to:
            // 1. Use Redis SCAN to iterate through keys
            // 2. Use Redis Lua scripts for atomic operations
            // 3. Implement batch deletion
            // 4. Track task IDs in a separate set for efficient cleanup
            
            _logger.LogInformation("Cleanup of old tasks requested (older than {OlderThan})", olderThan);
            
            // For now, return 0 as we don't have a way to iterate Redis keys efficiently
            // This would need to be implemented based on your Redis setup
            return Task.FromResult(0);
        }

        private async Task SaveTaskStatusAsync(string taskId, AsyncTaskStatus taskStatus, CancellationToken cancellationToken)
        {
            var key = GetTaskKey(taskId);
            var json = JsonSerializer.Serialize(taskStatus);
            
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(DEFAULT_TASK_EXPIRY_HOURS)
            };
            
            await _cache.SetStringAsync(key, json, options, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<IList<AsyncTaskStatus>> GetPendingTasksAsync(string? taskType = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            // Note: This implementation is limited by Redis capabilities
            // In production, you would want to maintain a sorted set of pending tasks
            // or use a different storage mechanism that supports querying
            
            _logger.LogWarning("GetPendingTasksAsync called on Redis-based AsyncTaskService. " +
                             "This implementation cannot efficiently query pending tasks. " +
                             "Consider using HybridAsyncTaskService or database-backed implementation.");
            
            // Return empty list as we cannot efficiently iterate Redis keys
            return Task.FromResult<IList<AsyncTaskStatus>>(new List<AsyncTaskStatus>());
        }

        private static string GetTaskKey(string taskId) => $"{TASK_KEY_PREFIX}{taskId}";

        private static string GenerateTaskId() => $"task_{Guid.NewGuid():N}";
    }
}