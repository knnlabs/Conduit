namespace ConduitLLM.Configuration.Messaging
{
    /// <summary>
    /// Transport-agnostic context supplied to an <see cref="IEventHandler{TEvent}"/>
    /// during delivery. It exposes exactly the consume-time capabilities the existing
    /// MassTransit consumers rely on, so they can be ported 1:1:
    /// cancellation, the message id (used for de-duplication keys), correlation id,
    /// header lookup, follow-on publishing, and deferred/scheduled publishing
    /// (the webhook retry path).
    /// </summary>
    public interface IEventContext
    {
        /// <summary>Cancellation token tied to the message's delivery lifetime.</summary>
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Transport message id for this delivery, if the transport provides one.
        /// Used by <c>WebhookDeliveryConsumer</c> to build a de-duplication key.
        /// </summary>
        Guid? MessageId { get; }

        /// <summary>Correlation id propagated with the message, if any.</summary>
        string? CorrelationId { get; }

        /// <summary>
        /// Attempts to read a transport header by key. Returns <c>false</c> if the
        /// transport does not carry the header (matching MassTransit
        /// <c>Headers.TryGetHeader</c> semantics).
        /// </summary>
        bool TryGetHeader(string key, out object? value);

        /// <summary>
        /// Publishes a follow-on event from within a handler (e.g. the spend processor
        /// emitting <c>SpendUpdated</c>). Equivalent to <see cref="IEventBus.PublishAsync"/>
        /// but flows through the delivery context so the transport can correlate it.
        /// </summary>
        Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class;

        /// <summary>
        /// Schedules an event for delivery at a future time. Mirrors MassTransit's
        /// <c>ConsumeContext.ScheduleSend</c> (used by the webhook retry path) and maps
        /// to Wolverine's native scheduling on the Wolverine backend.
        /// </summary>
        /// <param name="deliveryTime">Absolute time at which the event should be delivered.</param>
        /// <param name="event">The event to schedule.</param>
        /// <param name="cancellationToken">Token used to cancel the scheduling operation.</param>
        Task SchedulePublishAsync<TEvent>(DateTime deliveryTime, TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class;
    }
}
