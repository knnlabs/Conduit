using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Defines the contract for a repository that manages the history of batch operations.
    /// </summary>
    /// <remarks>
    /// This interface abstracts the data access layer for batch operation history, allowing for different
    /// implementations (e.g., in-memory, database, etc.). It is used to track the progress and outcome
    /// of long-running, asynchronous tasks that process multiple items in a batch. This is crucial for
    /// monitoring, debugging, and ensuring the reliability of batch processing jobs.
    /// </remarks>
    public interface IBatchOperationHistoryRepository
    {
        /// <summary>
        /// Saves a new batch operation history record to the data store.
        /// </summary>
        /// <param name="history">The batch operation history to save.</param>
        /// <returns>The saved batch operation history, including any updates from the data store (e.g., generated ID).</returns>
        Task<BatchOperationHistory> SaveAsync(BatchOperationHistory history);

        /// <summary>
        /// Updates an existing batch operation history record.
        /// </summary>
        /// <param name="history">The batch operation history with updated information.</param>
        /// <returns>The updated batch operation history, or null if the record was not found.</returns>
        Task<BatchOperationHistory?> UpdateAsync(BatchOperationHistory history);

        /// <summary>
        /// Retrieves a batch operation history record by its unique identifier.
        /// </summary>
        /// <param name="operationId">The unique ID of the batch operation.</param>
        /// <returns>The batch operation history, or null if not found.</returns>
        Task<BatchOperationHistory?> GetByIdAsync(string operationId);

        /// <summary>
        /// Retrieves a paginated list of batch operation histories for a specific virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key associated with the operations.</param>
        /// <param name="skip">The number of records to skip for pagination.</param>
        /// <param name="take">The number of records to take for pagination.</param>
        /// <returns>A list of batch operation histories.</returns>
        Task<List<BatchOperationHistory>> GetByVirtualKeyIdAsync(int virtualKeyId, int skip = 0, int take = 20);

        /// <summary>
        /// Gets a list of the most recent batch operations across all virtual keys.
        /// </summary>
        /// <param name="take">The maximum number of recent operations to retrieve.</param>
        /// <returns>A list of recent batch operation histories.</returns>
        Task<List<BatchOperationHistory>> GetRecentOperationsAsync(int take = 20);

        /// <summary>
        /// Retrieves a list of operations that were interrupted and can be resumed.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key for which to find resumable operations.</param>
        /// <returns>A list of batch operation histories that are in a resumable state.</returns>
        Task<List<BatchOperationHistory>> GetResumableOperationsAsync(int virtualKeyId);

        /// <summary>
        /// Deletes batch operation history records that are older than a specified date.
        /// </summary>
        /// <param name="olderThan">The date threshold; records older than this will be deleted.</param>
        /// <returns>The number of records deleted.</returns>
        Task<int> DeleteOldHistoryAsync(DateTime olderThan);

        /// <summary>
        /// Calculates and retrieves statistics for batch operations associated with a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The ID of the virtual key.</param>
        /// <param name="since">An optional date to restrict the statistics to operations that occurred after this time.</param>
        /// <returns>An object containing statistics about the batch operations.</returns>
        Task<BatchOperationStatistics> GetStatisticsAsync(int virtualKeyId, DateTime? since = null);
    }

    /// <summary>
    /// Holds statistical data about batch operations.
    /// </summary>
    public class BatchOperationStatistics
    {
        /// <summary>Gets or sets the total number of operations.</summary>
        public int TotalOperations { get; set; }
        /// <summary>Gets or sets the number of successful operations.</summary>
        public int SuccessfulOperations { get; set; }
        /// <summary>Gets or sets the number of failed operations.</summary>
        public int FailedOperations { get; set; }
        /// <summary>Gets or sets the number of cancelled operations.</summary>
        public int CancelledOperations { get; set; }
        /// <summary>Gets or sets the total number of items processed across all operations.</summary>
        public long TotalItemsProcessed { get; set; }
        /// <summary>Gets or sets the total number of items that succeeded.</summary>
        public long TotalItemsSucceeded { get; set; }
        /// <summary>Gets or sets the total number of items that failed.</summary>
        public long TotalItemsFailed { get; set; }
        /// <summary>Gets or sets the average number of items processed per second.</summary>
        public double AverageItemsPerSecond { get; set; }
        /// <summary>Gets or sets the average duration of operations in seconds.</summary>
        public double AverageDurationSeconds { get; set; }
        /// <summary>Gets or sets a dictionary with counts of each operation type.</summary>
        public Dictionary<string, int> OperationTypeCounts { get; set; } = new();
    }
}