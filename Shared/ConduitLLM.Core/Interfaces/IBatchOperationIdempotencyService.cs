namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Service for managing idempotency tokens in batch operations.
    /// Prevents duplicate processing of batch operations using Redis-based tracking.
    /// </summary>
    public interface IBatchOperationIdempotencyService
    {
        /// <summary>
        /// Checks if an operation with the given token has already been processed
        /// </summary>
        /// <param name="idempotencyToken">Unique token identifying the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the operation was already processed</returns>
        Task<bool> IsOperationProcessedAsync(string idempotencyToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores the result of a batch operation for idempotency checking
        /// </summary>
        /// <param name="idempotencyToken">Unique token identifying the operation</param>
        /// <param name="result">The operation result to cache</param>
        /// <param name="ttl">Time-to-live for the cached result (default 24 hours)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StoreOperationResultAsync(
            string idempotencyToken,
            object result,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves the cached result of a previously processed operation
        /// </summary>
        /// <typeparam name="T">Type of the result</typeparam>
        /// <param name="idempotencyToken">Unique token identifying the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The cached result, or null if not found</returns>
        Task<T?> GetOperationResultAsync<T>(string idempotencyToken, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Generates a unique idempotency token based on operation parameters
        /// </summary>
        /// <param name="operationType">Type of batch operation</param>
        /// <param name="parameters">Operation parameters to include in hash</param>
        /// <returns>Unique idempotency token</returns>
        string GenerateToken(string operationType, params object[] parameters);

        /// <summary>
        /// Removes the cached result for an idempotency token
        /// </summary>
        /// <param name="idempotencyToken">Token to remove</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InvalidateTokenAsync(string idempotencyToken, CancellationToken cancellationToken = default);
    }
}
