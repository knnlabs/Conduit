namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Base interface for all domain events in the Conduit system.
    /// </summary>
    /// <remarks>
    /// The contract lives in the Configuration assembly because Configuration events
    /// and Core events both depend on it; the namespace remains stable for consumers.
    /// </remarks>
    public interface IDomainEvent
    {
        /// <summary>
        /// Unique identifier for the event.
        /// </summary>
        string EventId { get; }

        /// <summary>
        /// Timestamp when the event occurred.
        /// </summary>
        DateTime Timestamp { get; }

        /// <summary>
        /// Correlation ID for tracking related events.
        /// </summary>
        string CorrelationId { get; }
    }

    /// <summary>
    /// Base record for domain events with common properties.
    /// </summary>
    public abstract record DomainEvent : IDomainEvent
    {
        public string EventId { get; init; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string CorrelationId { get; init; } = string.Empty;
    }

}
