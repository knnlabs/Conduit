using MassTransit;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.Configuration.Messaging.MassTransit
{
    /// <summary>
    /// Generic MassTransit consumer that bridges a delivered <typeparamref name="TEvent"/>
    /// to every registered <see cref="IEventHandler{TEvent}"/>. This is the consume half
    /// of the Phase 1 anti-corruption layer (epic #909): it lets handlers be written
    /// against <see cref="IEventHandler{TEvent}"/> / <see cref="IEventContext"/> with no
    /// reference to MassTransit, while the existing endpoint topology and retry semantics
    /// are preserved.
    /// </summary>
    /// <remarks>
    /// MassTransit constructs this consumer (and resolves its <see cref="IEventHandler{TEvent}"/>
    /// dependencies) from the per-message consume scope, so a handler that injects
    /// <see cref="IEventBus"/> for follow-on publishes gets the same scoped, correlation-aware
    /// publish endpoint it had when it injected <c>IPublishEndpoint</c> directly.
    /// <para>
    /// Handlers are invoked sequentially. An exception from any handler propagates to
    /// MassTransit, triggering the endpoint's retry / redelivery policy — identical to a
    /// consumer re-throwing today. Event types delivered to more than one handler are all
    /// idempotent cache/notification handlers, for which re-running on retry is safe; the
    /// ordered/financial/deferred endpoints each have exactly one handler.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEvent">The event type this bridge consumes.</typeparam>
    public sealed class MassTransitConsumerBridge<TEvent> : IConsumer<TEvent>
        where TEvent : class
    {
        private readonly IEnumerable<IEventHandler<TEvent>> _handlers;
        private readonly ILogger<MassTransitConsumerBridge<TEvent>> _logger;

        public MassTransitConsumerBridge(
            IEnumerable<IEventHandler<TEvent>> handlers,
            ILogger<MassTransitConsumerBridge<TEvent>> logger)
        {
            _handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task Consume(ConsumeContext<TEvent> context)
        {
            var eventContext = new MassTransitEventContext(context);

            foreach (var handler in _handlers)
            {
                // Let exceptions propagate so MassTransit applies the endpoint retry /
                // redelivery policy, exactly as the original consumers' re-throw did.
                await handler.HandleAsync(context.Message, eventContext);
            }
        }
    }
}
