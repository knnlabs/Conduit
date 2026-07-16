using Microsoft.Extensions.Logging;

using Wolverine;

namespace ConduitLLM.Configuration.Messaging.Wolverine
{
    /// <summary>
    /// Generic Wolverine handler that bridges a delivered <typeparamref name="TEvent"/>
    /// to every registered <see cref="IEventHandler{TEvent}"/> — the consume half of the
    /// Wolverine backend (epic #909, I2.2/#925) and the exact mirror of
    /// <c>MassTransitConsumerBridge&lt;TEvent&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Conventional discovery is disabled (see <c>WolverineMessagingExtensions</c>);
    /// closed bridge types are registered explicitly per event type via
    /// <c>AddEventBridge</c>. Wolverine constructs the bridge (and resolves its
    /// <see cref="IEventHandler{TEvent}"/> dependencies) from the per-message scope, so a
    /// handler that injects <see cref="IEventBus"/> for follow-on publishes gets the
    /// scoped, correlation-aware message context.
    /// <para>
    /// Handlers are invoked sequentially. An exception from any handler propagates to
    /// Wolverine, triggering the endpoint's retry / redelivery policy — identical to the
    /// MassTransit bridge's behavior. Event types delivered to more than one handler are
    /// all idempotent cache/notification handlers; the ordered/financial/deferred
    /// endpoints each have exactly one handler.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type this bridge handles.</typeparam>
    public sealed class WolverineHandlerBridge<TEvent>
        where TEvent : class
    {
        private readonly IEnumerable<IEventHandler<TEvent>> _handlers;
        private readonly ILogger<WolverineHandlerBridge<TEvent>> _logger;

        public WolverineHandlerBridge(
            IEnumerable<IEventHandler<TEvent>> handlers,
            ILogger<WolverineHandlerBridge<TEvent>> logger)
        {
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Wolverine handler entry point: dispatches the event to every registered
        /// <see cref="IEventHandler{TEvent}"/> with a Wolverine-backed
        /// <see cref="IEventContext"/>.
        /// </summary>
        public async Task Handle(TEvent message, IMessageContext context, CancellationToken cancellationToken)
        {
            var eventContext = new WolverineEventContext(context, cancellationToken);

            foreach (var handler in _handlers)
            {
                // Let exceptions propagate so Wolverine applies the endpoint retry /
                // redelivery policy, exactly as the MassTransit bridge does.
                await handler.HandleAsync(message, eventContext);
            }
        }
    }
}
