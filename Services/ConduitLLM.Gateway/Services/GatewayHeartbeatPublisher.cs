using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Publishes a periodic <see cref="GatewayHeartbeat"/> so the Admin health dashboard can
    /// report the Gateway's real liveness instead of a hardcoded status (#1067).
    /// </summary>
    /// <remarks>
    /// Runs on EVERY Gateway instance (deliberately NOT leader-elected): the heartbeat is a
    /// per-instance liveness signal, so while any instance is up the dashboard keeps seeing a
    /// fresh heartbeat, and a full outage stops every heartbeat and surfaces as unhealthy.
    /// Publishing via <see cref="IEventBus"/> keeps Admin↔Gateway strictly event-driven
    /// (epic #909) — there is no synchronous Admin→Gateway HTTP probe.
    /// </remarks>
    public class GatewayHeartbeatPublisher : HeartbeatPublisherBase
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayHeartbeatPublisher"/> class.
        /// </summary>
        public GatewayHeartbeatPublisher(
            IServiceProvider serviceProvider,
            HealthCheckService healthCheckService,
            IConfiguration configuration,
            ILogger<GatewayHeartbeatPublisher> logger)
            : base(
                healthCheckService,
                configuration,
                logger,
                typeof(GatewayHeartbeatPublisher),
                "Gateway",
                "GatewayHeartbeat:IntervalSeconds")
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        protected override async Task EmitAsync(
            GatewayHeartbeat heartbeat,
            CancellationToken cancellationToken)
        {
            // IEventBus is registered Scoped, so resolve it in a fresh scope per publish
            // (a singleton BackgroundService cannot inject it directly).
            using var scope = _serviceProvider.CreateScope();
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(heartbeat, cancellationToken);
        }
    }
}
