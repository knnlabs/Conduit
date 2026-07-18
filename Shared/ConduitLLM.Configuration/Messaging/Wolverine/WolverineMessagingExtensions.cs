using JasperFx;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;
using Wolverine.Postgresql;

namespace ConduitLLM.Configuration.Messaging.Wolverine
{
    /// <summary>
    /// Bootstrap for the Wolverine backend of the messaging abstraction (Phase 2 of
    /// epic #909, I2.1/#924). Configures the Wolverine host on the PostgreSQL
    /// transport with durable message persistence, reusing the service's existing
    /// Npgsql database — no new broker.
    /// </summary>
    /// <remarks>
    /// This registers the Wolverine host only. The <see cref="IEventBus"/> adapter and
    /// the <see cref="IEventHandler{TEvent}"/> handler host land in I2.2/#925; until
    /// then MassTransit remains the active backend and Wolverine boots idle behind the
    /// <c>ConduitLLM:Messaging:Backend</c> flag.
    /// </remarks>
    public static class WolverineMessagingExtensions
    {
        /// <summary>
        /// Configuration key for the durability schema name. Default is per-service
        /// (<c>wolverine_conduit_gateway</c> / <c>wolverine_conduit_admin</c>): the node,
        /// agent-assignment, and envelope tables MUST NOT be shared between services, or
        /// the two hosts form a single agent cluster and the leader (which may be the
        /// Admin) fails to assign the Gateway-only exclusive strict-ordering listeners
        /// (spend-update-events / image-generation-events) — leaving those queues with
        /// no consumer.
        /// </summary>
        public const string SchemaNameKey = "ConduitLLM:Messaging:Wolverine:SchemaName";

        /// <summary>
        /// Schema holding the PostgreSQL transport queue tables. Shared by all services —
        /// cross-service delivery (Admin -&gt; Gateway) works by writing to the same queue
        /// tables, so this schema must stay common even though durability schemas are
        /// per-service.
        /// </summary>
        public const string TransportSchemaName = "wolverine_queues";

        /// <summary>
        /// Configuration key controlling automatic provisioning of Wolverine's durability
        /// and queue tables at startup (default <c>true</c>). Production deployments that
        /// manage schema explicitly set this to <c>false</c> and provision via script —
        /// see the Phase 2 plan (#924).
        /// </summary>
        public const string AutoProvisionKey = "ConduitLLM:Messaging:Wolverine:AutoProvision";

        /// <summary>
        /// Configuration key selecting the Wolverine transport (I2.5/#928):
        /// <c>Postgresql</c> (default — durable persistence + cross-service queues) or
        /// <c>InMemory</c> (local queues only, no Postgres required — dev/CI parity with
        /// MassTransit's in-memory mode). Unrecognized values throw at boot.
        /// </summary>
        public const string TransportKey = "ConduitLLM:Messaging:Wolverine:Transport";

        /// <summary>
        /// Resolves whether the in-memory transport is selected (see <see cref="TransportKey"/>).
        /// Hosts use this to skip the Postgres queue topology, which only exists on the
        /// Postgresql transport.
        /// </summary>
        public static bool UsesInMemoryTransport(IConfiguration configuration)
        {
            var transport = configuration[TransportKey] ?? "Postgresql";

            if (transport.Equals("Postgresql", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (transport.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new InvalidOperationException(
                $"Unrecognized value '{transport}' for '{TransportKey}'. Valid values: Postgresql, InMemory.");
        }

        /// <summary>
        /// Adds the Wolverine host on the PostgreSQL transport with durable persistence.
        /// </summary>
        /// <param name="host">The host builder.</param>
        /// <param name="configuration">App configuration (schema/provisioning knobs).</param>
        /// <param name="connectionString">
        /// The service's PostgreSQL connection string (the same database EF Core uses,
        /// resolved per-service, e.g. "CoreAPI" / "AdminAPI").
        /// </param>
        /// <param name="serviceName">
        /// Wolverine service identity (e.g. <c>conduit-gateway</c>); distinguishes each
        /// service's durability agent and node records in the shared database.
        /// </param>
        /// <param name="configure">
        /// Per-service Wolverine configuration applied after the Conduit defaults —
        /// bridge registrations (<see cref="AddEventBridge{TEvent}"/>) and, from
        /// I2.3/#926, the tuned endpoint policies.
        /// </param>
        public static IHostBuilder AddConduitWolverine(
            this IHostBuilder host,
            IConfiguration configuration,
            string connectionString,
            string serviceName,
            Action<WolverineOptions>? configure = null)
        {
            var schemaName = configuration[SchemaNameKey]
                ?? $"wolverine_{serviceName.Replace('-', '_')}";
            var autoProvision = configuration.GetValue(AutoProvisionKey, true);
            var inMemory = UsesInMemoryTransport(configuration);

            return host.UseWolverine(opts =>
            {
                opts.ServiceName = serviceName;

                // The bridge handlers are container-resolved per message (see
                // AddEventBridge): their IEventHandler<T> graphs use scoped services and
                // opaque factory registrations that codegen cannot inline-construct.
                // Wolverine 6's default ServiceLocationPolicy.NotAllowed throws
                // InvalidServiceLocationException at first delivery for exactly that
                // resolution style, so the policy must be relaxed here. AllowedButWarn
                // keeps a startup warning per located type as a nudge toward
                // codegen-friendly registrations for any future native handlers.
                opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;

                // Wolverine 6 split the Roslyn runtime compiler out of the core package;
                // the default TypeLoadMode.Dynamic fails at startup without it. Explicit
                // (rather than relying on referenced-assembly auto-registration) so boot
                // does not depend on assembly load order. Pre-generated static codegen
                // ('codegen write' + TypeLoadMode.Static) is a cutover optimization (#930).
                opts.UseRuntimeCompilation();

                if (inMemory)
                {
                    // In-memory transport (I2.5/#928): local queues only, no persistence —
                    // dev/CI can run the Wolverine backend without Postgres, matching the
                    // durability level of MassTransit's in-memory mode. Solo skips the
                    // multi-node leader-election agents a single test host never needs.
                    opts.Durability.Mode = DurabilityMode.Solo;
                }
                else
                {
                    // Persistence (inbox/outbox/scheduled messages) AND the message
                    // transport share the existing Postgres database. The durability
                    // schema is per-service (see SchemaNameKey) so each host runs its own
                    // agent cluster; the transport schema is shared so cross-service
                    // queues (Admin -> Gateway) keep working.
                    opts.UsePostgresqlPersistenceAndTransport(
                        connectionString, schemaName, transportSchema: TransportSchemaName);

                    // Local queues (where in-process bridge handlers receive publishes) are
                    // backed by the Postgres durability tables, so buffered messages survive
                    // a crash — already an improvement on MassTransit's in-memory transport.
                    opts.Policies.UseDurableLocalQueues();

                    // Transactional outbox (I2.4/#927): every sending endpoint persists the
                    // envelope to the Postgres outbox before delivery, so a publish accepted
                    // by the bus survives a crash and is retried by the durability agent —
                    // the fire-and-forget publish seams no longer lose events on transient
                    // failure. Messages published from inside a handler additionally flush
                    // atomically with handler completion (the message-context outbox).
                    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

                    opts.AutoBuildMessageStorageOnStartup = autoProvision
                        ? AutoCreate.CreateOrUpdate
                        : AutoCreate.None;

                    // The durability agent's periodic node-assignment checks would
                    // otherwise emit a steady stream of no-op spans (#931).
                    opts.Durability.NodeAssignmentHealthCheckTracingEnabled = false;
                }

                // Wraps handlers in a database transaction where one applies.
                opts.Policies.AutoApplyTransactions();

                // Conduit's IEventHandler<T> implementations are named *Handler/*Consumer
                // with HandleAsync methods, which Wolverine's conventional discovery would
                // otherwise pick up as native handlers (with IEventContext unresolvable).
                // Dispatch goes exclusively through the explicit bridge registrations
                // added in I2.2/#925.
                opts.Discovery.DisableConventionalDiscovery();

                configure?.Invoke(opts);
            });
        }

        /// <summary>
        /// Registers the <see cref="IEventBus"/> adapter over Wolverine's
        /// <see cref="IMessageBus"/>. Scoped for the same reason as
        /// <c>AddMassTransitEventBus</c>: inside a handler scope the bus is the active
        /// message context, so follow-on publishes stay correlation-aware.
        /// </summary>
        public static IServiceCollection AddWolverineEventBus(this IServiceCollection services)
        {
            services.AddScoped<IEventBus, WolverineEventBus>();
            return services;
        }

        /// <summary>
        /// Registers the generic Wolverine bridge handler for an event type — the
        /// Wolverine analogue of the MassTransit <c>AddEventBridge</c>. Causes the event
        /// type to be consumed and dispatched to every registered
        /// <see cref="IEventHandler{TEvent}"/>. Required because conventional discovery
        /// is disabled.
        /// </summary>
        public static void AddEventBridge<TEvent>(this WolverineOptions options)
            where TEvent : class
        {
            options.AddEventBridge(typeof(TEvent));
        }

        /// <summary>
        /// Non-generic overload of <see cref="AddEventBridge{TEvent}(WolverineOptions)"/>
        /// for registering bridges from a shared event-type list.
        /// </summary>
        public static void AddEventBridge(this WolverineOptions options, Type eventType)
        {
            var bridgeType = typeof(WolverineHandlerBridge<>).MakeGenericType(eventType);

            options.Discovery.IncludeType(bridgeType);

            // The bridge must be container-resolved per message, not codegen-inlined:
            // its IEnumerable<IEventHandler<T>> dependency is scoped, and the handler
            // implementations behind it use scoped services, IServiceScopeFactory, and
            // opaque lambda-factory registrations (typed HttpClients, IModelCostService)
            // that Wolverine's inline construction cannot build. Without this opt-in,
            // ServiceLocationPolicy.NotAllowed (the Wolverine 6 default) throws
            // InvalidServiceLocationException at first delivery of the event type.
            // Container resolution per message is the same semantics the MassTransit
            // bridge has always had.
            options.CodeGeneration.AlwaysUseServiceLocationFor(bridgeType);
            options.Services.AddScoped(bridgeType);
        }
    }
}
