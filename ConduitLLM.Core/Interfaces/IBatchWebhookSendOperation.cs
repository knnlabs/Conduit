using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for batch webhook send operations
    /// </summary>
    public interface IBatchWebhookSendOperation
    {
        /// <summary>
        /// Execute batch webhook send operation
        /// </summary>
        /// <param name="webhooks">List of webhooks to send</param>
        /// <param name="virtualKeyId">Virtual key ID for the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The batch operation result</returns>
        Task<BatchOperationResult> ExecuteAsync(
            List<WebhookSendItem> webhooks,
            int virtualKeyId,
            CancellationToken cancellationToken = default);
    }
}