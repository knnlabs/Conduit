using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Data;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for managing async tasks.
    /// </summary>
    public class AsyncTaskRepository : IAsyncTaskRepository
    {
        private readonly IDbContextFactory<ConfigurationDbContext> _dbContextFactory;
        private readonly ILogger<AsyncTaskRepository> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskRepository"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory.</param>
        /// <param name="logger">The logger instance.</param>
        public AsyncTaskRepository(
            IDbContextFactory<ConfigurationDbContext> dbContextFactory,
            ILogger<AsyncTaskRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<AsyncTask?> GetByIdAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new ArgumentNullException(nameof(taskId));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await context.AsyncTasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting async task by ID: {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting async tasks by virtual key ID: {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetActiveByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.VirtualKeyId == virtualKeyId && !t.IsArchived)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active async tasks by virtual key ID: {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<string> CreateAsync(AsyncTask task, CancellationToken cancellationToken = default)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                task.CreatedAt = DateTime.UtcNow;
                task.UpdatedAt = DateTime.UtcNow;

                context.AsyncTasks.Add(task);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Created async task: {TaskId} of type {TaskType} for virtual key {VirtualKeyId}",
                    task.Id, task.Type, task.VirtualKeyId);

                return task.Id;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating async task: {Task}",
                    LogSanitizer.SanitizeObject(task));
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating async task: {Task}",
                    LogSanitizer.SanitizeObject(task));
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateAsync(AsyncTask task, CancellationToken cancellationToken = default)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                task.UpdatedAt = DateTime.UtcNow;
                
                context.AsyncTasks.Update(task);
                var affected = await context.SaveChangesAsync(cancellationToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Updated async task: {TaskId} with state {State}",
                        task.Id, task.State);
                }
                else
                {
                    _logger.LogWarning("No rows affected when updating async task: {TaskId}", task.Id);
                }

                return affected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating async task: {TaskId}", task.Id);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating async task: {TaskId}", task.Id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating async task: {TaskId}", task.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteAsync(string taskId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new ArgumentNullException(nameof(taskId));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var task = await context.AsyncTasks.FindAsync(new object[] { taskId }, cancellationToken);
                if (task == null)
                {
                    return false;
                }

                context.AsyncTasks.Remove(task);
                var affected = await context.SaveChangesAsync(cancellationToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Deleted async task: {TaskId}", taskId);
                }

                return affected > 0;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error deleting async task: {TaskId}", taskId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting async task: {TaskId}", taskId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> ArchiveOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var cutoffDate = DateTime.UtcNow.Subtract(olderThan);
                
                var completedStates = new[] { 2, 3, 4, 5 }; // Completed, Failed, Cancelled, TimedOut
                
                var tasksToArchive = await context.AsyncTasks
                    .Where(t => !t.IsArchived &&
                               t.CompletedAt.HasValue &&
                               t.CompletedAt.Value < cutoffDate &&
                               completedStates.Contains(t.State))
                    .ToListAsync(cancellationToken);

                foreach (var task in tasksToArchive)
                {
                    task.IsArchived = true;
                    task.ArchivedAt = DateTime.UtcNow;
                    task.UpdatedAt = DateTime.UtcNow;
                }

                var affected = await context.SaveChangesAsync(cancellationToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Archived {Count} completed tasks older than {OlderThan}",
                        affected, olderThan);
                }

                return affected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving old tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetTasksForCleanupAsync(TimeSpan archivedOlderThan, int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var cutoffDate = DateTime.UtcNow.Subtract(archivedOlderThan);
                
                return await context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.IsArchived && t.ArchivedAt.HasValue && t.ArchivedAt.Value < cutoffDate)
                    .OrderBy(t => t.ArchivedAt)
                    .Take(limit)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tasks for cleanup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> BulkDeleteAsync(IEnumerable<string> taskIds, CancellationToken cancellationToken = default)
        {
            if (taskIds == null)
            {
                throw new ArgumentNullException(nameof(taskIds));
            }

            var taskIdList = taskIds.ToList();
            if (!taskIdList.Any())
            {
                return 0;
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var tasksToDelete = await context.AsyncTasks
                    .Where(t => taskIdList.Contains(t.Id))
                    .ToListAsync(cancellationToken);

                context.AsyncTasks.RemoveRange(tasksToDelete);
                var affected = await context.SaveChangesAsync(cancellationToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Bulk deleted {Count} async tasks", affected);
                }

                return affected;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error bulk deleting async tasks");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk deleting async tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetPendingTasksAsync(string? taskType = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                var query = context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.State == 0 && !t.IsArchived && 
                               (t.LeasedBy == null || t.LeaseExpiryTime == null || t.LeaseExpiryTime < now));

                if (!string.IsNullOrEmpty(taskType))
                {
                    query = query.Where(t => t.Type == taskType);
                }

                return await query
                    .OrderBy(t => t.CreatedAt)
                    .Take(limit)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<AsyncTask?> LeaseNextPendingTaskAsync(string workerId, TimeSpan leaseDuration, string? taskType = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(workerId))
            {
                throw new ArgumentNullException(nameof(workerId));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                var query = context.AsyncTasks
                    .Where(t => t.State == 0 && !t.IsArchived && 
                               (t.LeasedBy == null || t.LeaseExpiryTime == null || t.LeaseExpiryTime < now) &&
                               (t.NextRetryAt == null || t.NextRetryAt <= now));

                if (!string.IsNullOrEmpty(taskType))
                {
                    query = query.Where(t => t.Type == taskType);
                }

                // Use row-level locking to prevent concurrent access
                var task = await query
                    .OrderBy(t => t.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (task != null)
                {
                    task.LeasedBy = workerId;
                    task.LeaseExpiryTime = now.Add(leaseDuration);
                    task.UpdatedAt = now;
                    task.Version++;

                    await context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation("Worker {WorkerId} leased task {TaskId} until {ExpiryTime}",
                        workerId, task.Id, task.LeaseExpiryTime);
                }

                return task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leasing next pending task for worker {WorkerId}", workerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ReleaseLeaseAsync(string taskId, string workerId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new ArgumentNullException(nameof(taskId));
            }

            if (string.IsNullOrWhiteSpace(workerId))
            {
                throw new ArgumentNullException(nameof(workerId));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var task = await context.AsyncTasks
                    .FirstOrDefaultAsync(t => t.Id == taskId && t.LeasedBy == workerId, cancellationToken);

                if (task == null)
                {
                    _logger.LogWarning("Task {TaskId} not found or not leased by worker {WorkerId}", taskId, workerId);
                    return false;
                }

                task.LeasedBy = null;
                task.LeaseExpiryTime = null;
                task.UpdatedAt = DateTime.UtcNow;
                task.Version++;

                var affected = await context.SaveChangesAsync(cancellationToken);
                
                if (affected > 0)
                {
                    _logger.LogInformation("Released lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
                }

                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error releasing lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ExtendLeaseAsync(string taskId, string workerId, TimeSpan extension, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new ArgumentNullException(nameof(taskId));
            }

            if (string.IsNullOrWhiteSpace(workerId))
            {
                throw new ArgumentNullException(nameof(workerId));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                var task = await context.AsyncTasks
                    .FirstOrDefaultAsync(t => t.Id == taskId && t.LeasedBy == workerId && 
                                             t.LeaseExpiryTime != null && t.LeaseExpiryTime > now, 
                                             cancellationToken);

                if (task == null)
                {
                    _logger.LogWarning("Task {TaskId} not found, not leased by worker {WorkerId}, or lease expired", 
                        taskId, workerId);
                    return false;
                }

                task.LeaseExpiryTime = now.Add(extension);
                task.UpdatedAt = now;
                task.Version++;

                var affected = await context.SaveChangesAsync(cancellationToken);
                
                if (affected > 0)
                {
                    _logger.LogInformation("Extended lease on task {TaskId} by worker {WorkerId} until {ExpiryTime}", 
                        taskId, workerId, task.LeaseExpiryTime);
                }

                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetExpiredLeaseTasksAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                var now = DateTime.UtcNow;
                return await context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.LeasedBy != null && 
                               t.LeaseExpiryTime != null && 
                               t.LeaseExpiryTime < now &&
                               t.State == 1) // Processing state
                    .OrderBy(t => t.LeaseExpiryTime)
                    .Take(limit)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting expired lease tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateWithVersionCheckAsync(AsyncTask task, int expectedVersion, CancellationToken cancellationToken = default)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                
                // Check version before updating
                var currentVersion = await context.AsyncTasks
                    .Where(t => t.Id == task.Id)
                    .Select(t => t.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentVersion != expectedVersion)
                {
                    _logger.LogWarning("Version mismatch for task {TaskId}. Expected {ExpectedVersion}, found {CurrentVersion}",
                        task.Id, expectedVersion, currentVersion);
                    return false;
                }

                task.UpdatedAt = DateTime.UtcNow;
                task.Version = expectedVersion + 1;
                
                context.AsyncTasks.Update(task);
                var affected = await context.SaveChangesAsync(cancellationToken);

                if (affected > 0)
                {
                    _logger.LogInformation("Updated task {TaskId} with version check (version {OldVersion} -> {NewVersion})",
                        task.Id, expectedVersion, task.Version);
                }

                return affected > 0;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating task {TaskId} with version check", task.Id);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating task {TaskId} with version check", task.Id);
                throw;
            }
        }
    }
}