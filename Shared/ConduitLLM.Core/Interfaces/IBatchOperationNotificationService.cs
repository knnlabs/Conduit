using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for sending real-time notifications about batch operation progress
    /// </summary>
    public interface IBatchOperationNotificationService
    {
        /// <summary>
        /// Notifies that a batch operation has started
        /// </summary>
        Task NotifyBatchOperationStartedAsync(
            string operationId,
            string operationType,
            int totalItems,
            int virtualKeyId,
            BatchOperationOptions options);

        /// <summary>
        /// Notifies progress update for a batch operation
        /// </summary>
        Task NotifyBatchOperationProgressAsync(
            string operationId,
            int processedCount,
            int successCount,
            int failedCount,
            double itemsPerSecond,
            TimeSpan elapsedTime,
            TimeSpan estimatedTimeRemaining,
            string? currentItem = null,
            string? message = null);

        /// <summary>
        /// Notifies that a batch operation has completed
        /// </summary>
        Task NotifyBatchOperationCompletedAsync(
            string operationId,
            string operationType,
            BatchOperationStatusEnum status,
            int totalItems,
            int successCount,
            int failedCount,
            TimeSpan duration,
            double averageItemsPerSecond,
            object? resultSummary = null);

        /// <summary>
        /// Notifies that a batch operation has failed
        /// </summary>
        Task NotifyBatchOperationFailedAsync(
            string operationId,
            string operationType,
            string error,
            bool isRetryable,
            int processedCount,
            int failedCount,
            string? stackTrace = null);

        /// <summary>
        /// Notifies that a batch operation has been cancelled
        /// </summary>
        Task NotifyBatchOperationCancelledAsync(
            string operationId,
            string operationType,
            string? reason,
            int processedCount,
            int remainingCount,
            bool canResume);
    }
}