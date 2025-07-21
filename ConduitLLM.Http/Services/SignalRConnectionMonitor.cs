using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Monitors SignalR connections and provides health metrics
    /// </summary>
    public class SignalRConnectionMonitor : IHostedService
    {
        private readonly ILogger<SignalRConnectionMonitor> _logger;
        private readonly ConcurrentDictionary<string, ConnectionInfo> _connections;
        private Timer? _cleanupTimer;

        public record ConnectionInfo(
            string ConnectionId,
            string? VirtualKeyId,
            DateTime ConnectedAt,
            DateTime LastActivity,
            string HubName,
            HashSet<string> SubscribedGroups);

        public SignalRConnectionMonitor(ILogger<SignalRConnectionMonitor> logger)
        {
            _logger = logger;
            _connections = new ConcurrentDictionary<string, ConnectionInfo>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cleanupTimer = new Timer(
                CleanupStaleConnections,
                null,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));

            _logger.LogInformation("SignalR connection monitor started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cleanupTimer?.Change(Timeout.Infinite, 0);
            _cleanupTimer?.Dispose();
            _logger.LogInformation("SignalR connection monitor stopped");
            return Task.CompletedTask;
        }

        public void AddConnection(string connectionId, string hubName, string? virtualKeyId)
        {
            var now = DateTime.UtcNow;
            var info = new ConnectionInfo(
                connectionId,
                virtualKeyId,
                now,
                now,
                hubName,
                new HashSet<string>());

            _connections.TryAdd(connectionId, info);
            _logger.LogDebug("Added connection {ConnectionId} for hub {HubName}", connectionId, hubName);
        }

        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var info))
            {
                var duration = DateTime.UtcNow - info.ConnectedAt;
                _logger.LogInformation(
                    "Removed connection {ConnectionId} for hub {HubName} after {Duration:F2}s with {GroupCount} groups",
                    connectionId, info.HubName, duration.TotalSeconds, info.SubscribedGroups.Count);
            }
        }

        public void UpdateActivity(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var info))
            {
                var updated = info with { LastActivity = DateTime.UtcNow };
                _connections.TryUpdate(connectionId, updated, info);
            }
        }

        public void AddGroupSubscription(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var info))
            {
                info.SubscribedGroups.Add(groupName);
                UpdateActivity(connectionId);
                _logger.LogDebug("Connection {ConnectionId} subscribed to group {GroupName}",
                    connectionId, groupName);
            }
        }

        public void RemoveGroupSubscription(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var info))
            {
                info.SubscribedGroups.Remove(groupName);
                UpdateActivity(connectionId);
                _logger.LogDebug("Connection {ConnectionId} unsubscribed from group {GroupName}",
                    connectionId, groupName);
            }
        }

        public ConnectionMetrics GetMetrics()
        {
            var now = DateTime.UtcNow;
            var activeConnections = 0;
            var totalGroups = 0;
            var connectionsByHub = new Dictionary<string, int>();
            var staleConnections = 0;

            foreach (var info in _connections.Values)
            {
                activeConnections++;
                totalGroups += info.SubscribedGroups.Count;

                if (!connectionsByHub.ContainsKey(info.HubName))
                    connectionsByHub[info.HubName] = 0;
                connectionsByHub[info.HubName]++;

                if (now - info.LastActivity > TimeSpan.FromMinutes(30))
                    staleConnections++;
            }

            return new ConnectionMetrics(
                activeConnections,
                totalGroups,
                connectionsByHub,
                staleConnections);
        }

        private void CleanupStaleConnections(object? state)
        {
            try
            {
                var staleThreshold = DateTime.UtcNow.AddHours(-1);
                var removedCount = 0;

                foreach (var kvp in _connections)
                {
                    if (kvp.Value.LastActivity < staleThreshold)
                    {
                        if (_connections.TryRemove(kvp.Key, out _))
                        {
                            removedCount++;
                            _logger.LogWarning(
                                "Removed stale connection {ConnectionId} for hub {HubName} (inactive since {LastActivity})",
                                kvp.Key, kvp.Value.HubName, kvp.Value.LastActivity);
                        }
                    }
                }

                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} stale SignalR connections", removedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stale connection cleanup");
            }
        }

        public record ConnectionMetrics(
            int ActiveConnections,
            int TotalGroups,
            Dictionary<string, int> ConnectionsByHub,
            int StaleConnections);
    }
}