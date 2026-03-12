using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for batch operation history.
    /// Uses IDbContextFactory for short-lived contexts, consistent with other repositories.
    /// </summary>
    public class BatchOperationHistoryRepository : IBatchOperationHistoryRepository
    {
        private readonly IDbContextFactory<ConduitDbContext> _dbContextFactory;
        private readonly ILogger<BatchOperationHistoryRepository> _logger;

        public BatchOperationHistoryRepository(
            IDbContextFactory<ConduitDbContext> dbContextFactory,
            ILogger<BatchOperationHistoryRepository> logger)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BatchOperationHistory> SaveAsync(BatchOperationHistory history)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                context.BatchOperationHistory.Add(history);
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Saved batch operation history for {OperationId} - Type: {OperationType}, Status: {Status}",
                    history.OperationId, history.OperationType, history.Status);

                return history;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving batch operation history for {OperationId}", history.OperationId);
                throw;
            }
        }

        public async Task<BatchOperationHistory?> UpdateAsync(BatchOperationHistory history)
        {
            try
            {
                using var context = await _dbContextFactory.CreateDbContextAsync();
                var existing = await context.BatchOperationHistory
                    .FirstOrDefaultAsync(h => h.OperationId == history.OperationId);

                if (existing == null)
                {
                    _logger.LogWarning("Batch operation history not found for update: {OperationId}", history.OperationId);
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

                _logger.LogInformation(
                    "Updated batch operation history for {OperationId} - Status: {Status}",
                    history.OperationId, history.Status);

                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating batch operation history for {OperationId}", history.OperationId);
                throw;
            }
        }

        public async Task<BatchOperationHistory?> GetByIdAsync(string operationId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.BatchOperationHistory
                .Include(h => h.VirtualKey)
                .AsNoTracking()
                .FirstOrDefaultAsync(h => h.OperationId == operationId);
        }

        public async Task<List<BatchOperationHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, int skip = 0, int take = 20)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.BatchOperationHistory
                .AsNoTracking()
                .Where(h => h.VirtualKeyId == virtualKeyId)
                .OrderByDescending(h => h.StartedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<BatchOperationHistory>> GetRecentOperationsAsync(int take = 20)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.BatchOperationHistory
                .Include(h => h.VirtualKey)
                .AsNoTracking()
                .OrderByDescending(h => h.StartedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<BatchOperationHistory>> GetResumableOperationsAsync(int virtualKeyId)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.BatchOperationHistory
                .AsNoTracking()
                .Where(h => h.VirtualKeyId == virtualKeyId &&
                           h.CanResume &&
                           (h.Status == "Cancelled" || h.Status == "Failed" || h.Status == "PartiallyCompleted"))
                .OrderByDescending(h => h.StartedAt)
                .ToListAsync();
        }

        public async Task<int> DeleteOldHistoryAsync(DateTime olderThan)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var toDelete = await context.BatchOperationHistory
                .Where(h => h.StartedAt < olderThan)
                .ToListAsync();

            if (toDelete.Any())
            {
                context.BatchOperationHistory.RemoveRange(toDelete);
                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Deleted {Count} batch operation history records older than {Date}",
                    toDelete.Count(), olderThan);
            }

            return toDelete.Count();
        }

        public async Task<BatchOperationStatistics> GetStatisticsAsync(int virtualKeyId, DateTime? since = null)
        {
            using var context = await _dbContextFactory.CreateDbContextAsync();
            var query = context.BatchOperationHistory
                .Where(h => h.VirtualKeyId == virtualKeyId);

            if (since.HasValue)
            {
                query = query.Where(h => h.StartedAt >= since.Value);
            }

            var operations = await query.ToListAsync();

            if (!operations.Any())
            {
                return new BatchOperationStatistics();
            }

            var stats = new BatchOperationStatistics
            {
                TotalOperations = operations.Count(),
                SuccessfulOperations = operations.Count(h => h.Status == "Completed"),
                FailedOperations = operations.Count(h => h.Status == "Failed"),
                CancelledOperations = operations.Count(h => h.Status == "Cancelled"),
                TotalItemsProcessed = operations.Sum(h => h.SuccessCount + h.FailedCount),
                TotalItemsSucceeded = operations.Sum(h => h.SuccessCount),
                TotalItemsFailed = operations.Sum(h => h.FailedCount)
            };

            // Calculate averages only for completed operations
            var completedOps = operations.Where(h => h.DurationSeconds.HasValue && h.ItemsPerSecond.HasValue).ToList();
            if (completedOps.Any())
            {
                stats.AverageDurationSeconds = completedOps.Average(h => h.DurationSeconds!.Value);
                stats.AverageItemsPerSecond = completedOps.Average(h => h.ItemsPerSecond!.Value);
            }

            // Count by operation type
            stats.OperationTypeCounts = operations
                .GroupBy(h => h.OperationType)
                .ToDictionary(g => g.Key, g => g.Count());

            return stats;
        }
    }
}
