namespace ConduitLLM.Configuration.DTOs.Metrics
{
    /// <summary>
    /// Infrastructure metrics.
    /// </summary>
    public class InfrastructureMetrics
    {
        /// <summary>
        /// Database metrics.
        /// </summary>
        public DatabaseMetrics Database { get; set; } = new();

        /// <summary>
        /// Redis cache metrics.
        /// </summary>
        public RedisMetrics Redis { get; set; } = new();

        /// <summary>
        /// RabbitMQ metrics.
        /// </summary>
        public RabbitMQMetrics RabbitMQ { get; set; } = new();

        /// <summary>
        /// SignalR metrics.
        /// </summary>
        public SignalRMetrics SignalR { get; set; } = new();
    }
}