namespace ConduitLLM.Configuration.Messaging
{
    /// <summary>
    /// Conduit-owned abstraction for handling a domain event of type
    /// <typeparamref name="TEvent"/>, independent of any specific messaging library.
    /// This is the consume half of the anti-corruption layer introduced by epic #909.
    /// </summary>
    /// <remarks>
    /// A single class may implement <see cref="IEventHandler{TEvent}"/> for several
    /// event types (the existing handlers frequently do). The hosting transport adapter
    /// resolves all registered handlers for a delivered event's type and invokes each.
    /// <para>
    /// Error semantics mirror the existing MassTransit consumers: throwing from
    /// <see cref="HandleAsync"/> signals the transport to retry / redeliver according to
    /// the endpoint's <see cref="EndpointPolicy"/>. Handlers that should not be retried
    /// must catch and swallow internally.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type this handler processes.</typeparam>
    public interface IEventHandler<TEvent>
        where TEvent : class
    {
        /// <summary>
        /// Handles a delivered event.
        /// </summary>
        /// <param name="event">The delivered event instance.</param>
        /// <param name="context">
        /// Transport-agnostic delivery context (cancellation, message id, headers,
        /// follow-on publish, and deferred/scheduled publish).
        /// </param>
        Task HandleAsync(TEvent @event, IEventContext context);
    }
}
