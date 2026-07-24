using System.Diagnostics;

using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Diagnostics;
using ConduitLLM.Core.Events;

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
    public class GatewayHeartbeatPublisher : BackgroundService
    {
        /// <summary>Floor on the configured interval to avoid flooding the bus.</summary>
        private const int MinimumIntervalSeconds = 5;

        private static readonly string InstanceIdentifier =
            $"{Environment.MachineName}_{Environment.ProcessId}";

        private static readonly BuildMetadata ServiceBuild =
            BuildMetadata.FromAssembly(typeof(GatewayHeartbeatPublisher).Assembly);

        private static readonly DateTime ProcessStartUtc =
            Process.GetCurrentProcess().StartTime.ToUniversalTime();

        private readonly IServiceProvider _serviceProvider;
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<GatewayHeartbeatPublisher> _logger;
        private readonly TimeSpan _interval;

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayHeartbeatPublisher"/> class.
        /// </summary>
        public GatewayHeartbeatPublisher(
            IServiceProvider serviceProvider,
            HealthCheckService healthCheckService,
            IConfiguration configuration,
            ILogger<GatewayHeartbeatPublisher> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var seconds = configuration.GetValue("GatewayHeartbeat:IntervalSeconds", 30);
            _interval = TimeSpan.FromSeconds(Math.Max(MinimumIntervalSeconds, seconds));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Gateway heartbeat publisher started (interval {IntervalSeconds}s, instance {InstanceId})",
                _interval.TotalSeconds, InstanceIdentifier);

            try
            {
                // Emit one immediately so a freshly started Gateway is reflected quickly.
                await PublishHeartbeatAsync(stoppingToken);

                using var timer = new PeriodicTimer(_interval);
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await PublishHeartbeatAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown.
            }
        }

        private async Task PublishHeartbeatAsync(CancellationToken cancellationToken)
        {
            try
            {
                // IEventBus is registered Scoped, so resolve it in a fresh scope per publish
                // (a singleton BackgroundService cannot inject it directly).
                using var scope = _serviceProvider.CreateScope();
                var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
                var readiness = await _healthCheckService.CheckHealthAsync(
                    registration => registration.Tags.Contains("ready") || registration.Tags.Count == 0,
                    cancellationToken);

                await eventBus.PublishAsync(new GatewayHeartbeat
                {
                    InstanceId = InstanceIdentifier,
                    Version = ServiceBuild.Version,
                    CommitSha = ServiceBuild.CommitSha,
                    BuildTimestamp = ServiceBuild.BuildTimestamp,
                    Status = readiness.Status switch
                    {
                        HealthStatus.Healthy => "healthy",
                        HealthStatus.Degraded => "degraded",
                        _ => "unhealthy"
                    },
                    UptimeSeconds = (DateTime.UtcNow - ProcessStartUtc).TotalSeconds,
                    IntervalSeconds = _interval.TotalSeconds
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down — ignore.
            }
            catch (Exception ex)
            {
                // A missed heartbeat is self-correcting (the next tick supersedes it); never
                // let it crash the background service.
                _logger.LogWarning(ex, "Failed to publish Gateway heartbeat");
            }
        }
    }
}
