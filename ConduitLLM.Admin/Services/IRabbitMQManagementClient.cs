using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Client interface for interacting with RabbitMQ Management API.
    /// </summary>
    public interface IRabbitMQManagementClient
    {
        /// <summary>
        /// Gets information about all queues.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of queue information.</returns>
        Task<IEnumerable<QueueInfo>> GetQueuesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets messages from a specific queue.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="count">Number of messages to retrieve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Collection of messages.</returns>
        Task<IEnumerable<RabbitMQMessage>> GetMessagesAsync(
            string queueName, 
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a specific message from a queue.
        /// </summary>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="messageId">Message identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Message details or null if not found.</returns>
        Task<RabbitMQMessage?> GetMessageAsync(
            string queueName, 
            string messageId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Information about a RabbitMQ queue.
    /// </summary>
    public record QueueInfo
    {
        /// <summary>
        /// Queue name.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Number of messages in the queue.
        /// </summary>
        public long Messages { get; init; }

        /// <summary>
        /// Total size of messages in bytes.
        /// </summary>
        public long MessageBytes { get; init; }

        /// <summary>
        /// Number of consumers.
        /// </summary>
        public int Consumers { get; init; }

        /// <summary>
        /// Queue state.
        /// </summary>
        public string State { get; init; } = "running";

        /// <summary>
        /// Additional queue arguments.
        /// </summary>
        public Dictionary<string, object> Arguments { get; init; } = new();

        /// <summary>
        /// Queue statistics.
        /// </summary>
        public QueueStatistics? MessageStats { get; init; }
    }

    /// <summary>
    /// Queue statistics.
    /// </summary>
    public record QueueStatistics
    {
        /// <summary>
        /// Message publish rate.
        /// </summary>
        public double PublishRate { get; init; }

        /// <summary>
        /// Message delivery rate.
        /// </summary>
        public double DeliverRate { get; init; }

        /// <summary>
        /// Message acknowledgment rate.
        /// </summary>
        public double AckRate { get; init; }
    }

    /// <summary>
    /// RabbitMQ message information.
    /// </summary>
    public record RabbitMQMessage
    {
        /// <summary>
        /// Message properties.
        /// </summary>
        public RabbitMQMessageProperties Properties { get; init; } = new();

        /// <summary>
        /// Message payload.
        /// </summary>
        public string Payload { get; init; } = string.Empty;

        /// <summary>
        /// Payload encoding.
        /// </summary>
        public string PayloadEncoding { get; init; } = "string";

        /// <summary>
        /// Redelivered flag.
        /// </summary>
        public bool Redelivered { get; init; }

        /// <summary>
        /// Exchange the message was published to.
        /// </summary>
        public string Exchange { get; init; } = string.Empty;

        /// <summary>
        /// Routing key used.
        /// </summary>
        public string RoutingKey { get; init; } = string.Empty;

        /// <summary>
        /// Message size in bytes.
        /// </summary>
        public int PayloadBytes { get; init; }
    }

    /// <summary>
    /// RabbitMQ message properties.
    /// </summary>
    public record RabbitMQMessageProperties
    {
        /// <summary>
        /// Message ID.
        /// </summary>
        public string? MessageId { get; init; }

        /// <summary>
        /// Correlation ID.
        /// </summary>
        public string? CorrelationId { get; init; }

        /// <summary>
        /// Message headers.
        /// </summary>
        public Dictionary<string, object> Headers { get; init; } = new();

        /// <summary>
        /// Content type.
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// Timestamp when message was created.
        /// </summary>
        public long? Timestamp { get; init; }

        /// <summary>
        /// Message type.
        /// </summary>
        public string? Type { get; init; }
    }
}