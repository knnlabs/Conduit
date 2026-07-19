using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace ConduitLLM.Configuration.Messaging.MassTransit
{
    /// <summary>
    /// DI helpers for wiring the messaging abstraction onto the MassTransit backend
    /// (Phase 1 of epic #909). Handlers and the <see cref="IEventBus"/> adapter are
    /// registered on the service collection; the generic bridge is registered on the
    /// MassTransit configurator so existing endpoints dispatch through it.
    /// </summary>
    public static class MassTransitMessagingExtensions
    {
        /// <summary>
        /// Registers the <see cref="IEventBus"/> adapter over MassTransit's
        /// <c>IPublishEndpoint</c>. Scoped so that, inside a consume scope, follow-on
        /// publishes flow through the current <c>ConsumeContext</c> (correlation-aware) —
        /// exactly as injecting <c>IPublishEndpoint</c> behaved before.
        /// </summary>
        public static IServiceCollection AddMassTransitEventBus(this IServiceCollection services)
        {
            services.AddScoped<IEventBus, MassTransitEventBus>();
            return services;
        }

        /// <summary>
        /// Registers a handler implementation for an event type. A single class may be
        /// registered for several event types (call once per type it handles).
        /// </summary>
        public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
            where TEvent : class
            where THandler : class, IEventHandler<TEvent>
        {
            services.AddScoped<IEventHandler<TEvent>, THandler>();
            return services;
        }

        /// <summary>
        /// Registers the generic MassTransit bridge consumer for an event type. Causes the
        /// event type to be consumed (on its endpoint) and dispatched to every registered
        /// <see cref="IEventHandler{TEvent}"/>.
        /// </summary>
        public static void AddEventBridge<TEvent>(this IRegistrationConfigurator configurator)
            where TEvent : class
        {
            configurator.AddConsumer<MassTransitConsumerBridge<TEvent>>();
        }

        /// <summary>
        /// Non-generic overload of <see cref="AddEventBridge{TEvent}(IRegistrationConfigurator)"/>
        /// for registering bridges from a shared event-type list (the same list drives the
        /// Wolverine backend's bridge registration, keeping the two backends in sync).
        /// </summary>
        public static void AddEventBridge(this IRegistrationConfigurator configurator, Type eventType)
        {
            configurator.AddConsumer(typeof(MassTransitConsumerBridge<>).MakeGenericType(eventType));
        }
    }
}
