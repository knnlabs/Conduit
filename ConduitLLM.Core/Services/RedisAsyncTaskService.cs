using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Redis-based implementation of the async task service.
    /// Suitable for distributed deployments with multiple instances.
    /// </summary>
    public class RedisAsyncTaskService : IAsyncTaskService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisAsyncTaskService> _logger;
        private readonly string _keyPrefix = "conduit:tasks:";
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisAsyncTaskService(IConnectionMultiplexer redis, ILogger<RedisAsyncTaskService> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, object metadata, CancellationToken cancellationToken = default)
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

            var db = _redis.GetDatabase();
            var key = GetTaskKey(taskId);
            var json = JsonSerializer.Serialize(taskStatus, _jsonOptions);
            
            // Set with 24 hour expiration by default
            await db.StringSetAsync(key, json, TimeSpan.FromHours(24));
            
            // Add to index for cleanup operations
            await db.SetAddAsync(GetTaskIndexKey(), taskId);

            _logger.LogInformation("Created async task {TaskId} of type {TaskType}", taskId, taskType);
            return taskId;
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, int virtualKeyId, object metadata, CancellationToken cancellationToken = default)
        {
            // For Redis implementation, we store virtualKeyId in metadata
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
            var db = _redis.GetDatabase();
            var key = GetTaskKey(taskId);
            var json = await db.StringGetAsync(key);

            if (json.IsNullOrEmpty)
            {
                return null;
            }

            var status = JsonSerializer.Deserialize<AsyncTaskStatus>(json!, _jsonOptions);
            return status;
        }

        /// <inheritdoc/>
        public async Task UpdateTaskStatusAsync(string taskId, TaskState status, int? progress = null, object? result = null, string? error = null, CancellationToken cancellationToken = default)
        {
            var taskStatus = await GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                throw new InvalidOperationException($"Task with ID {taskId} not found");
            }
            
            var now = DateTime.UtcNow;
            taskStatus.State = status;
            taskStatus.UpdatedAt = now;

            if (progress.HasValue)
            {
                taskStatus.Progress = progress.Value;
            }

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

            var db = _redis.GetDatabase();
            var key = GetTaskKey(taskId);
            var json = JsonSerializer.Serialize(taskStatus, _jsonOptions);
            
            // Extend expiration for completed tasks to allow retrieval
            var expiration = taskStatus.CompletedAt.HasValue ? TimeSpan.FromHours(2) : TimeSpan.FromHours(24);
            await db.StringSetAsync(key, json, expiration);

            _logger.LogInformation("Updated task {TaskId} to state {State}", taskId, status);
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
                if (status == null)
                {
                    throw new InvalidOperationException($"Task with ID {taskId} not found");
                }

                switch (status.State)
                {
                    case TaskState.Completed:
                    case TaskState.Failed:
                    case TaskState.Cancelled:
                    case TaskState.TimedOut:
                        return status;
                    
                    case TaskState.Pending:
                    case TaskState.Processing:
                        // Continue polling
                        break;
                    
                    default:
                        throw new InvalidOperationException($"Unknown task state: {status.State}");
                }

                await Task.Delay(pollingInterval, cancellationToken);
            }

            // Timeout reached
            await UpdateTaskStatusAsync(taskId, TaskState.TimedOut, error: "Task polling timed out", cancellationToken: cancellationToken);
            var finalStatus = await GetTaskStatusAsync(taskId, cancellationToken);
            return finalStatus ?? throw new InvalidOperationException($"Task with ID {taskId} not found after timeout");
        }

        /// <inheritdoc/>
        public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            await UpdateTaskStatusAsync(taskId, TaskState.Cancelled, error: "Task was cancelled", cancellationToken: cancellationToken);
        }

        /// <inheritdoc/>
        public async Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var key = GetTaskKey(taskId);
            var indexKey = GetTaskIndexKey();
            
            await db.KeyDeleteAsync(key);
            await db.SetRemoveAsync(indexKey, taskId);
            
            _logger.LogInformation("Deleted task {TaskId}", taskId);
        }

        /// <inheritdoc/>
        public async Task<int> CleanupOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();
            var indexKey = GetTaskIndexKey();
            var taskIds = await db.SetMembersAsync(indexKey);
            var cutoffTime = DateTime.UtcNow - olderThan;
            var removedCount = 0;

            foreach (var taskId in taskIds)
            {
                try
                {
                    var key = GetTaskKey(taskId!);
                    var json = await db.StringGetAsync(key);
                    
                    if (!json.IsNullOrEmpty)
                    {
                        var status = JsonSerializer.Deserialize<AsyncTaskStatus>(json!, _jsonOptions);
                        if (status != null && 
                            status.UpdatedAt < cutoffTime && 
                            (status.State == TaskState.Completed || 
                             status.State == TaskState.Failed || 
                             status.State == TaskState.Cancelled ||
                             status.State == TaskState.TimedOut))
                        {
                            await db.KeyDeleteAsync(key);
                            await db.SetRemoveAsync(indexKey, taskId);
                            removedCount++;
                        }
                    }
                    else
                    {
                        // Clean up orphaned index entry
                        await db.SetRemoveAsync(indexKey, taskId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up task {TaskId}", taskId);
                }
            }

            _logger.LogInformation("Cleaned up {Count} old tasks", removedCount);
            return removedCount;
        }

        /// <summary>
        /// Updates task progress information.
        /// </summary>
        /// <param name="taskId">The ID of the task to update</param>
        /// <param name="progressPercentage">Progress percentage (0-100)</param>
        /// <param name="progressMessage">Optional progress message</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task UpdateTaskProgressAsync(string taskId, int progressPercentage, string? progressMessage = null, CancellationToken cancellationToken = default)
        {
            var taskStatus = await GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                throw new InvalidOperationException($"Task with ID {taskId} not found");
            }
            
            taskStatus.Progress = Math.Clamp(progressPercentage, 0, 100);
            if (!string.IsNullOrEmpty(progressMessage))
            {
                taskStatus.ProgressMessage = progressMessage;
            }
            taskStatus.UpdatedAt = DateTime.UtcNow;

            var db = _redis.GetDatabase();
            var key = GetTaskKey(taskId);
            var json = JsonSerializer.Serialize(taskStatus, _jsonOptions);
            
            await db.StringSetAsync(key, json, TimeSpan.FromHours(24));

            _logger.LogDebug("Updated task {TaskId} progress to {Progress}%", taskId, progressPercentage);
        }

        private string GetTaskKey(string taskId) => $"{_keyPrefix}{taskId}";
        private string GetTaskIndexKey() => $"{_keyPrefix}index";
    }
}