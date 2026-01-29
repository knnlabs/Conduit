using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Utilities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository implementation for managing async tasks.
    /// Extends RepositoryBase for standard CRUD operations.
    /// </summary>
    public class AsyncTaskRepository : RepositoryBase<AsyncTask, string>, IAsyncTaskRepository
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTaskRepository"/> class.
        /// </summary>
        /// <param name="dbContextFactory">The database context factory.</param>
        /// <param name="logger">The logger instance.</param>
        public AsyncTaskRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<AsyncTaskRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        /// <inheritdoc/>
        protected override DbSet<AsyncTask> GetDbSet(ConduitDbContext context)
        {
            return context.AsyncTasks;
        }

        /// <inheritdoc/>
        protected override IQueryable<AsyncTask> ApplyDefaultOrdering(IQueryable<AsyncTask> query)
        {
            return query.OrderByDescending(t => t.CreatedAt);
        }

        /// <inheritdoc/>
        public override async Task<string> CreateAsync(AsyncTask entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var taskId = await base.CreateAsync(entity, cancellationToken);

                Logger.LogInformation("Created async task: {TaskId} of type {TaskType} for virtual key {VirtualKeyId}",
                    entity.Id, entity.Type, entity.VirtualKeyId);

                return taskId;
            }
            catch (DbUpdateException ex)
            {
                Logger.LogError(ex, "Database error creating async task: {Task}",
                    LoggingSanitizer.S(entity));
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating async task: {Task}",
                    LoggingSanitizer.S(entity));
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> UpdateAsync(AsyncTask entity, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entity);

            try
            {
                var result = await base.UpdateAsync(entity, cancellationToken);

                if (result)
                {
                    Logger.LogInformation("Updated async task: {TaskId} with state {State}",
                        entity.Id, entity.State);
                }
                else
                {
                    Logger.LogWarning("No rows affected when updating async task: {TaskId}", entity.Id);
                }

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Logger.LogWarning(ex, "Concurrency conflict updating async task: {TaskId}", entity.Id);
                return false;
            }
            catch (DbUpdateException ex)
            {
                Logger.LogError(ex, "Database error updating async task: {TaskId}", entity.Id);
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating async task: {TaskId}", entity.Id);
                throw;
            }
        }

        /// <inheritdoc/>
        public override async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            try
            {
                var result = await base.DeleteAsync(id, cancellationToken);

                if (result)
                {
                    Logger.LogInformation("Deleted async task: {TaskId}", id);
                }

                return result;
            }
            catch (DbUpdateException ex)
            {
                Logger.LogError(ex, "Database error deleting async task: {TaskId}", id);
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error deleting async task: {TaskId}", id);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    return await context.AsyncTasks
                        .AsNoTracking()
                        .Where(t => t.VirtualKeyId == virtualKeyId)
                        .OrderByDescending(t => t.CreatedAt)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting async tasks by virtual key ID: {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetActiveByVirtualKeyAsync(int virtualKeyId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    return await context.AsyncTasks
                        .AsNoTracking()
                        .Where(t => t.VirtualKeyId == virtualKeyId && !t.IsArchived)
                        .OrderByDescending(t => t.CreatedAt)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting active async tasks by virtual key ID: {VirtualKeyId}", virtualKeyId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> ArchiveOldTasksAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
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
                        Logger.LogInformation("Archived {Count} completed tasks older than {OlderThan}",
                            affected, olderThan);
                    }

                    return affected;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error archiving old tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetTasksForCleanupAsync(TimeSpan archivedOlderThan, int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
                    var cutoffDate = DateTime.UtcNow.Subtract(archivedOlderThan);

                    return await context.AsyncTasks
                        .AsNoTracking()
                        .Where(t => t.IsArchived && t.ArchivedAt.HasValue && t.ArchivedAt.Value < cutoffDate)
                        .OrderBy(t => t.ArchivedAt)
                        .Take(limit)
                        .ToListAsync(cancellationToken);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting tasks for cleanup");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<int> BulkDeleteAsync(IEnumerable<string> taskIds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(taskIds);

            var taskIdList = taskIds.ToList();
            if (taskIdList.Count == 0)
            {
                return 0;
            }

            try
            {
                return await ExecuteAsync(async context =>
                {
                    var tasksToDelete = await context.AsyncTasks
                        .Where(t => taskIdList.Contains(t.Id))
                        .ToListAsync(cancellationToken);

                    context.AsyncTasks.RemoveRange(tasksToDelete);
                    var affected = await context.SaveChangesAsync(cancellationToken);

                    if (affected > 0)
                    {
                        Logger.LogInformation("Bulk deleted {Count} async tasks", affected);
                    }

                    return affected;
                }, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                Logger.LogError(ex, "Database error bulk deleting async tasks");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error bulk deleting async tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetPendingTasksAsync(string? taskType = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
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
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting pending tasks");
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
                return await ExecuteAsync(async context =>
                {
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

                        Logger.LogInformation("Worker {WorkerId} leased task {TaskId} until {ExpiryTime}",
                            workerId, task.Id, task.LeaseExpiryTime);
                    }

                    return task;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error leasing next pending task for worker {WorkerId}", workerId);
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
                return await ExecuteAsync(async context =>
                {
                    var task = await context.AsyncTasks
                        .FirstOrDefaultAsync(t => t.Id == taskId && t.LeasedBy == workerId, cancellationToken);

                    if (task == null)
                    {
                        Logger.LogWarning("Task {TaskId} not found or not leased by worker {WorkerId}", taskId, workerId);
                        return false;
                    }

                    task.LeasedBy = null;
                    task.LeaseExpiryTime = null;
                    task.UpdatedAt = DateTime.UtcNow;
                    task.Version++;

                    var affected = await context.SaveChangesAsync(cancellationToken);

                    if (affected > 0)
                    {
                        Logger.LogInformation("Released lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
                    }

                    return affected > 0;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error releasing lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
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
                return await ExecuteAsync(async context =>
                {
                    var now = DateTime.UtcNow;
                    var task = await context.AsyncTasks
                        .FirstOrDefaultAsync(t => t.Id == taskId && t.LeasedBy == workerId &&
                                                 t.LeaseExpiryTime != null && t.LeaseExpiryTime > now,
                                                 cancellationToken);

                    if (task == null)
                    {
                        Logger.LogWarning("Task {TaskId} not found, not leased by worker {WorkerId}, or lease expired",
                            taskId, workerId);
                        return false;
                    }

                    task.LeaseExpiryTime = now.Add(extension);
                    task.UpdatedAt = now;
                    task.Version++;

                    var affected = await context.SaveChangesAsync(cancellationToken);

                    if (affected > 0)
                    {
                        Logger.LogInformation("Extended lease on task {TaskId} by worker {WorkerId} until {ExpiryTime}",
                            taskId, workerId, task.LeaseExpiryTime);
                    }

                    return affected > 0;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error extending lease on task {TaskId} by worker {WorkerId}", taskId, workerId);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<List<AsyncTask>> GetExpiredLeaseTasksAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                return await ExecuteAsync(async context =>
                {
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
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting expired lease tasks");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> UpdateWithVersionCheckAsync(AsyncTask task, int expectedVersion, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(task);

            try
            {
                return await ExecuteAsync(async context =>
                {
                    // Check version before updating
                    var currentVersion = await context.AsyncTasks
                        .Where(t => t.Id == task.Id)
                        .Select(t => t.Version)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (currentVersion != expectedVersion)
                    {
                        Logger.LogWarning("Version mismatch for task {TaskId}. Expected {ExpectedVersion}, found {CurrentVersion}",
                            task.Id, expectedVersion, currentVersion);
                        return false;
                    }

                    task.UpdatedAt = DateTime.UtcNow;
                    task.Version = expectedVersion + 1;

                    context.AsyncTasks.Update(task);
                    var affected = await context.SaveChangesAsync(cancellationToken);

                    if (affected > 0)
                    {
                        Logger.LogInformation("Updated task {TaskId} with version check (version {OldVersion} -> {NewVersion})",
                            task.Id, expectedVersion, task.Version);
                    }

                    return affected > 0;
                }, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                Logger.LogWarning(ex, "Concurrency conflict updating task {TaskId} with version check", task.Id);
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error updating task {TaskId} with version check", task.Id);
                throw;
            }
        }
    }
}
