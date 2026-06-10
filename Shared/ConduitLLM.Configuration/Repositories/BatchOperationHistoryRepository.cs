using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for batch operation history.
    /// Inherits common CRUD operations from RepositoryBase.
    /// </summary>
    public class BatchOperationHistoryRepository : RepositoryBase<BatchOperationHistory, string>, IBatchOperationHistoryRepository
    {
        public BatchOperationHistoryRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<BatchOperationHistoryRepository> logger)
            : base(dbContextFactory, logger)
        {
        }

        /// <inheritdoc/>
        protected override DbSet<BatchOperationHistory> GetDbSet(ConduitDbContext context) => context.BatchOperationHistory;

        /// <inheritdoc/>
        protected override IQueryable<BatchOperationHistory> ApplyDefaultIncludes(IQueryable<BatchOperationHistory> query)
        {
            return query.Include(h => h.VirtualKey);
        }

        /// <inheritdoc/>
        protected override IQueryable<BatchOperationHistory> ApplyDefaultOrdering(IQueryable<BatchOperationHistory> query)
        {
            return query.OrderByDescending(h => h.StartedAt);
        }

        /// <inheritdoc/>
        public async Task<BatchOperationHistory> SaveAsync(BatchOperationHistory history)
        {
            return await ExecuteAsync(async context =>
            {
                GetDbSet(context).Add(history);
                await context.SaveChangesAsync();

                Logger.LogInformation(
                    "Saved batch operation history for {OperationId} - Type: {OperationType}, Status: {Status}",
                    history.OperationId, history.OperationType, history.Status);

                return history;
            }, operationName: "saving");
        }

        /// <summary>
        /// Updates an existing batch operation history record by looking up the existing record
        /// via OperationId, applying field-level changes, and saving.
        /// </summary>
        /// <remarks>
        /// This is an explicit interface implementation because it differs from the base
        /// <see cref="RepositoryBase{TEntity,TKey}.UpdateAsync"/> — it applies selective
        /// field updates and returns the updated entity (or null if not found).
        /// </remarks>
        async Task<BatchOperationHistory?> IBatchOperationHistoryRepository.UpdateAsync(BatchOperationHistory history)
        {
            return await ExecuteAsync(async context =>
            {
                var existing = await GetDbSet(context)
                    .FirstOrDefaultAsync(h => h.OperationId == history.OperationId);

                if (existing == null)
                {
                    Logger.LogWarning("Batch operation history not found for update: {OperationId}", history.OperationId);
                    return null;
                }

                // Update fields
                existing.SuccessCount = history.SuccessCount;
                existing.FailedCount = history.FailedCount;
                existing.Status = history.Status;
                existing.CompletedAt = history.CompletedAt;
                existing.DurationSeconds = history.DurationSeconds;
                existing.ItemsPerSecond = history.ItemsPerSecond;
                existing.ErrorMessage = history.ErrorMessage;
                existing.CancellationReason = history.CancellationReason;
                existing.ErrorDetails = history.ErrorDetails;
                existing.ResultSummary = history.ResultSummary;
                existing.CheckpointData = history.CheckpointData;
                existing.LastProcessedIndex = history.LastProcessedIndex;

                await context.SaveChangesAsync();

                Logger.LogInformation(
                    "Updated batch operation history for {OperationId} - Status: {Status}",
                    history.OperationId, history.Status);

                return existing;
            }, operationName: "updating");
        }

        /// <summary>
        /// Gets a batch operation history by its operation ID.
        /// Overrides the base implementation because the primary key property (OperationId)
        /// differs from the IEntity.Id alias, which is [NotMapped] and cannot be used in LINQ-to-SQL.
        /// </summary>
        public override async Task<BatchOperationHistory?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                return await query.FirstOrDefaultAsync(e => e.OperationId == id, cancellationToken);
            }, cancellationToken, $"getting by ID {id}");
        }

        /// <summary>
        /// Explicit interface implementation for <see cref="IBatchOperationHistoryRepository.GetByIdAsync(string)"/>
        /// which lacks a CancellationToken parameter.
        /// </summary>
        async Task<BatchOperationHistory?> IBatchOperationHistoryRepository.GetByIdAsync(string operationId)
        {
            return await GetByIdAsync(operationId);
        }

        /// <summary>
        /// Overrides base implementation to use OperationId (the mapped PK property)
        /// instead of Id (the [NotMapped] alias) in LINQ queries.
        /// </summary>
        public override async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
        {
            return await ExecuteAsync(async context =>
                await GetDbSet(context)
                    .AsNoTracking()
                    .AnyAsync(e => e.OperationId == id, cancellationToken),
                cancellationToken, $"checking existence of ID {id}");
        }

        /// <inheritdoc/>
        public async Task<List<BatchOperationHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, int skip = 0, int take = 20)
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId)
                    .OrderByDescending(h => h.StartedAt)
                    .Skip(skip)
                    .Take(take)
                    .ToListAsync();
            }, operationName: "getting by virtual key ID");
        }

        /// <inheritdoc/>
        public async Task<List<BatchOperationHistory>> GetRecentOperationsAsync(int take = 20)
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context).AsNoTracking();
                query = ApplyDefaultIncludes(query);
                return await query
                    .OrderByDescending(h => h.StartedAt)
                    .Take(take)
                    .ToListAsync();
            }, operationName: "getting recent operations");
        }

        /// <inheritdoc/>
        public async Task<List<BatchOperationHistory>> GetResumableOperationsAsync(int virtualKeyId)
        {
            return await ExecuteAsync(async context =>
            {
                return await GetDbSet(context)
                    .AsNoTracking()
                    .Where(h => h.VirtualKeyId == virtualKeyId &&
                               h.CanResume &&
                               (h.Status == "Cancelled" || h.Status == "Failed" || h.Status == "PartiallyCompleted"))
                    .OrderByDescending(h => h.StartedAt)
                    .ToListAsync();
            }, operationName: "getting resumable operations");
        }

        /// <inheritdoc/>
        public async Task<int> DeleteOldHistoryAsync(DateTime olderThan)
        {
            return await ExecuteAsync(async context =>
            {
                var toDelete = await GetDbSet(context)
                    .Where(h => h.StartedAt < olderThan)
                    .ToListAsync();

                if (toDelete.Any())
                {
                    GetDbSet(context).RemoveRange(toDelete);
                    await context.SaveChangesAsync();

                    Logger.LogInformation(
                        "Deleted {Count} batch operation history records older than {Date}",
                        toDelete.Count, olderThan);
                }

                return toDelete.Count;
            }, operationName: "deleting old history");
        }

        /// <inheritdoc/>
        public async Task<BatchOperationStatistics> GetStatisticsAsync(int virtualKeyId, DateTime? since = null)
        {
            return await ExecuteAsync(async context =>
            {
                var query = GetDbSet(context)
                    .Where(h => h.VirtualKeyId == virtualKeyId);

                if (since.HasValue)
                {
                    query = query.Where(h => h.StartedAt >= since.Value);
                }

                // Aggregate in the database rather than loading every row into memory
                var summary = await query
                    .GroupBy(h => 1)
                    .Select(g => new
                    {
                        TotalOperations = g.Count(),
                        SuccessfulOperations = g.Count(h => h.Status == "Completed"),
                        FailedOperations = g.Count(h => h.Status == "Failed"),
                        CancelledOperations = g.Count(h => h.Status == "Cancelled"),
                        TotalItemsSucceeded = g.Sum(h => (long)h.SuccessCount),
                        TotalItemsFailed = g.Sum(h => (long)h.FailedCount),
                        // AVG ignores NULLs, so gate both columns on the same completeness
                        // condition the original in-memory filter used
                        AverageDurationSeconds = g.Average(h =>
                            h.DurationSeconds.HasValue && h.ItemsPerSecond.HasValue ? h.DurationSeconds : null),
                        AverageItemsPerSecond = g.Average(h =>
                            h.DurationSeconds.HasValue && h.ItemsPerSecond.HasValue ? h.ItemsPerSecond : null)
                    })
                    .FirstOrDefaultAsync();

                if (summary == null)
                {
                    return new BatchOperationStatistics();
                }

                var operationTypeCounts = await query
                    .GroupBy(h => h.OperationType)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .ToListAsync();

                return new BatchOperationStatistics
                {
                    TotalOperations = summary.TotalOperations,
                    SuccessfulOperations = summary.SuccessfulOperations,
                    FailedOperations = summary.FailedOperations,
                    CancelledOperations = summary.CancelledOperations,
                    TotalItemsProcessed = summary.TotalItemsSucceeded + summary.TotalItemsFailed,
                    TotalItemsSucceeded = summary.TotalItemsSucceeded,
                    TotalItemsFailed = summary.TotalItemsFailed,
                    AverageDurationSeconds = summary.AverageDurationSeconds ?? 0,
                    AverageItemsPerSecond = summary.AverageItemsPerSecond ?? 0,
                    OperationTypeCounts = operationTypeCounts.ToDictionary(g => g.Key, g => g.Count)
                };
            }, operationName: "getting statistics");
        }
    }
}
