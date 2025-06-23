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
                
                var query = context.AsyncTasks
                    .AsNoTracking()
                    .Where(t => t.State == 0 && !t.IsArchived); // 0 = Pending state

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
    }
}