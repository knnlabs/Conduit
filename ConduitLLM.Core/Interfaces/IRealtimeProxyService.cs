using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Core.Interfaces
{
    /// <summary>
    /// Defines the contract for the real-time audio proxy service that handles
    /// WebSocket connections between clients and providers.
    /// </summary>
    /// <remarks>
    /// The proxy service is responsible for:
    /// - Establishing connections to the appropriate provider
    /// - Translating messages between Conduit's format and provider formats
    /// - Tracking usage and costs
    /// - Handling connection lifecycle and errors
    /// - Ensuring message delivery and resilience
    /// </remarks>
    public interface IRealtimeProxyService
    {
        /// <summary>
        /// Handles a WebSocket connection from a client, proxying messages to/from the provider.
        /// </summary>
        /// <param name="connectionId">Unique identifier for this connection.</param>
        /// <param name="clientWebSocket">The client's WebSocket connection.</param>
        /// <param name="virtualKey">The authenticated virtual key entity.</param>
        /// <param name="model">The model to use for the session.</param>
        /// <param name="provider">Optional provider override (null to use routing).</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that completes when the connection is closed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no suitable provider is available.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the virtual key lacks necessary permissions.</exception>
        Task HandleConnectionAsync(
            string connectionId,
            WebSocket clientWebSocket,
            VirtualKey virtualKey,
            string model,
            string? provider,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current status of a proxy connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to query.</param>
        /// <returns>The connection status, or null if not found.</returns>
        Task<ProxyConnectionStatus?> GetConnectionStatusAsync(string connectionId);

        /// <summary>
        /// Attempts to gracefully close a proxy connection.
        /// </summary>
        /// <param name="connectionId">The connection ID to close.</param>
        /// <param name="reason">Optional reason for closing.</param>
        /// <returns>True if the connection was closed, false if not found.</returns>
        Task<bool> CloseConnectionAsync(string connectionId, string? reason = null);
    }

    /// <summary>
    /// Status information for a proxy connection.
    /// </summary>
    public class ProxyConnectionStatus
    {
        /// <summary>
        /// The connection identifier.
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Current state of the connection.
        /// </summary>
        public ProxyConnectionState State { get; set; }

        /// <summary>
        /// The provider being used.
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// The model being used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// When the connection was established.
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// Last activity timestamp.
        /// </summary>
        public DateTime LastActivityAt { get; set; }

        /// <summary>
        /// Number of messages sent to provider.
        /// </summary>
        public long MessagesToProvider { get; set; }

        /// <summary>
        /// Number of messages received from provider.
        /// </summary>
        public long MessagesFromProvider { get; set; }

        /// <summary>
        /// Total bytes sent.
        /// </summary>
        public long BytesSent { get; set; }

        /// <summary>
        /// Total bytes received.
        /// </summary>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Current estimated cost.
        /// </summary>
        public decimal EstimatedCost { get; set; }

        /// <summary>
        /// Any error information.
        /// </summary>
        public string? LastError { get; set; }
    }

    /// <summary>
    /// States for a proxy connection.
    /// </summary>
    public enum ProxyConnectionState
    {
        /// <summary>
        /// Connection is being established.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connection is active and passing messages.
        /// </summary>
        Active,

        /// <summary>
        /// Connection is closing gracefully.
        /// </summary>
        Closing,

        /// <summary>
        /// Connection has been closed.
        /// </summary>
        Closed,

        /// <summary>
        /// Connection failed with an error.
        /// </summary>
        Failed
    }
}