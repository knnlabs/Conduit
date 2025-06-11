using System;

namespace ConduitLLM.Core.Models.Realtime
{
    /// <summary>
    /// Information about an active real-time connection.
    /// </summary>
    public class ConnectionInfo
    {
        /// <summary>
        /// Unique connection identifier.
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// The model being used.
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// The provider being used.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// When the connection was established.
        /// </summary>
        public DateTime ConnectedAt { get; set; }

        /// <summary>
        /// Current connection state.
        /// </summary>
        public string State { get; set; } = "active";

        /// <summary>
        /// Usage statistics for this connection.
        /// </summary>
        public ConnectionUsageStats? Usage { get; set; }

        /// <summary>
        /// The virtual key associated with this connection.
        /// </summary>
        public string VirtualKey { get; set; } = string.Empty;

        /// <summary>
        /// The provider connection ID.
        /// </summary>
        public string? ProviderConnectionId { get; set; }

        /// <summary>
        /// Connection start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Last activity timestamp.
        /// </summary>
        public DateTime LastActivity { get; set; }

        /// <summary>
        /// Total audio bytes processed.
        /// </summary>
        public long AudioBytesProcessed { get; set; }

        /// <summary>
        /// Total tokens used.
        /// </summary>
        public long TokensUsed { get; set; }

        /// <summary>
        /// Estimated cost.
        /// </summary>
        public decimal EstimatedCost { get; set; }
    }

    /// <summary>
    /// Usage statistics for a real-time connection.
    /// </summary>
    public class ConnectionUsageStats
    {
        /// <summary>
        /// Total audio duration in seconds.
        /// </summary>
        public double AudioDurationSeconds { get; set; }

        /// <summary>
        /// Number of messages sent.
        /// </summary>
        public int MessagesSent { get; set; }

        /// <summary>
        /// Number of messages received.
        /// </summary>
        public int MessagesReceived { get; set; }

        /// <summary>
        /// Estimated cost so far.
        /// </summary>
        public decimal EstimatedCost { get; set; }
    }
}
