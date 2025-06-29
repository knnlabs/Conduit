using System;
using System.Collections.Generic;

namespace ConduitLLM.Http.SignalR.Models
{
    /// <summary>
    /// Information about a SignalR connection
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// Unique connection identifier
        /// </summary>
        public string ConnectionId { get; set; } = null!;

        /// <summary>
        /// Hub name for this connection
        /// </summary>
        public string HubName { get; set; } = null!;

        /// <summary>
        /// Virtual key ID associated with this connection
        /// </summary>
        public int? VirtualKeyId { get; set; }

        /// <summary>
        /// Time when the connection was established
        /// </summary>
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last activity time for this connection
        /// </summary>
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Groups this connection is subscribed to
        /// </summary>
        public HashSet<string> Groups { get; set; } = new();

        /// <summary>
        /// User agent string from the client
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// IP address of the client
        /// </summary>
        public string? IpAddress { get; set; }

        /// <summary>
        /// Transport type (WebSockets, ServerSentEvents, LongPolling)
        /// </summary>
        public string? TransportType { get; set; }

        /// <summary>
        /// Custom metadata about the connection
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Number of messages sent to this connection
        /// </summary>
        public long MessagesSent { get; set; }

        /// <summary>
        /// Number of messages acknowledged by this connection
        /// </summary>
        public long MessagesAcknowledged { get; set; }

        /// <summary>
        /// Gets the connection duration
        /// </summary>
        public TimeSpan ConnectionDuration => DateTime.UtcNow - ConnectedAt;

        /// <summary>
        /// Gets the time since last activity
        /// </summary>
        public TimeSpan IdleTime => DateTime.UtcNow - LastActivityAt;

        /// <summary>
        /// Checks if the connection is considered stale
        /// </summary>
        /// <param name="staleThreshold">Threshold for considering a connection stale</param>
        public bool IsStale(TimeSpan staleThreshold) => IdleTime > staleThreshold;
    }
}