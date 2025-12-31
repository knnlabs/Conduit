namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Raised when a SignalR connection limit is exceeded for a virtual key
    /// </summary>
    public record ConnectionLimitExceeded : DomainEvent
    {
        /// <summary>
        /// Virtual Key ID that exceeded the limit
        /// </summary>
        public int VirtualKeyId { get; init; }

        /// <summary>
        /// Virtual Key hash for identification
        /// </summary>
        public string VirtualKeyHash { get; init; } = string.Empty;

        /// <summary>
        /// Current number of active connections
        /// </summary>
        public int CurrentConnections { get; init; }

        /// <summary>
        /// Maximum allowed connections
        /// </summary>
        public int MaxConnections { get; init; }

        /// <summary>
        /// Hub name the connection was attempted on
        /// </summary>
        public string? HubName { get; init; }

        /// <summary>
        /// IP address of the connection attempt (if available)
        /// </summary>
        public string? IpAddress { get; init; }

        /// <summary>
        /// Partition key for ordered processing per virtual key
        /// </summary>
        public string PartitionKey => VirtualKeyId.ToString();
    }
}
