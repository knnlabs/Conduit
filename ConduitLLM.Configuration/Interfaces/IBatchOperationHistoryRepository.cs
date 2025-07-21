using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Repository interface for batch operation history
    /// </summary>
    public interface IBatchOperationHistoryRepository
    {
        /// <summary>
        /// Saves batch operation history
        /// </summary>
        Task<BatchOperationHistory> SaveAsync(BatchOperationHistory history);

        /// <summary>
        /// Updates batch operation history
        /// </summary>
        Task<BatchOperationHistory?> UpdateAsync(BatchOperationHistory history);

        /// <summary>
        /// Gets batch operation history by ID
        /// </summary>
        Task<BatchOperationHistory?> GetByIdAsync(string operationId);

        /// <summary>
        /// Gets batch operation history for a virtual key
        /// </summary>
        Task<List<BatchOperationHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets recent batch operations
        /// </summary>
        Task<List<BatchOperationHistory>> GetRecentOperationsAsync(int take = 20);

        /// <summary>
        /// Gets resumable operations
        /// </summary>
        Task<List<BatchOperationHistory>> GetResumableOperationsAsync(int virtualKeyId);

        /// <summary>
        /// Deletes old batch operation history
        /// </summary>
        Task<int> DeleteOldHistoryAsync(DateTime olderThan);

        /// <summary>
        /// Gets batch operation statistics for a virtual key
        /// </summary>
        Task<BatchOperationStatistics> GetStatisticsAsync(int virtualKeyId, DateTime? since = null);
    }

    /// <summary>
    /// Statistics for batch operations
    /// </summary>
    public class BatchOperationStatistics
    {
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public int CancelledOperations { get; set; }
        public long TotalItemsProcessed { get; set; }
        public long TotalItemsSucceeded { get; set; }
        public long TotalItemsFailed { get; set; }
        public double AverageItemsPerSecond { get; set; }
        public double AverageDurationSeconds { get; set; }
        public Dictionary<string, int> OperationTypeCounts { get; set; } = new();
    }
}