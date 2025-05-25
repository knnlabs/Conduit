using System;
using System.Collections.Generic;

namespace ConduitLLM.Providers.InternalModels
{
    /// <summary>
    /// Internal message class used by providers for real-time communication.
    /// </summary>
    public class ProviderRealtimeMessage
    {
        /// <summary>
        /// The type of message.
        /// </summary>
        public string? Type { get; set; }

        /// <summary>
        /// Message data payload.
        /// </summary>
        public Dictionary<string, object>? Data { get; set; }

        /// <summary>
        /// Session identifier.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Sequence number for ordering.
        /// </summary>
        public long? SequenceNumber { get; set; }
    }
}