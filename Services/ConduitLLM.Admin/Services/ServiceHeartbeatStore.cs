using System.Collections.Concurrent;
using System.Text.Json;

using ConduitLLM.Admin.Interfaces;
using ConduitLLM.Core.Constants;

using StackExchange.Redis;

namespace ConduitLLM.Admin.Services
{
    /// <summary>
    /// Redis-backed <see cref="IServiceHeartbeatStore"/> (#1067). The last heartbeat is written
    /// under a short TTL so a present value always means "seen recently"; the reader still
    /// derives health from the recorded age, not mere key presence. When Redis is unavailable
    /// (single-instance/dev) it degrades to an in-process snapshot so the dashboard keeps
    /// working — mirrors the optional-<c>IConnectionMultiplexer</c> pattern used by
    /// <see cref="MediaCleanupStatusService"/>.
    /// </summary>
    public class ServiceHeartbeatStore : IServiceHeartbeatStore
    {
        /// <summary>
        /// TTL for the persisted snapshot. Comfortably longer than several heartbeat intervals
        /// so a brief gap doesn't evict it; the reader decides staleness from the age.
        /// </summary>
        private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(5);

        private readonly IConnectionMultiplexer? _redis;
        private readonly ILogger<ServiceHeartbeatStore> _logger;
        private readonly ConcurrentDictionary<string, ServiceHeartbeatSnapshot> _inProcess = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceHeartbeatStore"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="redis">The Redis connection, or <c>null</c> when Redis is not configured.</param>
        public ServiceHeartbeatStore(
            ILogger<ServiceHeartbeatStore> logger,
            IConnectionMultiplexer? redis = null)
        {
            _logger = logger;
            _redis = redis;
        }

        /// <inheritdoc />
        public async Task RecordAsync(ServiceHeartbeatSnapshot heartbeat, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(heartbeat);

            // Always keep an in-process copy so a Redis-less single instance still works.
            _inProcess[heartbeat.ServiceId] = heartbeat;

            if (_redis == null)
            {
                return;
            }

            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(heartbeat);
                await db.StringSetAsync(RedisKeys.ServiceHeartbeat.For(heartbeat.ServiceId), json, SnapshotTtl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to persist {ServiceId} heartbeat to Redis; using in-process fallback",
                    heartbeat.ServiceId);
            }
        }

        /// <inheritdoc />
        public async Task<ServiceHeartbeatSnapshot?> GetAsync(string serviceId, CancellationToken cancellationToken = default)
        {
            if (_redis != null)
            {
                try
                {
                    var db = _redis.GetDatabase();
                    var json = await db.StringGetAsync(RedisKeys.ServiceHeartbeat.For(serviceId));

                    // Redis is authoritative when present: a missing key means "not seen
                    // recently" (TTL expired or never written), so return null rather than a
                    // stale in-process copy.
                    return json.IsNullOrEmpty
                        ? null
                        : JsonSerializer.Deserialize<ServiceHeartbeatSnapshot>(json.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to read {ServiceId} heartbeat from Redis; falling back to in-process copy",
                        serviceId);
                }
            }

            return _inProcess.TryGetValue(serviceId, out var snapshot) ? snapshot : null;
        }
    }
}
