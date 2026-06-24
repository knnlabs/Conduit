using MassTransit;

namespace ConduitLLM.Configuration.Messaging.MassTransit
{
    /// <summary>
    /// <see cref="IEventBus"/> implementation that delegates to MassTransit's
    /// <see cref="IPublishEndpoint"/>. This is the Phase 1 backend of the messaging
    /// abstraction (epic #909): it preserves today's publish routing exactly while
    /// removing the direct MassTransit dependency from call sites.
    /// </summary>
    /// <remarks>
    /// Routing is unchanged: <c>PublishAsync&lt;TEvent&gt;</c> calls
    /// <c>IPublishEndpoint.Publish&lt;TEvent&gt;</c>, so MassTransit routes by the closed
    /// generic type just as the previous direct calls did.
    /// <para>
    /// <b>Durability gap (flagged for #927):</b> this adapter does not itself swallow
    /// failures — but the existing fire-and-forget seams
    /// (<c>EventPublishingControllerBase</c> / <c>EventPublishingServiceBase</c>) call it
    /// inside a background <c>Task.Run</c> and swallow exceptions, and there is no outbox.
    /// A crash (or broker outage) between the business commit and the publish loses the
    /// event. The Wolverine Postgres transactional outbox closes this in I2.4 (#927);
    /// Phase 1 deliberately preserves the current (lossy) semantics for a zero-behavior
    /// change checkpoint.
    /// </para>
    /// </remarks>
    public sealed class MassTransitEventBus : IEventBus
    {
        private readonly IPublishEndpoint _publishEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="MassTransitEventBus"/> class.
        /// </summary>
        /// <param name="publishEndpoint">The MassTransit publish endpoint to delegate to.</param>
        public MassTransitEventBus(IPublishEndpoint publishEndpoint)
        {
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        }

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            return _publishEndpoint.Publish(@event, cancellationToken);
        }
    }
}
