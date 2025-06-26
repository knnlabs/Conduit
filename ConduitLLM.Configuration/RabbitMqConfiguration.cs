namespace ConduitLLM.Configuration
{
    /// <summary>
    /// Configuration settings for RabbitMQ message broker.
    /// </summary>
    public class RabbitMqConfiguration
    {
        /// <summary>
        /// The RabbitMQ host name or IP address.
        /// </summary>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// The RabbitMQ port number.
        /// </summary>
        public int Port { get; set; } = 5672;

        /// <summary>
        /// The RabbitMQ username for authentication.
        /// </summary>
        public string Username { get; set; } = "guest";

        /// <summary>
        /// The RabbitMQ password for authentication.
        /// </summary>
        public string Password { get; set; } = "guest";

        /// <summary>
        /// The RabbitMQ virtual host.
        /// </summary>
        public string VHost { get; set; } = "/";

        /// <summary>
        /// The prefetch count for consumers (controls concurrency per consumer).
        /// Production recommendation: 25-30 for balanced memory usage and throughput.
        /// </summary>
        public ushort PrefetchCount { get; set; } = 25;

        /// <summary>
        /// The number of partitions for ordered event processing.
        /// This determines the maximum parallelism for processing events of the same type.
        /// Production recommendation: 30-50 based on expected load.
        /// </summary>
        public int PartitionCount { get; set; } = 30;

        /// <summary>
        /// Whether to use durable queues and exchanges.
        /// </summary>
        public bool Durable { get; set; } = true;

        /// <summary>
        /// Whether to auto-delete queues when no consumers are connected.
        /// </summary>
        public bool AutoDelete { get; set; } = false;

        /// <summary>
        /// Connection retry count for initial connection.
        /// </summary>
        public int ConnectionRetryCount { get; set; } = 5;

        /// <summary>
        /// Heartbeat interval in seconds.
        /// </summary>
        public ushort HeartbeatInterval { get; set; } = 60;

        /// <summary>
        /// Network recovery interval in seconds for automatic reconnection.
        /// </summary>
        public int NetworkRecoveryInterval { get; set; } = 10;

        /// <summary>
        /// Whether to enable automatic topology recovery.
        /// </summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>
        /// Maximum concurrent messages per endpoint (prevents consumer overload).
        /// Production recommendation: 50-100 based on processing complexity.
        /// </summary>
        public int ConcurrentMessageLimit { get; set; } = 50;

        /// <summary>
        /// Connection pool size for better throughput.
        /// </summary>
        public int MaxConnections { get; set; } = 5;

        /// <summary>
        /// Minimum connections to maintain in the pool.
        /// </summary>
        public int MinConnections { get; set; } = 2;

        /// <summary>
        /// Channel max for RabbitMQ connection (default is 2047).
        /// </summary>
        public ushort ChannelMax { get; set; } = 500;

        /// <summary>
        /// Frame max size in bytes (default is 131072).
        /// </summary>
        public uint FrameMax { get; set; } = 131072;

        /// <summary>
        /// Request heartbeat timeout in seconds.
        /// </summary>
        public ushort RequestedHeartbeat { get; set; } = 30;

        /// <summary>
        /// Publisher confirmation timeout in milliseconds.
        /// </summary>
        public int PublisherConfirmationTimeout { get; set; } = 5000;

        /// <summary>
        /// Enable publisher confirmations for reliability.
        /// </summary>
        public bool PublisherConfirmation { get; set; } = true;
    }
}