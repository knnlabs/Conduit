using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Configuration.Interfaces;

namespace ConduitLLM.Configuration.Repositories
{
    /// <summary>
    /// Repository for batch operation history
    /// </summary>
    public class BatchOperationHistoryRepository : IBatchOperationHistoryRepository
    {
        private readonly ConduitDbContext _context;
        private readonly ILogger<BatchOperationHistoryRepository> _logger;

        public BatchOperationHistoryRepository(
            ConduitDbContext context,
            ILogger<BatchOperationHistoryRepository> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BatchOperationHistory> SaveAsync(BatchOperationHistory history)
        {
            try
            {
                _context.BatchOperationHistory.Add(history);
                await _context.SaveChangesAsync();
                
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
                var existing = await _context.BatchOperationHistory
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

                await _context.SaveChangesAsync();
                
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
            return await _context.BatchOperationHistory
                .Include(h => h.VirtualKey)
                .FirstOrDefaultAsync(h => h.OperationId == operationId);
        }

        public async Task<List<BatchOperationHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, int skip = 0, int take = 20)
        {
            return await _context.BatchOperationHistory
                .Where(h => h.VirtualKeyId == virtualKeyId)
                .OrderByDescending(h => h.StartedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<BatchOperationHistory>> GetRecentOperationsAsync(int take = 20)
        {
            return await _context.BatchOperationHistory
                .OrderByDescending(h => h.StartedAt)
                .Take(take)
                .Include(h => h.VirtualKey)
                .ToListAsync();
        }

        public async Task<List<BatchOperationHistory>> GetResumableOperationsAsync(int virtualKeyId)
        {
            return await _context.BatchOperationHistory
                .Where(h => h.VirtualKeyId == virtualKeyId && 
                           h.CanResume && 
                           (h.Status == "Cancelled" || h.Status == "Failed" || h.Status == "PartiallyCompleted"))
                .OrderByDescending(h => h.StartedAt)
                .ToListAsync();
        }

        public async Task<int> DeleteOldHistoryAsync(DateTime olderThan)
        {
            var toDelete = await _context.BatchOperationHistory
                .Where(h => h.StartedAt < olderThan)
                .ToListAsync();
            
            if (toDelete.Count() > 0)
            {
                _context.BatchOperationHistory.RemoveRange(toDelete);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation(
                    "Deleted {Count} batch operation history records older than {Date}",
                    toDelete.Count(), olderThan);
            }
            
            return toDelete.Count();
        }

        public async Task<BatchOperationStatistics> GetStatisticsAsync(int virtualKeyId, DateTime? since = null)
        {
            var query = _context.BatchOperationHistory
                .Where(h => h.VirtualKeyId == virtualKeyId);
            
            if (since.HasValue)
            {
                query = query.Where(h => h.StartedAt >= since.Value);
            }

            var operations = await query.ToListAsync();
            
            if (operations.Count() == 0)
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
            if (completedOps.Count() > 0)
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