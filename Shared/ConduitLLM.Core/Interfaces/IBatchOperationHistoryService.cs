using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing batch operation history
    /// </summary>
    public interface IBatchOperationHistoryService
    {
        /// <summary>
        /// Records the start of a batch operation
        /// </summary>
        Task RecordOperationStartAsync(
            string operationId,
            string operationType,
            int virtualKeyId,
            int totalItems,
            BatchOperationOptions options);

        /// <summary>
        /// Records the completion of a batch operation
        /// </summary>
        Task RecordOperationCompletionAsync(
            string operationId,
            BatchOperationResult result);

        /// <summary>
        /// Updates checkpoint data for resumable operations
        /// </summary>
        Task UpdateCheckpointAsync(
            string operationId,
            int lastProcessedIndex,
            object checkpointData);

        /// <summary>
        /// Gets operation history for resumption
        /// </summary>
        Task<BatchOperationResumptionData?> GetResumptionDataAsync(string operationId);
    }

    /// <summary>
    /// Data needed to resume a batch operation
    /// </summary>
    public class BatchOperationResumptionData
    {
        public string OperationId { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public int LastProcessedIndex { get; set; }
        public string? CheckpointData { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime StartedAt { get; set; }
    }
}