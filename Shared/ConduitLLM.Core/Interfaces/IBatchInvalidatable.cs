namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Interface for caches that support batch invalidation
    /// </summary>
    public interface IBatchInvalidatable
    {
        /// <summary>
        /// Invalidate multiple cache entries in a batch
        /// </summary>
        Task<BatchInvalidationResult> InvalidateBatchAsync(
            IEnumerable<InvalidationRequest> requests,
            CancellationToken cancellationToken = default);
    }
}
