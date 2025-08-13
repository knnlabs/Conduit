using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Repositories;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using MassTransit;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using ConduitLLM.Configuration.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Hybrid implementation of async task management service that uses both database and cache.
    /// </summary>
    public class HybridAsyncTaskService : IAsyncTaskService
    {
        private readonly IAsyncTaskRepository _repository;
        private readonly IDistributedCache _cache;
        private readonly IPublishEndpoint? _publishEndpoint;
        private readonly ILogger<HybridAsyncTaskService> _logger;
        private const string TASK_KEY_PREFIX = "async:task:";
        private const int CACHE_EXPIRY_HOURS = 2; // Shorter expiry for completed tasks

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAsyncTaskService"/> class.
        /// </summary>
        /// <param name="repository">The async task repository.</param>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="logger">The logger instance.</param>
        public HybridAsyncTaskService(
            IAsyncTaskRepository repository,
            IDistributedCache cache,
            ILogger<HybridAsyncTaskService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HybridAsyncTaskService"/> class with event publishing.
        /// </summary>
        /// <param name="repository">The async task repository.</param>
        /// <param name="cache">The distributed cache service.</param>
        /// <param name="publishEndpoint">The event publish endpoint (optional, can be null).</param>
        /// <param name="logger">The logger instance.</param>
        public HybridAsyncTaskService(
            IAsyncTaskRepository repository,
            IDistributedCache cache,
            IPublishEndpoint? publishEndpoint,
            ILogger<HybridAsyncTaskService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _publishEndpoint = publishEndpoint; // Allow null
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, object metadata, CancellationToken cancellationToken = default)
        {
            var taskId = GenerateTaskId();
            var now = DateTime.UtcNow;
            var virtualKeyId = GetVirtualKeyIdFromMetadata(metadata);

            // Create database entity
            var asyncTask = new AsyncTask
            {
                Id = taskId,
                Type = taskType,
                State = (int)TaskState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                VirtualKeyId = virtualKeyId,
                Metadata = JsonSerializer.Serialize(metadata),
                Progress = 0
            };

            // Save to database first - this is the critical operation
            await _repository.CreateAsync(asyncTask, cancellationToken);
            _logger.LogInformation("Created async task {TaskId} in database", taskId);

            // Create task status for cache
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = taskType,
                State = TaskState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                Metadata = metadata as TaskMetadata,
                Progress = 0
            };

            // Best-effort cache update with resilience
            try
            {
                await CacheTaskStatusAsync(taskId, taskStatus, cancellationToken);
                _logger.LogDebug("Cached task {TaskId} status", taskId);
            }
            catch (Exception ex)
            {
                // Log but don't fail - cache will self-heal on next read
                _logger.LogWarning(ex, "Failed to cache task {TaskId}, will self-heal on next read", taskId);
            }
            
            // Best-effort event publishing
            if (_publishEndpoint != null)
            {
                try
                {
                    await _publishEndpoint.Publish(new AsyncTaskCreated
                    {
                        TaskId = taskId,
                        TaskType = taskType,
                        VirtualKeyId = virtualKeyId
                    }, cancellationToken);
                    _logger.LogDebug("Published AsyncTaskCreated event for task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    // Log but don't fail - events are not critical for task creation
                    _logger.LogWarning(ex, "Failed to publish AsyncTaskCreated event for task {TaskId}", taskId);
                }
            }
            
            _logger.LogInformation("Created hybrid async task {TaskId} of type {TaskType}", taskId, taskType);
            
            return taskId;
        }

        /// <inheritdoc/>
        public async Task<string> CreateTaskAsync(string taskType, int virtualKeyId, object metadata, CancellationToken cancellationToken = default)
        {
            var taskId = GenerateTaskId();
            var now = DateTime.UtcNow;

            // Create database entity
            var asyncTask = new AsyncTask
            {
                Id = taskId,
                Type = taskType,
                State = (int)TaskState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                VirtualKeyId = virtualKeyId,
                Metadata = JsonSerializer.Serialize(metadata),
                Progress = 0
            };

            // Save to database first - this is the critical operation
            await _repository.CreateAsync(asyncTask, cancellationToken);
            _logger.LogInformation("Created async task {TaskId} in database", taskId);

            // Create task status for cache
            var taskStatus = new AsyncTaskStatus
            {
                TaskId = taskId,
                TaskType = taskType,
                State = TaskState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                Metadata = metadata as TaskMetadata,
                Progress = 0
            };

            // Best-effort cache update with resilience
            try
            {
                await CacheTaskStatusAsync(taskId, taskStatus, cancellationToken);
                _logger.LogDebug("Cached task {TaskId} status", taskId);
            }
            catch (Exception ex)
            {
                // Log but don't fail - cache will self-heal on next read
                _logger.LogWarning(ex, "Failed to cache task {TaskId}, will self-heal on next read", taskId);
            }
            
            // Best-effort event publishing
            if (_publishEndpoint != null)
            {
                try
                {
                    await _publishEndpoint.Publish(new AsyncTaskCreated
                    {
                        TaskId = taskId,
                        TaskType = taskType,
                        VirtualKeyId = virtualKeyId
                    }, cancellationToken);
                    _logger.LogDebug("Published AsyncTaskCreated event for task {TaskId}", taskId);
                }
                catch (Exception ex)
                {
                    // Log but don't fail - events are not critical for task creation
                    _logger.LogWarning(ex, "Failed to publish AsyncTaskCreated event for task {TaskId}", taskId);
                }
            }
            
            _logger.LogInformation("Created hybrid async task {TaskId} of type {TaskType} for VirtualKeyId {VirtualKeyId}", 
                taskId, taskType, virtualKeyId);
            
            return taskId;
        }

        /// <inheritdoc/>
        public async Task<AsyncTaskStatus?> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("GetTaskStatusAsync called for task {TaskId}", taskId);
            
            // Try cache first
            var key = GetTaskKey(taskId);
            _logger.LogDebug("Looking for task in cache with key: {CacheKey}", key);
            
            string? json = null;
            bool cacheHit = false;
            
            try
            {
                json = await _cache.GetStringAsync(key, cancellationToken);
                cacheHit = !string.IsNullOrEmpty(json);
                _logger.LogDebug("Cache lookup result - Hit: {CacheHit}, HasValue: {HasValue}", cacheHit, !string.IsNullOrEmpty(json));
            }
            catch (Exception ex)
            {
                // Cache failure - log and continue with database fallback
                _logger.LogWarning(ex, "Cache read failed for task {TaskId} with key {CacheKey}, falling back to database", taskId, key);
            }
            
            if (cacheHit && !string.IsNullOrEmpty(json))
            {
                _logger.LogInformation("Task {TaskId} found in cache with key {CacheKey}", taskId, key);
                try
                {
                    var cachedStatus = JsonSerializer.Deserialize<AsyncTaskStatus>(json);
                    _logger.LogDebug("Successfully deserialized cached task {TaskId}, State: {State}", taskId, cachedStatus?.State);
                    return cachedStatus;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached task {TaskId} from key {CacheKey}, JSON: {Json}", taskId, key, json?.Substring(0, Math.Min(json.Length, 200)));
                    // Continue to database fallback
                }
            }

            // Fallback to database
            _logger.LogInformation("Cache miss for task {TaskId} with key {CacheKey}, reading from database", taskId, key);
            var dbTask = await _repository.GetByIdAsync(taskId, cancellationToken);
            if (dbTask == null)
            {
                _logger.LogWarning("Task {TaskId} not found in database either", taskId);
                return null;
            }
            
            _logger.LogInformation("Task {TaskId} found in database, State: {State}", taskId, dbTask.State);

            // Log consistency monitoring
            if (cacheHit)
            {
                _logger.LogInformation("Task {TaskId} cache-database consistency issue detected - cache had invalid data", taskId);
            }

            // Convert database entity to task status
            var taskStatus = ConvertToTaskStatus(dbTask);

            // Re-cache the task with resilience
            try
            {
                await CacheTaskStatusAsync(taskId, taskStatus, cancellationToken, IsTaskCompleted(taskStatus.State));
                _logger.LogDebug("Re-cached task {TaskId} after database read", taskId);
            }
            catch (Exception ex)
            {
                // Log but don't fail the read operation
                _logger.LogWarning(ex, "Failed to re-cache task {TaskId} after database read", taskId);
            }

            return taskStatus;
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
            // Get task from database to ensure it exists
            var dbTask = await _repository.GetByIdAsync(taskId, cancellationToken);
            if (dbTask == null)
            {
                throw new InvalidOperationException($"Task {taskId} not found");
            }

            // Update database entity
            dbTask.State = (int)status;
            dbTask.UpdatedAt = DateTime.UtcNow;
            
            if (progress.HasValue)
            {
                dbTask.Progress = progress.Value;
            }
            
            if (result != null)
            {
                dbTask.Result = JsonSerializer.Serialize(result);
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                dbTask.Error = error;
                
                // If task is being set to pending for retry after a failure
                if (status == TaskState.Pending && dbTask.RetryCount < dbTask.MaxRetries)
                {
                    dbTask.RetryCount++;
                    dbTask.ProgressMessage = $"Retry {dbTask.RetryCount}/{dbTask.MaxRetries} scheduled";
                    
                    // Calculate next retry time if not already set
                    if (dbTask.NextRetryAt == null || dbTask.NextRetryAt <= DateTime.UtcNow)
                    {
                        // Use exponential backoff: 30s * 2^retryCount
                        var baseDelay = 30; // seconds
                        var delaySeconds = baseDelay * Math.Pow(2, dbTask.RetryCount - 1);
                        var maxDelay = 3600; // 1 hour max
                        delaySeconds = Math.Min(delaySeconds, maxDelay);
                        
                        // Add jitter
                        var jitter = new Random().NextDouble() * 0.4 - 0.2; // -0.2 to +0.2
                        delaySeconds = delaySeconds * (1 + jitter);
                        
                        dbTask.NextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                    }
                }
            }
            
            if (IsTaskCompleted(status))
            {
                dbTask.CompletedAt = DateTime.UtcNow;
            }

            // Save to database
            await _repository.UpdateAsync(dbTask, cancellationToken);

            // Update cache
            var taskStatus = ConvertToTaskStatus(dbTask);
            await CacheTaskStatusAsync(taskId, taskStatus, cancellationToken, IsTaskCompleted(status));
            
            // Publish event if event bus is available
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new AsyncTaskUpdated
                {
                    TaskId = taskId,
                    State = status.ToString(),
                    Progress = dbTask.Progress,
                    IsCompleted = IsTaskCompleted(status)
                }, cancellationToken);
            }
            
            _logger.LogInformation(
                "Updated task {TaskId} status to {Status} with progress {Progress}%", 
                taskId, 
                status, 
                dbTask.Progress);
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

                if (IsTaskCompleted(status.State))
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
            // Delete from cache first
            var key = GetTaskKey(taskId);
            await _cache.RemoveAsync(key, cancellationToken);
            
            // Delete from database
            await _repository.DeleteAsync(taskId, cancellationToken);
            
            // Publish event if event bus is available
            if (_publishEndpoint != null)
            {
                await _publishEndpoint.Publish(new AsyncTaskDeleted
                {
                    TaskId = taskId
                }, cancellationToken);
            }
            
            _logger.LogInformation("Deleted task {TaskId} from both cache and database", taskId);
        }

        /// <inheritdoc/>
        public async Task<int> CleanupOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            // First, archive old tasks in the database
            var archivedCount = await _repository.ArchiveOldTasksAsync(olderThan, cancellationToken);
            
            _logger.LogInformation("Archived {Count} old tasks (older than {OlderThan})", archivedCount, olderThan);

            // Optionally, clean up very old archived tasks
            var cleanupThreshold = TimeSpan.FromDays(30); // Keep archived tasks for 30 days
            var tasksToDelete = await _repository.GetTasksForCleanupAsync(cleanupThreshold, 100, cancellationToken);
            
            if (tasksToDelete.Count() > 0)
            {
                var taskIds = tasksToDelete.Select(t => t.Id);
                var deletedCount = await _repository.BulkDeleteAsync(taskIds, cancellationToken);
                
                _logger.LogInformation("Deleted {Count} archived tasks older than {Days} days", 
                    deletedCount, cleanupThreshold.TotalDays);
            }

            return archivedCount;
        }

        private async Task CacheTaskStatusAsync(
            string taskId, 
            AsyncTaskStatus taskStatus, 
            CancellationToken cancellationToken,
            bool isCompleted = false)
        {
            var key = GetTaskKey(taskId);
            var json = JsonSerializer.Serialize(taskStatus);
            
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(isCompleted ? CACHE_EXPIRY_HOURS : 24)
            };
            
            // Retry logic with exponential backoff for cache operations
            var retryCount = 3;
            var delay = TimeSpan.FromMilliseconds(100);
            
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    await _cache.SetStringAsync(key, json, options, cancellationToken);
                    return; // Success
                }
                catch (Exception ex) when (i < retryCount - 1)
                {
                    _logger.LogWarning(ex, "Cache operation failed for task {TaskId}, attempt {Attempt} of {MaxAttempts}", 
                        taskId, i + 1, retryCount);
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
                }
            }
            
            // If we get here, all retries failed - let it throw
            await _cache.SetStringAsync(key, json, options, cancellationToken);
        }

        private static AsyncTaskStatus ConvertToTaskStatus(AsyncTask dbTask)
        {
            return new AsyncTaskStatus
            {
                TaskId = dbTask.Id,
                TaskType = dbTask.Type,
                State = (TaskState)dbTask.State,
                CreatedAt = dbTask.CreatedAt,
                UpdatedAt = dbTask.UpdatedAt,
                CompletedAt = dbTask.CompletedAt,
                Result = string.IsNullOrEmpty(dbTask.Result) ? null : JsonSerializer.Deserialize<object>(dbTask.Result),
                Error = dbTask.Error,
                Metadata = string.IsNullOrEmpty(dbTask.Metadata) ? null : JsonSerializer.Deserialize<TaskMetadata>(dbTask.Metadata),
                Progress = dbTask.Progress,
                ProgressMessage = dbTask.ProgressMessage,
                RetryCount = dbTask.RetryCount,
                MaxRetries = dbTask.MaxRetries,
                IsRetryable = dbTask.IsRetryable,
                NextRetryAt = dbTask.NextRetryAt
            };
        }

        private static int GetVirtualKeyIdFromMetadata(object metadata)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata), "Metadata cannot be null when creating a task");
            }

            // First check if it's already a TaskMetadata instance
            if (metadata is TaskMetadata taskMetadata)
            {
                return taskMetadata.VirtualKeyId;
            }

            try
            {
                // Extract VirtualKeyId from metadata
                var json = JsonSerializer.Serialize(metadata);
                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;

                // Try different property name variations (case-insensitive)
                string[] propertyNames = { "virtualKeyId", "VirtualKeyId", "virtualkeyid" };
                
                foreach (var propertyName in propertyNames)
                {
                    if (root.TryGetProperty(propertyName, out var element))
                    {
                        // Try to get as int first
                        if (element.TryGetInt32(out var intValue))
                        {
                            return intValue;
                        }
                        
                        // Try to parse string as int
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            var stringValue = element.GetString();
                            if (!string.IsNullOrEmpty(stringValue) && int.TryParse(stringValue, out var parsedValue))
                            {
                                return parsedValue;
                            }
                        }
                    }
                }

                // More detailed error message to help debugging
                var availableProperties = string.Join(", ", root.EnumerateObject().Select(p => p.Name));
                throw new InvalidOperationException(
                    $"VirtualKeyId not found in metadata. Available properties: {availableProperties}. " +
                    "Metadata must contain a 'virtualKeyId' or 'VirtualKeyId' property with an integer value.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Failed to parse metadata as JSON", ex);
            }
        }

        /// <inheritdoc/>
        public async Task<IList<AsyncTaskStatus>> GetPendingTasksAsync(string? taskType = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            // Query database for pending tasks
            var pendingTasks = await _repository.GetPendingTasksAsync(taskType, limit, cancellationToken);
            var taskStatuses = new List<AsyncTaskStatus>();

            foreach (var task in pendingTasks)
            {
                var taskStatus = ConvertToTaskStatus(task);
                taskStatuses.Add(taskStatus);

                // Update cache with pending tasks
                try
                {
                    var cacheKey = GetTaskKey(task.Id);
                    var json = JsonSerializer.Serialize(taskStatus);
                    await _cache.SetStringAsync(cacheKey, json, new DistributedCacheEntryOptions
                    {
                        SlidingExpiration = TimeSpan.FromHours(24)
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache pending task {TaskId}", task.Id);
                }
            }

            return taskStatuses;
        }

        private static bool IsTaskCompleted(TaskState state)
        {
            return state == TaskState.Completed || 
                   state == TaskState.Failed || 
                   state == TaskState.Cancelled ||
                   state == TaskState.TimedOut;
        }

        private static string GetTaskKey(string taskId) => $"{TASK_KEY_PREFIX}{taskId}";

        private static string GenerateTaskId() => $"task_{Guid.NewGuid():N}";
    }
}