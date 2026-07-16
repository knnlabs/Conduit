using JasperFx;

using Microsoft.Extensions.Configuration;
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
        /// <summary>Configuration key for the durability/queue schema name (default <c>wolverine</c>).</summary>
        public const string SchemaNameKey = "ConduitLLM:Messaging:Wolverine:SchemaName";

        /// <summary>
        /// Configuration key controlling automatic provisioning of Wolverine's durability
        /// and queue tables at startup (default <c>true</c>). Production deployments that
        /// manage schema explicitly set this to <c>false</c> and provision via script —
        /// see the Phase 2 plan (#924).
        /// </summary>
        public const string AutoProvisionKey = "ConduitLLM:Messaging:Wolverine:AutoProvision";

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
        public static IHostBuilder AddConduitWolverine(
            this IHostBuilder host,
            IConfiguration configuration,
            string connectionString,
            string serviceName)
        {
            var schemaName = configuration[SchemaNameKey] ?? "wolverine";
            var autoProvision = configuration.GetValue(AutoProvisionKey, true);

            return host.UseWolverine(opts =>
            {
                opts.ServiceName = serviceName;

                // Persistence (inbox/outbox/scheduled messages) AND the message transport
                // share the existing Postgres database, in an isolated schema.
                opts.UsePostgresqlPersistenceAndTransport(connectionString, schemaName);

                // Wraps handlers in a database transaction where one applies — the
                // foundation for the transactional outbox work in I2.4/#927.
                opts.Policies.AutoApplyTransactions();

                // Conduit's IEventHandler<T> implementations are named *Handler/*Consumer
                // with HandleAsync methods, which Wolverine's conventional discovery would
                // otherwise pick up as native handlers (with IEventContext unresolvable).
                // Dispatch goes exclusively through the explicit bridge registrations
                // added in I2.2/#925.
                opts.Discovery.DisableConventionalDiscovery();

                opts.AutoBuildMessageStorageOnStartup = autoProvision
                    ? AutoCreate.CreateOrUpdate
                    : AutoCreate.None;
            });
        }
    }
}
