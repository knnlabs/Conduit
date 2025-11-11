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
        /// Queues a spend update to be processed in the next batch
        /// </summary>
        void QueueSpendUpdate(int virtualKeyId, decimal cost);
    }
}