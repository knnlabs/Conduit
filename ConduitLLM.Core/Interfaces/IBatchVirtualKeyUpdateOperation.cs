using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for batch virtual key update operations
    /// </summary>
    public interface IBatchVirtualKeyUpdateOperation
    {
        /// <summary>
        /// Execute batch virtual key update operation
        /// </summary>
        /// <param name="updates">List of virtual key updates to process</param>
        /// <param name="adminVirtualKeyId">Admin virtual key ID for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The batch operation result</returns>
        Task<BatchOperationResult> ExecuteAsync(
            List<VirtualKeyUpdateItem> updates,
            int adminVirtualKeyId,
            CancellationToken cancellationToken = default);
    }
}