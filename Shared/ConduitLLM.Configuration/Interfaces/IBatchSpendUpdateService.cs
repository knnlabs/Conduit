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
        /// Gets spend that has not yet been reflected in the group's database balance,
        /// including active reservations.
        /// </summary>
        Task<decimal> GetPendingSpendAsync(int virtualKeyId);

        /// <summary>
        /// Atomically reserves part of a virtual key group's available balance.
        /// </summary>
        Task<bool> TryReserveSpendAsync(int virtualKeyId, decimal amount, string reservationId);

        /// <summary>
        /// Releases a previously-created spend reservation.
        /// </summary>
        Task ReleaseSpendReservationAsync(int virtualKeyId, string reservationId);

        /// <summary>
        /// Queues a spend update to an in-memory fallback queue.
        /// Used as a last resort when both Redis and direct DB writes fail.
        /// Updates are drained on the next successful flush cycle.
        /// </summary>
        void QueueFallbackUpdate(int virtualKeyId, decimal cost);
    }
}
