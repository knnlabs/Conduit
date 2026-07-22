namespace ConduitLLM.Core.Events
{
    /// <summary>
    /// Periodic liveness heartbeat published by every Gateway ("core-api") instance so the
    /// Admin health dashboard can report the Gateway's real status from staleness instead of a
    /// hardcoded literal (#1067). Consumed by the Admin's <c>GatewayHeartbeatHandler</c>, which
    /// records the last-seen snapshot; the dashboard derives health from how stale that
    /// snapshot is.
    /// </summary>
    /// <remarks>
    /// Publishing liveness as an event keeps Admin↔Gateway strictly event-driven (epic #909) —
    /// no synchronous Admin→Gateway HTTP probe. The base <see cref="DomainEvent.Timestamp"/>
    /// carries the Gateway's publish time.
    /// </remarks>
    public record GatewayHeartbeat : DomainEvent
    {
        /// <summary>Identifies the reporting Gateway instance (machine name + process id).</summary>
        public string InstanceId { get; init; } = string.Empty;

        /// <summary>Assembly version of the reporting Gateway.</summary>
        public string Version { get; init; } = string.Empty;

        /// <summary>Wall-clock seconds the reporting Gateway process has been running.</summary>
        public double UptimeSeconds { get; init; }

        /// <summary>
        /// The publisher's heartbeat cadence in seconds, so the consumer can judge staleness
        /// without sharing configuration between the two services.
        /// </summary>
        public double IntervalSeconds { get; init; }
    }
}
