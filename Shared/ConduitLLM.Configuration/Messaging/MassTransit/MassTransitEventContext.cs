using MassTransit;

namespace ConduitLLM.Configuration.Messaging.MassTransit
{
    /// <summary>
    /// Adapts a MassTransit <see cref="ConsumeContext"/> to the transport-agnostic
    /// <see cref="IEventContext"/> handed to <see cref="IEventHandler{TEvent}"/>.
    /// Every member maps 1:1 to a capability the existing consumers already use, so they
    /// port without behavior change.
    /// </summary>
    internal sealed class MassTransitEventContext : IEventContext
    {
        private readonly ConsumeContext _context;

        public MassTransitEventContext(ConsumeContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc />
        public CancellationToken CancellationToken => _context.CancellationToken;

        /// <inheritdoc />
        public Guid? MessageId => _context.MessageId;

        /// <inheritdoc />
        public string? CorrelationId => _context.CorrelationId?.ToString();

        /// <inheritdoc />
        public bool TryGetHeader(string key, out object? value)
            => _context.Headers.TryGetHeader(key, out value);

        /// <inheritdoc />
        // Routed through the ConsumeContext so MassTransit propagates correlation /
        // conversation linkage from the message being handled — identical to the
        // previous behavior of a consumer publishing via its scoped IPublishEndpoint.
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            return _context.Publish(@event, cancellationToken);
        }

        /// <inheritdoc />
        // Maps to ConsumeContext.ScheduleSend (the webhook deferred-retry path). The
        // scheduled message returns to the current endpoint at the requested time,
        // exactly as WebhookDeliveryConsumer relied on.
        public Task SchedulePublishAsync<TEvent>(DateTime deliveryTime, TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            return _context.ScheduleSend(deliveryTime, @event, cancellationToken);
        }
    }
}
