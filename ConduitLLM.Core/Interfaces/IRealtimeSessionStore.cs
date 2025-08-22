using ConduitLLM.Core.Models.Audio;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for storing and managing real-time audio session state.
    /// </summary>
    public interface IRealtimeSessionStore
    {
        /// <summary>
        /// Stores a new session.
        /// </summary>
        /// <param name="session">The session to store.</param>
        /// <param name="ttl">Time to live for the session data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StoreSessionAsync(
            RealtimeSession session,
            TimeSpan? ttl = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a session by ID.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The session if found, null otherwise.</returns>
        Task<RealtimeSession?> GetSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing session.
        /// </summary>
        /// <param name="session">The updated session.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateSessionAsync(
            RealtimeSession session,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes a session from storage.
        /// </summary>
        /// <param name="sessionId">The session ID to remove.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<bool> RemoveSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all active sessions.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of active sessions.</returns>
        Task<List<RealtimeSession>> GetActiveSessionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets sessions by virtual key.
        /// </summary>
        /// <param name="virtualKey">The virtual key to filter by.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of sessions for the virtual key.</returns>
        Task<List<RealtimeSession>> GetSessionsByVirtualKeyAsync(
            string virtualKey,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates session metrics.
        /// </summary>
        /// <param name="sessionId">The session ID.</param>
        /// <param name="metrics">Updated metrics.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpdateSessionMetricsAsync(
            string sessionId,
            SessionStatistics metrics,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs cleanup of expired sessions.
        /// </summary>
        /// <param name="maxAge">Maximum age for sessions before cleanup.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Number of sessions cleaned up.</returns>
        Task<int> CleanupExpiredSessionsAsync(
            TimeSpan maxAge,
            CancellationToken cancellationToken = default);
    }
}
