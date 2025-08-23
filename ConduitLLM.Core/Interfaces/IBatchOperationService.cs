using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing and tracking batch operations with real-time progress updates.
    /// Supports cancellation, resumption, and detailed progress tracking for bulk operations.
    /// </summary>
    public interface IBatchOperationService
    {
        /// <summary>
        /// Starts a new batch operation with progress tracking
        /// </summary>
        /// <typeparam name="T">Type of items being processed</typeparam>
        /// <param name="operationType">Type of operation (e.g., "spend_update", "virtual_key_update")</param>
        /// <param name="items">Items to process</param>
        /// <param name="processItemFunc">Function to process each item</param>
        /// <param name="options">Operation options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the batch operation</returns>
        Task<BatchOperationResult> StartBatchOperationAsync<T>(
            string operationType,
            IEnumerable<T> items,
            Func<T, CancellationToken, Task<BatchItemResult>> processItemFunc,
            BatchOperationOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels an active batch operation
        /// </summary>
        /// <param name="operationId">ID of the operation to cancel</param>
        /// <returns>True if successfully cancelled, false otherwise</returns>
        Task<bool> CancelBatchOperationAsync(string operationId);

        /// <summary>
        /// Gets the current status of a batch operation
        /// </summary>
        /// <param name="operationId">ID of the operation</param>
        /// <returns>Current status or null if operation not found</returns>
        BatchOperationStatus? GetOperationStatus(string operationId);
    }
}