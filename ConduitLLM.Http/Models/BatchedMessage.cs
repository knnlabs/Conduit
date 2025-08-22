namespace ConduitLLM.Http.Models
{
    /// <summary>
    /// Represents a batch of messages to be sent together
    /// </summary>
    public class BatchedMessage
    {
        /// <summary>
        /// Array of message payloads
        /// </summary>
        public List<object> Messages { get; set; } = new();

        /// <summary>
        /// Number of messages in the batch
        /// </summary>
        public int Count => Messages.Count;

        /// <summary>
        /// Unique batch identifier
        /// </summary>
        public string BatchId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Batch creation timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Method name for all messages in this batch
        /// </summary>
        public string MethodName { get; set; } = null!;

        /// <summary>
        /// Target connection ID (null for group messages)
        /// </summary>
        public string? ConnectionId { get; set; }

        /// <summary>
        /// Target group name (null for direct messages)
        /// </summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Hub name for routing
        /// </summary>
        public string HubName { get; set; } = null!;

        /// <summary>
        /// Total size of all messages in bytes (estimated)
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Priority of the batch (highest priority of contained messages)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Whether this batch contains critical messages
        /// </summary>
        public bool ContainsCriticalMessages { get; set; }
    }
}