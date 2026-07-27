using ConduitLLM.Core.Events;

namespace ConduitLLM.Admin.Interfaces
{
    /// <summary>
    /// Records and retrieves the most recent liveness heartbeat for a backend service
    /// (currently the Gateway, #1067). Backed by Redis when available so the snapshot is shared
    /// across Admin instances, with an in-process fallback for single-instance/dev where Redis
    /// is absent.
    /// </summary>
    public interface IServiceHeartbeatStore
    {
        /// <summary>Records the latest heartbeat for a service.</summary>
        Task RecordAsync(ServiceHeartbeatSnapshot heartbeat, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the most recent heartbeat for the given service id, or <c>null</c> if none has
        /// been recorded recently (never seen, or the stored snapshot has expired).
        /// </summary>
        Task<ServiceHeartbeatSnapshot?> GetAsync(string serviceId, CancellationToken cancellationToken = default);

        /// <summary>Gets all non-expired instance snapshots for a logical service.</summary>
        Task<IReadOnlyList<ServiceHeartbeatSnapshot>> GetAllAsync(
            string serviceId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A recorded service heartbeat. Serialized to Redis; the reader compares
    /// <see cref="ReceivedAtUtc"/> against <see cref="GatewayHeartbeat.IntervalSeconds"/> to judge staleness.
    /// </summary>
    public sealed class ServiceHeartbeatSnapshot
    {
        /// <summary>Logical service identifier (e.g. "gateway").</summary>
        public string ServiceId { get; set; } = string.Empty;

        /// <summary>The heartbeat payload reported by the service instance.</summary>
        public GatewayHeartbeat Heartbeat { get; set; } = new();

        /// <summary>When the service reported the heartbeat (its clock).</summary>
        public DateTime ReportedAtUtc { get; set; }

        /// <summary>When the Admin recorded it (Admin's clock — used for staleness, avoiding cross-service clock skew).</summary>
        public DateTime ReceivedAtUtc { get; set; }
    }
}
