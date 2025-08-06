namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// SignalR metrics.
    /// </summary>
    public class SignalRMetrics
    {
        /// <summary>
        /// Active WebSocket connections.
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Messages sent per minute.
        /// </summary>
        public int MessagesSentPerMinute { get; set; }

        /// <summary>
        /// Messages received per minute.
        /// </summary>
        public int MessagesReceivedPerMinute { get; set; }

        /// <summary>
        /// Average message processing time in milliseconds.
        /// </summary>
        public double AverageMessageProcessingTime { get; set; }

        /// <summary>
        /// Hub method invocations per minute.
        /// </summary>
        public int HubInvocationsPerMinute { get; set; }

        /// <summary>
        /// Reconnection rate per minute.
        /// </summary>
        public int ReconnectionsPerMinute { get; set; }
    }
}