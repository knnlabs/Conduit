namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// RabbitMQ metrics.
    /// </summary>
    public class RabbitMQMetrics
    {
        /// <summary>
        /// Connection status.
        /// </summary>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Queue depths by queue name.
        /// </summary>
        public Dictionary<string, int> QueueDepths { get; set; } = new();

        /// <summary>
        /// Messages published per minute.
        /// </summary>
        public int MessagesPublishedPerMinute { get; set; }

        /// <summary>
        /// Messages consumed per minute.
        /// </summary>
        public int MessagesConsumedPerMinute { get; set; }

        /// <summary>
        /// Consumer count by queue.
        /// </summary>
        public Dictionary<string, int> ConsumerCounts { get; set; } = new();
    }
}