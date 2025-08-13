using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models.Realtime;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Manages active WebSocket connections for real-time audio streaming.
    /// </summary>
    public class RealtimeConnectionManager : IRealtimeConnectionManager, IHostedService
    {
        private readonly ILogger<RealtimeConnectionManager> _logger;
        private readonly IConfiguration? _configuration;
        private readonly ConcurrentDictionary<string, ManagedConnection> _connections = new();
        private readonly ConcurrentDictionary<int, HashSet<string>> _connectionsByVirtualKey = new();
        private readonly ConcurrentDictionary<string, string> _connectionToVirtualKey = new(); // For string-based virtual keys in tests
        private readonly Timer? _cleanupTimer;
        private readonly int _maxConnectionsPerKey;
        private readonly int _maxTotalConnections;
        private readonly TimeSpan _staleConnectionTimeout;

        public RealtimeConnectionManager(ILogger<RealtimeConnectionManager> logger)
            : this(logger, null)
        {
        }

        public RealtimeConnectionManager(
            ILogger<RealtimeConnectionManager> logger,
            IConfiguration? configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;

            // Load configuration with defaults
            _maxConnectionsPerKey = _configuration?.GetValue<int>("Realtime:MaxConnectionsPerKey", 5) ?? 5;
            _maxTotalConnections = _configuration?.GetValue<int>("Realtime:MaxTotalConnections", 1000) ?? 1000;
            _staleConnectionTimeout = TimeSpan.FromMinutes(_configuration?.GetValue<int>("Realtime:StaleConnectionTimeoutMinutes", 30) ?? 30);

            // Only setup cleanup timer if we have configuration
            if (_configuration != null)
            {
                _cleanupTimer = new Timer(
                    async _ => await CleanupStaleConnectionsAsync(),
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
            }
        }

        // Synchronous methods for testing
        public void RegisterConnection(string connectionId, string virtualKey, string model, string provider)
        {
            var connection = new ManagedConnection
            {
                Info = new ConduitLLM.Core.Models.Realtime.ConnectionInfo
                {
                    ConnectionId = connectionId,
                    Model = model,
                    ConnectedAt = DateTime.UtcNow,
                    State = "active",
                    StartTime = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    VirtualKey = virtualKey,
                    Provider = provider
                },
                VirtualKeyId = 0, // For testing
                LastHeartbeat = DateTime.UtcNow,
                IsHealthy = true
            };

            _connections.TryAdd(connectionId, connection);
            _connectionToVirtualKey.TryAdd(connectionId, virtualKey);
        }

        public void UnregisterConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                _connectionToVirtualKey.TryRemove(connectionId, out _);

                // Remove from per-key collection
                if (_connectionsByVirtualKey.TryGetValue(connection.VirtualKeyId, out var keyConnections))
                {
                    keyConnections.Remove(connectionId);

                    if (keyConnections.Count() == 0)
                    {
                        _connectionsByVirtualKey.TryRemove(connection.VirtualKeyId, out _);
                    }
                }
            }
        }

        public void UpdateConnectionProvider(string connectionId, string providerConnectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Info.ProviderConnectionId = providerConnectionId;
            }
        }

        public ConduitLLM.Core.Models.Realtime.ConnectionInfo? GetConnectionInfo(string connectionId)
        {
            return _connections.TryGetValue(connectionId, out var connection) ? connection.Info : null;
        }

        public List<ConduitLLM.Core.Models.Realtime.ConnectionInfo> GetConnectionsByVirtualKey(string virtualKey)
        {
            return _connections.Values
                .Where(c => _connectionToVirtualKey.TryGetValue(c.Info.ConnectionId, out var key) && key == virtualKey)
                .Select(c => c.Info)
                .ToList();
        }

        public void IncrementUsage(string connectionId, long audioBytes, long tokens, decimal cost)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Info.AudioBytesProcessed += audioBytes;
                connection.Info.TokensUsed += tokens;
                connection.Info.EstimatedCost += cost;
                connection.Info.LastActivity = DateTime.UtcNow;
            }
        }

        public List<ConduitLLM.Core.Models.Realtime.ConnectionInfo> GetActiveConnections()
        {
            return _connections.Values.Select(c => c.Info).ToList();
        }

        // Async interface methods
        public async Task RegisterConnectionAsync(
            string connectionId,
            int virtualKeyId,
            string model,
            WebSocket webSocket)
        {
            // Check total connection limit
            if (_connections.Count() >= _maxTotalConnections)
            {
                throw new InvalidOperationException($"Maximum total connections ({_maxTotalConnections}) reached");
            }

            // Check per-key limit
            if (await IsAtConnectionLimitAsync(virtualKeyId))
            {
                throw new InvalidOperationException($"Virtual key {virtualKeyId} has reached maximum connections ({_maxConnectionsPerKey})");
            }

            var connection = new ManagedConnection
            {
                Info = new ConduitLLM.Core.Models.Realtime.ConnectionInfo
                {
                    ConnectionId = connectionId,
                    Model = model,
                    ConnectedAt = DateTime.UtcNow,
                    State = "active",
                    StartTime = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                },
                WebSocket = webSocket,
                VirtualKeyId = virtualKeyId,
                LastHeartbeat = DateTime.UtcNow,
                IsHealthy = true
            };

            // Add to main collection
            if (!_connections.TryAdd(connectionId, connection))
            {
                throw new InvalidOperationException($"Connection {connectionId} already registered");
            }

            // Add to per-key collection
            _connectionsByVirtualKey.AddOrUpdate(
                virtualKeyId,
                new HashSet<string> { connectionId },
                (_, set) =>
                {
                    set.Add(connectionId);
                    return set;
                });

            _logger.LogInformation(
                "Registered connection {ConnectionId} for virtual key {VirtualKeyId}",
                connectionId, virtualKeyId);
        }

        public async Task UnregisterConnectionAsync(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var connection))
            {
                // Remove from per-key collection
                if (_connectionsByVirtualKey.TryGetValue(connection.VirtualKeyId, out var keyConnections))
                {
                    keyConnections.Remove(connectionId);

                    if (keyConnections.Count() == 0)
                    {
                        _connectionsByVirtualKey.TryRemove(connection.VirtualKeyId, out _);
                    }
                }

                _logger.LogInformation(
                    "Unregistered connection {ConnectionId} for virtual key {VirtualKeyId}",
                    connectionId.Replace(Environment.NewLine, ""), connection.VirtualKeyId);
            }

            await Task.CompletedTask;
        }

        public async Task<List<ConduitLLM.Core.Models.Realtime.ConnectionInfo>> GetActiveConnectionsAsync(int virtualKeyId)
        {
            var connections = new List<ConduitLLM.Core.Models.Realtime.ConnectionInfo>();

            if (_connectionsByVirtualKey.TryGetValue(virtualKeyId, out var connectionIds))
            {
                foreach (var id in connectionIds)
                {
                    if (_connections.TryGetValue(id, out var connection))
                    {
                        connections.Add(connection.Info);
                    }
                }
            }

            return await Task.FromResult(connections);
        }

        public async Task<int> GetTotalConnectionCountAsync()
        {
            return await Task.FromResult(_connections.Count());
        }

        public async Task<ConduitLLM.Core.Models.Realtime.ConnectionInfo?> GetConnectionAsync(string connectionId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                return await Task.FromResult(connection.Info);
            }

            return await Task.FromResult<ConduitLLM.Core.Models.Realtime.ConnectionInfo?>(null);
        }

        public async Task<bool> TerminateConnectionAsync(string connectionId, int virtualKeyId)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                // Verify ownership
                if (connection.VirtualKeyId != virtualKeyId)
                {
                    return false;
                }

                // Close WebSocket if still open
                if (connection.WebSocket != null && connection.WebSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await connection.WebSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection terminated by user",
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
_logger.LogError(ex, "Error closing WebSocket for connection {ConnectionId}", connectionId.Replace(Environment.NewLine, ""));
                    }
                }

                // Remove from collections
                await UnregisterConnectionAsync(connectionId);

                return true;
            }

            return false;
        }

        public async Task<bool> IsAtConnectionLimitAsync(int virtualKeyId)
        {
            if (_connectionsByVirtualKey.TryGetValue(virtualKeyId, out var connections))
            {
                return await Task.FromResult(connections.Count() >= _maxConnectionsPerKey);
            }

            return false;
        }

        public async Task UpdateUsageStatsAsync(string connectionId, ConnectionUsageStats stats)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                connection.Info.Usage = stats;
                connection.LastHeartbeat = DateTime.UtcNow;
            }

            await Task.CompletedTask;
        }

        public async Task<int> CleanupStaleConnectionsAsync()
        {
            var cleanedCount = 0;
            var now = DateTime.UtcNow;
            var staleConnections = new List<string>();

            foreach (var kvp in _connections)
            {
                var connection = kvp.Value;
                var timeSinceHeartbeat = now - connection.LastHeartbeat;

                // Check if connection is stale
                if (timeSinceHeartbeat > _staleConnectionTimeout)
                {
                    staleConnections.Add(kvp.Key);
                }
                // Check WebSocket state
                else if (connection.WebSocket != null &&
                         connection.WebSocket.State != WebSocketState.Open &&
                         connection.WebSocket.State != WebSocketState.Connecting)
                {
                    staleConnections.Add(kvp.Key);
                }
            }

            // Clean up stale connections
            foreach (var connectionId in staleConnections)
            {
                _logger.LogWarning(
                    "Cleaning up stale connection {ConnectionId}",
                    connectionId);

                if (_connections.TryRemove(connectionId, out var connection))
                {
                    await TerminateConnectionAsync(connectionId, connection.VirtualKeyId);
                    cleanedCount++;
                }
            }

            if (cleanedCount > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} stale connections",
                    cleanedCount);
            }

            return cleanedCount;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RealtimeConnectionManager started");
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RealtimeConnectionManager stopping...");

            // Stop cleanup timer
            if (_cleanupTimer != null)
            {
                await _cleanupTimer.DisposeAsync();
            }

            // Close all active connections
            var tasks = new List<Task>();

            foreach (var connection in _connections.Values)
            {
                if (connection.WebSocket != null && connection.WebSocket.State == WebSocketState.Open)
                {
                    tasks.Add(connection.WebSocket.CloseAsync(
                        WebSocketCloseStatus.EndpointUnavailable,
                        "Server shutting down",
                        cancellationToken));
                }
            }

            await Task.WhenAll(tasks);

            _connections.Clear();
            _connectionsByVirtualKey.Clear();

            _logger.LogInformation("RealtimeConnectionManager stopped");
        }
    }
}
