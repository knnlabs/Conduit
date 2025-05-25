using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using ConduitLLM.Core.Models.Realtime;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Manages active real-time WebSocket connections.
    /// </summary>
    /// <remarks>
    /// This service tracks all active connections, enforces connection limits,
    /// and provides connection lifecycle management.
    /// </remarks>
    public interface IRealtimeConnectionManager
    {
        /// <summary>
        /// Registers a new WebSocket connection.
        /// </summary>
        /// <param name="connectionId">Unique identifier for the connection.</param>
        /// <param name="virtualKeyId">The virtual key ID associated with this connection.</param>
        /// <param name="model">The model being used.</param>
        /// <param name="webSocket">The WebSocket instance.</param>
        /// <returns>A task that completes when registration is done.</returns>
        /// <exception cref="InvalidOperationException">Thrown when connection limit is exceeded.</exception>
        Task RegisterConnectionAsync(
            string connectionId, 
            int virtualKeyId, 
            string model,
            WebSocket webSocket);

        /// <summary>
        /// Unregisters a connection when it closes.
        /// </summary>
        /// <param name="connectionId">The connection ID to unregister.</param>
        /// <returns>A task that completes when unregistration is done.</returns>
        Task UnregisterConnectionAsync(string connectionId);

        /// <summary>
        /// Gets all active connections for a virtual key.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to query.</param>
        /// <returns>List of active connections.</returns>
        Task<List<ConnectionInfo>> GetActiveConnectionsAsync(int virtualKeyId);

        /// <summary>
        /// Gets the total number of active connections across all keys.
        /// </summary>
        /// <returns>The total connection count.</returns>
        Task<int> GetTotalConnectionCountAsync();

        /// <summary>
        /// Gets connection information by ID.
        /// </summary>
        /// <param name="connectionId">The connection ID to query.</param>
        /// <returns>Connection information, or null if not found.</returns>
        Task<ConnectionInfo?> GetConnectionAsync(string connectionId);

        /// <summary>
        /// Attempts to terminate a specific connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to terminate.</param>
        /// <param name="virtualKeyId">The virtual key ID (for ownership verification).</param>
        /// <returns>True if terminated, false if not found or not owned.</returns>
        Task<bool> TerminateConnectionAsync(string connectionId, int virtualKeyId);

        /// <summary>
        /// Checks if a virtual key has reached its connection limit.
        /// </summary>
        /// <param name="virtualKeyId">The virtual key ID to check.</param>
        /// <returns>True if at limit, false otherwise.</returns>
        Task<bool> IsAtConnectionLimitAsync(int virtualKeyId);

        /// <summary>
        /// Updates usage statistics for a connection.
        /// </summary>
        /// <param name="connectionId">The connection ID.</param>
        /// <param name="stats">The usage statistics to update.</param>
        /// <returns>A task that completes when the update is done.</returns>
        Task UpdateUsageStatsAsync(string connectionId, ConnectionUsageStats stats);

        /// <summary>
        /// Performs health checks on all connections and removes stale ones.
        /// </summary>
        /// <returns>Number of connections cleaned up.</returns>
        Task<int> CleanupStaleConnectionsAsync();
    }

    /// <summary>
    /// Detailed information about a managed connection.
    /// </summary>
    public class ManagedConnection
    {
        /// <summary>
        /// The connection information.
        /// </summary>
        public ConnectionInfo Info { get; set; } = new();

        /// <summary>
        /// The WebSocket instance.
        /// </summary>
        public WebSocket? WebSocket { get; set; }

        /// <summary>
        /// The virtual key ID.
        /// </summary>
        public int VirtualKeyId { get; set; }

        /// <summary>
        /// Last heartbeat timestamp.
        /// </summary>
        public DateTime LastHeartbeat { get; set; }

        /// <summary>
        /// Whether the connection is healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = true;
    }
}