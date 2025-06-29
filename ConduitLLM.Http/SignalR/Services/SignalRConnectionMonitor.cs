using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalRConnectionInfo = ConduitLLM.Http.SignalR.Models.ConnectionInfo;

namespace ConduitLLM.Http.SignalR.Services
{
    /// <summary>
    /// Service that monitors SignalR connections
    /// </summary>
    public interface ISignalRConnectionMonitor
    {
        /// <summary>
        /// Records a new connection
        /// </summary>
        Task OnConnectionAsync(string connectionId, string hubName, HubCallerContext context);

        /// <summary>
        /// Records a disconnection
        /// </summary>
        Task OnDisconnectionAsync(string connectionId);

        /// <summary>
        /// Records activity on a connection
        /// </summary>
        Task RecordActivityAsync(string connectionId);

        /// <summary>
        /// Adds a connection to a group
        /// </summary>
        Task AddToGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// Removes a connection from a group
        /// </summary>
        Task RemoveFromGroupAsync(string connectionId, string groupName);

        /// <summary>
        /// Gets information about a specific connection
        /// </summary>
        ConduitLLM.Http.SignalR.Models.ConnectionInfo? GetConnection(string connectionId);

        /// <summary>
        /// Gets all active connections
        /// </summary>
        IEnumerable<ConduitLLM.Http.SignalR.Models.ConnectionInfo> GetActiveConnections();

        /// <summary>
        /// Gets connections for a specific hub
        /// </summary>
        IEnumerable<ConduitLLM.Http.SignalR.Models.ConnectionInfo> GetHubConnections(string hubName);

        /// <summary>
        /// Gets connections for a specific virtual key
        /// </summary>
        IEnumerable<ConduitLLM.Http.SignalR.Models.ConnectionInfo> GetVirtualKeyConnections(int virtualKeyId);

        /// <summary>
        /// Gets connections in a specific group
        /// </summary>
        IEnumerable<ConduitLLM.Http.SignalR.Models.ConnectionInfo> GetGroupConnections(string groupName);

        /// <summary>
        /// Gets monitoring statistics
        /// </summary>
        ConnectionStatistics GetStatistics();

        /// <summary>
        /// Records a message sent to a connection
        /// </summary>
        Task RecordMessageSentAsync(string connectionId);

        /// <summary>
        /// Records a message acknowledged by a connection
        /// </summary>
        Task RecordMessageAcknowledgedAsync(string connectionId);
    }

    /// <summary>
    /// Statistics about SignalR connections
    /// </summary>
    public class ConnectionStatistics
    {
        public int TotalActiveConnections { get; set; }
        public Dictionary<string, int> ConnectionsByHub { get; set; } = new();
        public Dictionary<string, int> ConnectionsByTransport { get; set; } = new();
        public int TotalGroups { get; set; }
        public int StaleConnections { get; set; }
        public double AverageConnectionDurationMinutes { get; set; }
        public double AverageIdleTimeMinutes { get; set; }
        public DateTime OldestConnectionTime { get; set; }
        public DateTime NewestConnectionTime { get; set; }
        public long TotalMessagesSent { get; set; }
        public long TotalMessagesAcknowledged { get; set; }
        public double AcknowledgmentRate { get; set; }
    }

    /// <summary>
    /// Implementation of SignalR connection monitor
    /// </summary>
    public class SignalRConnectionMonitor : ISignalRConnectionMonitor, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRConnectionMonitor> _logger;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, SignalRConnectionInfo> _connections = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _groupConnections = new();
        private Timer? _cleanupTimer;
        private readonly TimeSpan _staleConnectionThreshold;
        private readonly TimeSpan _cleanupInterval;

        public SignalRConnectionMonitor(
            ILogger<SignalRConnectionMonitor> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _staleConnectionThreshold = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:StaleThresholdMinutes", 60));
            _cleanupInterval = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:CleanupIntervalMinutes", 5));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor starting");

            _cleanupTimer = new Timer(
                CleanupStaleConnections,
                null,
                _cleanupInterval,
                _cleanupInterval);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor stopping");

            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public Task OnConnectionAsync(string connectionId, string hubName, HubCallerContext context)
        {
            var connectionInfo = new SignalRConnectionInfo
            {
                ConnectionId = connectionId,
                HubName = hubName,
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                UserAgent = context.GetHttpContext()?.Request.Headers["User-Agent"].ToString(),
                IpAddress = context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString(),
                TransportType = context.Features.Get<IHttpTransportFeature>()?.TransportType.ToString()
            };

            // Extract virtual key ID from context
            if (context.Items.TryGetValue("VirtualKeyId", out var virtualKeyIdObj) && 
                virtualKeyIdObj is int virtualKeyId)
            {
                connectionInfo.VirtualKeyId = virtualKeyId;
            }

            _connections.TryAdd(connectionId, connectionInfo);

            _logger.LogDebug(
                "Connection {ConnectionId} established on {HubName} from {IpAddress} using {Transport}",
                connectionId, hubName, connectionInfo.IpAddress, connectionInfo.TransportType);

            return Task.CompletedTask;
        }

        public Task OnDisconnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connectionInfo))
            {
                // Remove from all groups
                foreach (var kvp in _groupConnections)
                {
                    kvp.Value.Remove(connectionId);
                }

                _logger.LogDebug(
                    "Connection {ConnectionId} disconnected after {Duration}min with {MessagesSent} messages sent, {MessagesAcked} acknowledged",
                    connectionId, 
                    connectionInfo.ConnectionDuration.TotalMinutes,
                    connectionInfo.MessagesSent,
                    connectionInfo.MessagesAcknowledged);
            }

            return Task.CompletedTask;
        }

        public Task RecordActivityAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task AddToGroupAsync(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.Groups.Add(groupName);
                
                _groupConnections.AddOrUpdate(
                    groupName,
                    new HashSet<string> { connectionId },
                    (_, set) => { set.Add(connectionId); return set; });

                _logger.LogDebug(
                    "Connection {ConnectionId} added to group {GroupName}",
                    connectionId, groupName);
            }

            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.Groups.Remove(groupName);
                
                if (_groupConnections.TryGetValue(groupName, out var connections))
                {
                    connections.Remove(connectionId);
                }

                _logger.LogDebug(
                    "Connection {ConnectionId} removed from group {GroupName}",
                    connectionId, groupName);
            }

            return Task.CompletedTask;
        }

        public Task RecordMessageSentAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.MessagesSent++;
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task RecordMessageAcknowledgedAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connectionInfo))
            {
                connectionInfo.MessagesAcknowledged++;
                connectionInfo.LastActivityAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public SignalRConnectionInfo? GetConnection(string connectionId)
        {
            return _connections.TryGetValue(connectionId, out var connectionInfo) ? connectionInfo : null;
        }

        public IEnumerable<SignalRConnectionInfo> GetActiveConnections()
        {
            return _connections.Values.Where(c => !c.IsStale(_staleConnectionThreshold));
        }

        public IEnumerable<SignalRConnectionInfo> GetHubConnections(string hubName)
        {
            return _connections.Values.Where(c => c.HubName == hubName);
        }

        public IEnumerable<SignalRConnectionInfo> GetVirtualKeyConnections(int virtualKeyId)
        {
            return _connections.Values.Where(c => c.VirtualKeyId == virtualKeyId);
        }

        public IEnumerable<SignalRConnectionInfo> GetGroupConnections(string groupName)
        {
            if (_groupConnections.TryGetValue(groupName, out var connectionIds))
            {
                return connectionIds
                    .Select(id => _connections.TryGetValue(id, out var conn) ? conn : null)
                    .Where(c => c != null)
                    .Cast<SignalRConnectionInfo>();
            }

            return Enumerable.Empty<SignalRConnectionInfo>();
        }

        public ConnectionStatistics GetStatistics()
        {
            var allConnections = _connections.Values.ToList();
            var activeConnections = allConnections.Where(c => !c.IsStale(_staleConnectionThreshold)).ToList();
            
            var stats = new ConnectionStatistics
            {
                TotalActiveConnections = activeConnections.Count,
                StaleConnections = allConnections.Count - activeConnections.Count,
                TotalGroups = _groupConnections.Count,
                TotalMessagesSent = allConnections.Sum(c => c.MessagesSent),
                TotalMessagesAcknowledged = allConnections.Sum(c => c.MessagesAcknowledged)
            };

            // Connections by hub
            stats.ConnectionsByHub = activeConnections
                .GroupBy(c => c.HubName)
                .ToDictionary(g => g.Key, g => g.Count());

            // Connections by transport
            stats.ConnectionsByTransport = activeConnections
                .Where(c => c.TransportType != null)
                .GroupBy(c => c.TransportType!)
                .ToDictionary(g => g.Key, g => g.Count());

            if (activeConnections.Any())
            {
                stats.AverageConnectionDurationMinutes = activeConnections
                    .Average(c => c.ConnectionDuration.TotalMinutes);
                stats.AverageIdleTimeMinutes = activeConnections
                    .Average(c => c.IdleTime.TotalMinutes);
                stats.OldestConnectionTime = activeConnections
                    .Min(c => c.ConnectedAt);
                stats.NewestConnectionTime = activeConnections
                    .Max(c => c.ConnectedAt);
            }

            if (stats.TotalMessagesSent > 0)
            {
                stats.AcknowledgmentRate = (double)stats.TotalMessagesAcknowledged / stats.TotalMessagesSent * 100;
            }

            return stats;
        }

        private void CleanupStaleConnections(object? state)
        {
            try
            {
                var staleConnections = _connections.Values
                    .Where(c => c.IsStale(_staleConnectionThreshold))
                    .ToList();

                foreach (var connection in staleConnections)
                {
                    if (_connections.TryRemove(connection.ConnectionId, out _))
                    {
                        // Remove from all groups
                        foreach (var kvp in _groupConnections)
                        {
                            kvp.Value.Remove(connection.ConnectionId);
                        }

                        _logger.LogWarning(
                            "Cleaned up stale connection {ConnectionId} from {HubName} (idle for {IdleMinutes}min)",
                            connection.ConnectionId, 
                            connection.HubName,
                            connection.IdleTime.TotalMinutes);
                    }
                }

                // Clean up empty groups
                var emptyGroups = _groupConnections
                    .Where(kvp => kvp.Value.Count == 0)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var group in emptyGroups)
                {
                    _groupConnections.TryRemove(group, out _);
                }

                if (staleConnections.Count > 0)
                {
                    _logger.LogInformation(
                        "Cleaned up {Count} stale connections and {GroupCount} empty groups",
                        staleConnections.Count, emptyGroups.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stale connection cleanup");
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }

    /// <summary>
    /// Interface to get transport type from SignalR features
    /// </summary>
    public interface IHttpTransportFeature
    {
        HttpTransportType TransportType { get; }
    }

    /// <summary>
    /// HTTP transport types
    /// </summary>
    public enum HttpTransportType
    {
        WebSockets,
        ServerSentEvents,
        LongPolling
    }
}