using System.Diagnostics;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Records a readiness heartbeat for every Admin process so cluster diagnostics do not
/// mistake the process serving the current request for the whole logical service.
/// </summary>
public sealed class AdminHeartbeatPublisher : BackgroundService
{
    private const int MinimumIntervalSeconds = 5;
    private static readonly string InstanceIdentifier =
        $"{Environment.MachineName}_{Environment.ProcessId}";
    private static readonly BuildMetadata ServiceBuild =
        BuildMetadata.FromAssembly(typeof(AdminHeartbeatPublisher).Assembly);
    private static readonly DateTime ProcessStartUtc =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();

    private readonly IServiceHeartbeatStore _store;
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<AdminHeartbeatPublisher> _logger;
    private readonly TimeSpan _interval;

    public AdminHeartbeatPublisher(
        IServiceHeartbeatStore store,
        HealthCheckService healthCheckService,
        IConfiguration configuration,
        ILogger<AdminHeartbeatPublisher> logger)
    {
        _store = store;
        _healthCheckService = healthCheckService;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(
            MinimumIntervalSeconds,
            configuration.GetValue("AdminHeartbeat:IntervalSeconds", 30)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecordHeartbeatAsync(stoppingToken);
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RecordHeartbeatAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    internal async Task RecordHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            var readiness = await _healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("ready") || registration.Tags.Count == 0,
                cancellationToken);
            var now = DateTime.UtcNow;
            await _store.RecordAsync(new ServiceHeartbeatSnapshot
            {
                ServiceId = RedisKeys.ServiceHeartbeat.AdminServiceId,
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
                UptimeSeconds = (now - ProcessStartUtc).TotalSeconds,
                IntervalSeconds = _interval.TotalSeconds,
                ReportedAtUtc = now,
                ReceivedAtUtc = now
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record Admin heartbeat");
        }
    }
}
