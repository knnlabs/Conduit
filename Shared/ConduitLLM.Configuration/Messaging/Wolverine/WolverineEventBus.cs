using Wolverine;

namespace ConduitLLM.Configuration.Messaging.Wolverine
{
    /// <summary>
    /// <see cref="IEventBus"/> implementation that delegates to Wolverine's
    /// <see cref="IMessageBus"/>. This is the Phase 2 backend of the messaging
    /// abstraction (epic #909, I2.2/#925): call sites are identical to the
    /// previous backend — routing is by the closed generic event type.
    /// </summary>
    /// <remarks>
    /// Registered scoped, like the previous backend's event bus: Wolverine's
    /// <see cref="IMessageBus"/> is itself scoped, and inside a handler scope it is the
    /// active <see cref="IMessageContext"/>, so follow-on publishes from handlers flow
    /// through the current envelope (correlation-aware) and flush atomically with
    /// handler completion. All sending endpoints are durable (I2.4/#927), so an
    /// accepted publish is persisted to the Postgres outbox before delivery.
    /// <para>
    /// Wolverine's publish APIs carry no <see cref="CancellationToken"/>; the token is
    /// honored by checking it before handing each event to the transport.
    /// </para>
    /// </remarks>
    public sealed class WolverineEventBus : IEventBus
    {
        private readonly IMessageBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="WolverineEventBus"/> class.
        /// </summary>
        /// <param name="bus">The Wolverine message bus to delegate to.</param>
        public WolverineEventBus(IMessageBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <inheritdoc />
        public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(@event);
            cancellationToken.ThrowIfCancellationRequested();
            return _bus.PublishAsync(@event).AsTask();
        }

        /// <inheritdoc />
        // Wolverine has no batch-publish API; sequential publishes preserve the
        // IEventBus contract (the batch is an optimization, not a semantic guarantee).
        public async Task PublishBatchAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
            where TEvent : class
        {
            ArgumentNullException.ThrowIfNull(events);
            foreach (var @event in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _bus.PublishAsync(@event);
            }
        }
    }
}
