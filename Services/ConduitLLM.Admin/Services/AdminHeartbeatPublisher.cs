using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Services;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConduitLLM.Admin.Services;

/// <summary>
/// Records a readiness heartbeat for every Admin process so cluster diagnostics do not
/// mistake the process serving the current request for the whole logical service.
/// </summary>
public sealed class AdminHeartbeatPublisher : HeartbeatPublisherBase
{
    private readonly IServiceHeartbeatStore _store;

    public AdminHeartbeatPublisher(
        IServiceHeartbeatStore store,
        HealthCheckService healthCheckService,
        IConfiguration configuration,
        ILogger<AdminHeartbeatPublisher> logger)
        : base(
            healthCheckService,
            configuration,
            logger,
            typeof(AdminHeartbeatPublisher),
            "Admin",
            "AdminHeartbeat:IntervalSeconds")
    {
        _store = store;
    }

    protected override Task EmitAsync(
        GatewayHeartbeat heartbeat,
        CancellationToken cancellationToken)
    {
        return _store.RecordAsync(new ServiceHeartbeatSnapshot
        {
            ServiceId = RedisKeys.ServiceHeartbeat.AdminServiceId,
            Heartbeat = heartbeat,
            ReportedAtUtc = heartbeat.Timestamp,
            ReceivedAtUtc = heartbeat.Timestamp
        }, cancellationToken);
    }

    internal Task RecordHeartbeatAsync(CancellationToken cancellationToken) =>
        PublishHeartbeatAsync(cancellationToken);
}
