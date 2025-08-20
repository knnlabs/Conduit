using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
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