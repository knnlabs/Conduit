using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Admin.EventHandlers
{
    /// <summary>
    /// Records the Gateway's periodic liveness heartbeat (#1067) so the health dashboard can
    /// report the Gateway's real status from staleness instead of a hardcoded literal.
    /// </summary>
    /// <remarks>
    /// Failures are swallowed on purpose: a dropped heartbeat write must NOT trigger Wolverine
    /// redelivery (the next heartbeat, seconds later, supersedes it), so this handler never
    /// throws out of <see cref="HandleAsync"/>.
    /// </remarks>
    public class GatewayHeartbeatHandler : IEventHandler<GatewayHeartbeat>
    {
        private readonly IServiceHeartbeatStore _store;
        private readonly ILogger<GatewayHeartbeatHandler> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayHeartbeatHandler"/> class.
        /// </summary>
        public GatewayHeartbeatHandler(
            IServiceHeartbeatStore store,
            ILogger<GatewayHeartbeatHandler> logger)
        {
            _store = store;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task HandleAsync(GatewayHeartbeat message, IEventContext context)
        {
            try
            {
                await _store.RecordAsync(new ServiceHeartbeatSnapshot
                {
                    ServiceId = RedisKeys.ServiceHeartbeat.GatewayServiceId,
                    Heartbeat = message,
                    ReportedAtUtc = message.Timestamp,
                    ReceivedAtUtc = DateTime.UtcNow
                }, context.CancellationToken);
            }
            catch (Exception ex)
            {
                // Swallow — never redeliver a heartbeat (the next one supersedes it).
                _logger.LogWarning(ex,
                    "Failed to record Gateway heartbeat from instance {InstanceId}",
                    message.InstanceId);
            }
        }
    }
}
