using System.Diagnostics;

using ConduitLLM.Core.Diagnostics;
using ConduitLLM.Core.Events;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services;

/// <summary>
/// Builds and emits a readiness heartbeat immediately and at a configured interval.
/// </summary>
public abstract class HeartbeatPublisherBase : BackgroundService
{
    private const int MinimumIntervalSeconds = 5;
    private static readonly string InstanceIdentifier =
        $"{Environment.MachineName}_{Environment.ProcessId}";
    private static readonly DateTime ProcessStartUtc =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly BuildMetadata _serviceBuild;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger _logger;
    private readonly string _serviceName;
    private readonly TimeSpan _interval;

    protected HeartbeatPublisherBase(
        HealthCheckService healthCheckService,
        IConfiguration configuration,
        ILogger logger,
        Type serviceAssemblyMarker,
        string serviceName,
        string intervalConfigurationKey)
    {
        _healthCheckService =
            healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(serviceAssemblyMarker);
        _serviceName = serviceName;
        _serviceBuild = BuildMetadata.FromAssembly(serviceAssemblyMarker.Assembly);
        _interval = TimeSpan.FromSeconds(Math.Max(
            MinimumIntervalSeconds,
            configuration.GetValue(intervalConfigurationKey, 30)));
    }

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{ServiceName} heartbeat publisher started (interval {IntervalSeconds}s, instance {InstanceId})",
            _serviceName,
            _interval.TotalSeconds,
            InstanceIdentifier);

        try
        {
            await PublishHeartbeatAsync(stoppingToken);
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishHeartbeatAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    protected async Task PublishHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("ready") || registration.Tags.Count == 0,
                cancellationToken);
            var now = DateTime.UtcNow;
            var heartbeat = new GatewayHeartbeat
            {
                InstanceId = InstanceIdentifier,
                Version = _serviceBuild.Version,
                CommitSha = _serviceBuild.CommitSha,
                BuildTimestamp = _serviceBuild.BuildTimestamp,
                Status = readiness.Status switch
                {
                    HealthStatus.Healthy => "healthy",
                    HealthStatus.Degraded => "degraded",
                    _ => "unhealthy"
                },
                UptimeSeconds = (now - ProcessStartUtc).TotalSeconds,
                IntervalSeconds = _interval.TotalSeconds,
                Timestamp = now
            };

            await EmitAsync(heartbeat, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit {ServiceName} heartbeat", _serviceName);
        }
    }

    protected abstract Task EmitAsync(
        GatewayHeartbeat heartbeat,
        CancellationToken cancellationToken);
}
