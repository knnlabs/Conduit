namespace ConduitLLM.Configuration.Interfaces
{
    /// <summary>
    /// Interface for batch spend update service
    /// </summary>
    public interface IBatchSpendUpdateService
    {
        /// <summary>
        /// Gets whether the service is healthy and able to accept updates
        /// </summary>
        bool IsHealthy { get; }

        /// <summary>
        /// Event raised after successful batch spend updates with the key hashes that were updated
        /// </summary>
        event Action<string[]>? SpendUpdatesCompleted;

        /// <summary>
        /// Queues a spend update to Redis for batch processing.
        /// Throws on failure so the caller can fall back to alternative paths.
        /// </summary>
        Task QueueSpendUpdateAsync(int virtualKeyId, decimal cost);

        /// <summary>
        /// Queues a spend update to an in-memory fallback queue.
        /// Used as a last resort when both Redis and direct DB writes fail.
        /// Updates are drained on the next successful flush cycle.
        /// </summary>
        void QueueFallbackUpdate(int virtualKeyId, decimal cost);
    }
}