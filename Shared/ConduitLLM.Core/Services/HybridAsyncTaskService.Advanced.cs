using System.Text.Json;

using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Configuration.Interfaces;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Advanced operations for HybridAsyncTaskService.
    /// </summary>
    public partial class HybridAsyncTaskService
    {
        /// <inheritdoc/>
        public async Task<AsyncTaskClaimResult> TryClaimTaskAsync(
            string taskId,
            string workerId,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var result = await _repository.TryClaimTaskAsync(
                taskId, workerId, leaseDuration, cancellationToken);
            if (result == AsyncTaskClaimResult.Claimed)
            {
                await _cache.RemoveAsync(GetTaskKey(taskId), cancellationToken);
            }
            return result;
        }

        /// <inheritdoc/>
        public Task<bool> MarkProviderInvocationStartedAsync(
            string taskId,
            string workerId,
            CancellationToken cancellationToken = default)
            => _repository.MarkProviderInvocationStartedAsync(taskId, workerId, cancellationToken);

        /// <inheritdoc/>
        public Task<bool> MarkProviderInvocationCompletedAsync(
            string taskId,
            string workerId,
            string? providerOperationId = null,
            CancellationToken cancellationToken = default)
            => _repository.MarkProviderInvocationCompletedAsync(
                taskId, workerId, providerOperationId, cancellationToken);

        /// <inheritdoc/>
        public Task<bool> ExtendTaskLeaseAsync(
            string taskId,
            string workerId,
            TimeSpan extension,
            CancellationToken cancellationToken = default)
            => _repository.ExtendLeaseAsync(taskId, workerId, extension, cancellationToken);

        /// <inheritdoc/>
        public async Task<bool> ResolveIndeterminateTaskAsync(
            string taskId,
            IndeterminateTaskResolution resolution,
            string reason,
            string? providerOperationId = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            var (state, retryable) = resolution switch
            {
                IndeterminateTaskResolution.SafeToRetry => ((int)TaskState.Pending, true),
                IndeterminateTaskResolution.Failed => ((int)TaskState.Failed, false),
                IndeterminateTaskResolution.Completed => ((int)TaskState.Completed, false),
                _ => throw new ArgumentOutOfRangeException(nameof(resolution))
            };
            var updated = await _repository.ResolveIndeterminateTaskAsync(
                taskId, state, retryable, reason, providerOperationId, cancellationToken);
            if (updated)
            {
                await _cache.RemoveAsync(GetTaskKey(taskId), cancellationToken);
            }
            return updated;
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
            if (_eventBus != null)
            {
                await _eventBus.PublishAsync(new AsyncTaskDeleted
                {
                    TaskId = taskId
                }, cancellationToken);
            }
            
            _logger.LogInformation("Deleted task {TaskId} from both cache and database", taskId);
        }

        /// <inheritdoc/>
        public async Task<AsyncTaskCleanupResult> CleanupOldTasksAsync(
            AsyncTaskRetentionPolicy policy,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (policy.ArchiveCompletedAfter <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(policy.ArchiveCompletedAfter));
            if (policy.DeleteArchivedAfter <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(policy.DeleteArchivedAfter));
            if (policy.ArchiveStaleAfter <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(policy.ArchiveStaleAfter));

            var batchSize = Math.Clamp(policy.BatchSize, 1, 10_000);
            var archivedCount = await _repository.ArchiveOldTasksAsync(
                policy.ArchiveCompletedAfter,
                policy.ArchiveStaleAfter,
                cancellationToken);

            var deletedTotal = 0;
            while (true)
            {
                var tasksToDelete = await _repository.GetTasksForCleanupAsync(
                    policy.DeleteArchivedAfter,
                    batchSize,
                    cancellationToken);
                if (tasksToDelete.Count == 0)
                    break;

                var taskIds = tasksToDelete.Select(task => task.Id).ToArray();
                var deletedCount = await _repository.BulkDeleteAsync(taskIds, cancellationToken);
                if (deletedCount == 0)
                    break;

                deletedTotal += deletedCount;
                foreach (var taskId in taskIds)
                {
                    await _cache.RemoveAsync(GetTaskKey(taskId), cancellationToken);
                }
            }

            _logger.LogInformation(
                "Async task retention archived {ArchivedCount} and deleted {DeletedCount} tasks",
                archivedCount,
                deletedTotal);
            return new AsyncTaskCleanupResult(archivedCount, deletedTotal);
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
    }
}
