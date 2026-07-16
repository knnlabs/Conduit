using Wolverine;

namespace ConduitLLM.Configuration.Messaging.Wolverine
{
    /// <summary>
    /// Adapts a Wolverine <see cref="IMessageContext"/> to the transport-agnostic
    /// <see cref="IEventContext"/> handed to <see cref="IEventHandler{TEvent}"/> —
    /// the mirror of <c>MassTransitEventContext</c>.
    /// </summary>
    internal sealed class WolverineEventContext : IEventContext
    {
        private readonly IMessageContext _context;
        private readonly CancellationToken _cancellationToken;

        /// <param name="context">The Wolverine message context for the current delivery.</param>
        /// <param name="cancellationToken">
        /// The delivery-lifetime token (Wolverine injects it as a handler method
        /// parameter rather than exposing it on the context).
        /// </param>
        public WolverineEventContext(IMessageContext context, CancellationToken cancellationToken)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cancellationToken = cancellationToken;
        }

        /// <inheritdoc />
        public CancellationToken CancellationToken => _cancellationToken;

        /// <inheritdoc />
        public Guid? MessageId => _context.Envelope?.Id;

        /// <inheritdoc />
        public string? CorrelationId => _context.Envelope?.CorrelationId;

        /// <inheritdoc />
        public bool TryGetHeader(string key, out object? value)
        {
            if (_context.Envelope?.Headers is { } headers && headers.TryGetValue(key, out var headerValue))
            {
                value = headerValue;
                return true;
            }

            value = null;
            return false;
        }

        /// <inheritdoc />
        // Routed through the message context so Wolverine treats it as a cascading
        // publish from the current envelope (correlation propagated, outbox-aware) —
        // the equivalent of MassTransit's ConsumeContext.Publish.
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            cancellationToken.ThrowIfCancellationRequested();
            return _context.PublishAsync(@event).AsTask();
        }

        /// <inheritdoc />
        // Wolverine-native durable scheduling (survives restarts, unlike the
        // unconfigured MassTransit scheduler this replaces). The scheduled event is
        // routed by type, which lands it back on the same subscription the current
        // delivery came from — the ScheduleSend semantics WebhookDeliveryConsumer needs.
        public Task SchedulePublishAsync<TEvent>(DateTime deliveryTime, TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            cancellationToken.ThrowIfCancellationRequested();

            // Callers pass UTC (DateTime.UtcNow + delay); treat an unspecified kind as
            // UTC rather than letting DateTimeOffset assume server-local time.
            var deliverAt = deliveryTime.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(deliveryTime, TimeSpan.Zero)
                : new DateTimeOffset(deliveryTime.ToUniversalTime());

            // Equivalent to the ScheduleAsync extension, but via the interface method so
            // the scheduling intent is explicit (and testable).
            return _context.PublishAsync(@event, new DeliveryOptions { ScheduledTime = deliverAt }).AsTask();
        }
    }
}
