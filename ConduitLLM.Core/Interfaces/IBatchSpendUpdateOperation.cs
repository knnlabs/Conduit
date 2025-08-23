using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for batch spend update operations
    /// </summary>
    public interface IBatchSpendUpdateOperation
    {
        /// <summary>
        /// Execute batch spend update operation
        /// </summary>
        /// <param name="spendUpdates">List of spend updates to process</param>
        /// <param name="virtualKeyId">Virtual key ID for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The batch operation result</returns>
        Task<BatchOperationResult> ExecuteAsync(
            List<SpendUpdateItem> spendUpdates,
            int virtualKeyId,
            CancellationToken cancellationToken = default);
    }
}