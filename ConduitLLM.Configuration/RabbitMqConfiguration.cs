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
        /// </summary>
        public ushort PrefetchCount { get; set; } = 10;

        /// <summary>
        /// The number of partitions for ordered event processing.
        /// This determines the maximum parallelism for processing events of the same type.
        /// </summary>
        public int PartitionCount { get; set; } = 10;

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
    }
}