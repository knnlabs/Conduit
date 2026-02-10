using ConduitLLM.Configuration.Services;
using ConduitLLM.Gateway.Interfaces;

using System.Collections.Concurrent;

using StackExchange.Redis;
using System.Text.Json;

using Microsoft.AspNetCore.SignalR;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Gateway.Services
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
        ConduitLLM.Gateway.Models.ConnectionInfo? GetConnection(string connectionId);

        /// <summary>
        /// Gets all active connections
        /// </summary>
        IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo> GetActiveConnections();

        /// <summary>
        /// Gets connections for a specific hub
        /// </summary>
        IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo> GetHubConnections(string hubName);

        /// <summary>
        /// Gets connections for a specific virtual key
        /// </summary>
        IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo> GetVirtualKeyConnections(int virtualKeyId);

        /// <summary>
        /// Gets connections in a specific group
        /// </summary>
        IEnumerable<ConduitLLM.Gateway.Models.ConnectionInfo> GetGroupConnections(string groupName);

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
    /// Implementation of SignalR connection monitor using Redis
    /// </summary>
    public class SignalRConnectionMonitor : ISignalRConnectionMonitor, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRConnectionMonitor> _logger;
        private readonly IConfiguration _configuration;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        
        private Timer? _cleanupTimer;
        private IDatabase? _redis;
        private IServer? _server;

        // Redis keys
        private readonly string _connectionsKey;
        private readonly string _groupConnectionsKeyPrefix;
        
        private readonly TimeSpan _staleConnectionThreshold;
        private readonly TimeSpan _cleanupInterval;

        public SignalRConnectionMonitor(
            ILogger<SignalRConnectionMonitor> logger,
            IConfiguration configuration,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _redisConnectionFactory = redisConnectionFactory;

            // Redis keys
            _connectionsKey = "signalr:connections";
            _groupConnectionsKeyPrefix = "signalr:groups";

            _staleConnectionThreshold = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:StaleThresholdMinutes", 60));
            _cleanupInterval = TimeSpan.FromMinutes(
                configuration.GetValue<int>("SignalR:ConnectionMonitor:CleanupIntervalMinutes", 5));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor starting");

            try
            {
                var connection = await _redisConnectionFactory.GetConnectionAsync();
                _redis = connection.GetDatabase();
                _server = connection.GetServer(connection.GetEndPoints().First());

                _cleanupTimer = new Timer(
                    CleanupStaleConnections,
                    null,
                    _cleanupInterval,
                    _cleanupInterval);

                _logger.LogInformation("SignalR Connection Monitor started with Redis backend");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR Connection Monitor");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Connection Monitor stopping");

            _cleanupTimer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public async Task OnConnectionAsync(string connectionId, string hubName, HubCallerContext context)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot track connection {ConnectionId}", connectionId);
                return;
            }

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

            try
            {
                var connectionData = JsonSerializer.Serialize(connectionInfo);
                await _redis.HashSetAsync(_connectionsKey, connectionId, connectionData);

                // Set expiration on the connection data to auto-cleanup stale connections
                // Note: HashFieldExpireAsync might not be available in older Redis versions
                // Instead, we rely on cleanup timer for now
                // TODO: Implement per-field TTL when Redis version supports it

                _logger.LogDebug(
                    "Connection {ConnectionId} established on {HubName} from {IpAddress} using {Transport}",
                    connectionId, hubName, connectionInfo.IpAddress, connectionInfo.TransportType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to track connection {ConnectionId}", connectionId);
                // Don't throw - connection tracking failure shouldn't break the connection
            }
        }

        public async Task OnDisconnectionAsync(string connectionId)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot remove connection {ConnectionId}", connectionId);
                return;
            }

            try
            {
                // Get connection info before removing it
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                SignalRConnectionInfo? connectionInfo = null;

                if (connectionData.HasValue)
                {
                    try
                    {
                        connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionId);
                    }
                }

                // Remove from connections hash
                await _redis.HashDeleteAsync(_connectionsKey, connectionId);

                // Remove from all groups - scan group keys for this connection
                if (_server != null)
                {
                    var tasks = new List<Task>();
                    foreach (var groupKey in _server.Keys(pattern: $"{_groupConnectionsKeyPrefix}:*"))
                    {
                        tasks.Add(_redis.SetRemoveAsync(groupKey, connectionId));
                    }
                    await Task.WhenAll(tasks);
                }

                if (connectionInfo != null)
                {
                    _logger.LogDebug(
                        "Connection {ConnectionId} disconnected after {Duration}min with {MessagesSent} messages sent, {MessagesAcked} acknowledged",
                        connectionId, 
                        connectionInfo.ConnectionDuration.TotalMinutes,
                        connectionInfo.MessagesSent,
                        connectionInfo.MessagesAcknowledged);
                }
                else
                {
                    _logger.LogDebug("Connection {ConnectionId} disconnected", connectionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove connection {ConnectionId}", connectionId);
                // Don't throw - disconnection cleanup failure shouldn't break the disconnection
            }
        }

        public async Task RecordActivityAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record activity for connection {ConnectionId}", connectionId);
            }
        }

        public async Task AddToGroupAsync(string connectionId, string groupName)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                // Update connection info to include the group
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.Groups.Add(groupName);
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                    }
                }

                // Add connection to group set
                var groupKey = $"{_groupConnectionsKeyPrefix}:{groupName}";
                await _redis.SetAddAsync(groupKey, connectionId);

                _logger.LogDebug(
                    "Connection {ConnectionId} added to group {GroupName}",
                    connectionId, groupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add connection {ConnectionId} to group {GroupName}", connectionId, groupName);
            }
        }

        public async Task RemoveFromGroupAsync(string connectionId, string groupName)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                // Update connection info to remove the group
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.Groups.Remove(groupName);
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                    }
                }

                // Remove connection from group set
                var groupKey = $"{_groupConnectionsKeyPrefix}:{groupName}";
                await _redis.SetRemoveAsync(groupKey, connectionId);

                _logger.LogDebug(
                    "Connection {ConnectionId} removed from group {GroupName}",
                    connectionId, groupName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove connection {ConnectionId} from group {GroupName}", connectionId, groupName);
            }
        }

        public async Task RecordMessageSentAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.MessagesSent++;
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record message sent for connection {ConnectionId}", connectionId);
            }
        }

        public async Task RecordMessageAcknowledgedAsync(string connectionId)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                    if (connectionInfo != null)
                    {
                        connectionInfo.MessagesAcknowledged++;
                        connectionInfo.LastActivityAt = DateTime.UtcNow;
                        var updatedData = JsonSerializer.Serialize(connectionInfo);
                        await _redis.HashSetAsync(_connectionsKey, connectionId, updatedData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record message acknowledged for connection {ConnectionId}", connectionId);
            }
        }

        public async Task<SignalRConnectionInfo?> GetConnectionAsync(string connectionId)
        {
            if (_redis == null)
            {
                return null;
            }

            try
            {
                var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId);
                if (connectionData.HasValue)
                {
                    return JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get connection {ConnectionId}", connectionId);
            }

            return null;
        }

        [Obsolete("Use GetConnectionAsync instead. This synchronous method may cause thread pool starvation.")]
        public SignalRConnectionInfo? GetConnection(string connectionId)
        {
            // Synchronous wrapper for backward compatibility
            return GetConnectionAsync(connectionId).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<SignalRConnectionInfo>> GetActiveConnectionsAsync()
        {
            if (_redis == null)
            {
                return Enumerable.Empty<SignalRConnectionInfo>();
            }

            try
            {
                var allConnections = await _redis.HashGetAllAsync(_connectionsKey);
                var activeConnections = new List<SignalRConnectionInfo>();

                foreach (var connectionData in allConnections)
                {
                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.Value!.ToString());
                        if (connectionInfo != null && !connectionInfo.IsStale(_staleConnectionThreshold))
                        {
                            activeConnections.Add(connectionInfo);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionData.Name);
                    }
                }

                return activeConnections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get active connections");
                return Enumerable.Empty<SignalRConnectionInfo>();
            }
        }

        [Obsolete("Use GetActiveConnectionsAsync instead. This synchronous method may cause thread pool starvation.")]
        public IEnumerable<SignalRConnectionInfo> GetActiveConnections()
        {
            // Synchronous wrapper for backward compatibility
            return GetActiveConnectionsAsync().GetAwaiter().GetResult();
        }

        [Obsolete("Use GetHubConnectionsAsync instead. This synchronous method may cause thread pool starvation.")]
        public IEnumerable<SignalRConnectionInfo> GetHubConnections(string hubName)
        {
            // Synchronous wrapper for backward compatibility
            return GetHubConnectionsAsync(hubName).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<SignalRConnectionInfo>> GetHubConnectionsAsync(string hubName)
        {
            if (_redis == null)
            {
                return Enumerable.Empty<SignalRConnectionInfo>();
            }

            try
            {
                var allConnections = await _redis.HashGetAllAsync(_connectionsKey);
                var hubConnections = new List<SignalRConnectionInfo>();

                foreach (var connectionData in allConnections)
                {
                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.Value!.ToString());
                        if (connectionInfo != null && connectionInfo.HubName == hubName && !connectionInfo.IsStale(_staleConnectionThreshold))
                        {
                            hubConnections.Add(connectionInfo);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionData.Name);
                    }
                }

                return hubConnections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get hub connections for {HubName}", hubName);
                return Enumerable.Empty<SignalRConnectionInfo>();
            }
        }

        [Obsolete("Use GetVirtualKeyConnectionsAsync instead. This synchronous method may cause thread pool starvation.")]
        public IEnumerable<SignalRConnectionInfo> GetVirtualKeyConnections(int virtualKeyId)
        {
            // Synchronous wrapper for backward compatibility
            return GetVirtualKeyConnectionsAsync(virtualKeyId).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<SignalRConnectionInfo>> GetVirtualKeyConnectionsAsync(int virtualKeyId)
        {
            if (_redis == null)
            {
                return Enumerable.Empty<SignalRConnectionInfo>();
            }

            try
            {
                var allConnections = await _redis.HashGetAllAsync(_connectionsKey);
                var virtualKeyConnections = new List<SignalRConnectionInfo>();

                foreach (var connectionData in allConnections)
                {
                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.Value!.ToString());
                        if (connectionInfo != null && connectionInfo.VirtualKeyId == virtualKeyId && !connectionInfo.IsStale(_staleConnectionThreshold))
                        {
                            virtualKeyConnections.Add(connectionInfo);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionData.Name);
                    }
                }

                return virtualKeyConnections;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get virtual key connections for {VirtualKeyId}", virtualKeyId);
                return Enumerable.Empty<SignalRConnectionInfo>();
            }
        }

        [Obsolete("Use GetGroupConnectionsAsync instead. This synchronous method may cause thread pool starvation.")]
        public IEnumerable<SignalRConnectionInfo> GetGroupConnections(string groupName)
        {
            // Synchronous wrapper for backward compatibility
            return GetGroupConnectionsAsync(groupName).GetAwaiter().GetResult();
        }

        public async Task<IEnumerable<SignalRConnectionInfo>> GetGroupConnectionsAsync(string groupName)
        {
            if (_redis == null)
            {
                return Enumerable.Empty<SignalRConnectionInfo>();
            }

            try
            {
                var groupKey = $"{_groupConnectionsKeyPrefix}:{groupName}";
                var connectionIds = await _redis.SetMembersAsync(groupKey);

                var groupConnections = new List<SignalRConnectionInfo>();
                var tasks = connectionIds.Select(async connectionId =>
                {
                    try
                    {
                        var connectionData = await _redis.HashGetAsync(_connectionsKey, connectionId!);
                        if (connectionData.HasValue)
                        {
                            var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(connectionData.ToString());
                            if (connectionInfo != null && !connectionInfo.IsStale(_staleConnectionThreshold))
                            {
                                return connectionInfo;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", connectionId);
                    }
                    return null;
                });

                var results = await Task.WhenAll(tasks);
                return results.Where(c => c != null).Cast<SignalRConnectionInfo>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get group connections for {GroupName}", groupName);
                return Enumerable.Empty<SignalRConnectionInfo>();
            }
        }

        [Obsolete("Use GetStatisticsAsync instead. This synchronous method may cause thread pool starvation.")]
        public ConnectionStatistics GetStatistics()
        {
            // Synchronous wrapper for backward compatibility
            return GetStatisticsAsync().GetAwaiter().GetResult();
        }

        public async Task<ConnectionStatistics> GetStatisticsAsync()
        {
            if (_redis == null)
            {
                return new ConnectionStatistics();
            }

            try
            {
                var allConnections = await GetAllConnectionsFromRedisAsync();
                List<Models.ConnectionInfo> activeConnections = [..allConnections.Where(c => !c.IsStale(_staleConnectionThreshold))];

                var stats = new ConnectionStatistics
                {
                    TotalActiveConnections = activeConnections.Count,
                    StaleConnections = allConnections.Count - activeConnections.Count,
                    TotalGroups = await GetGroupCountAsync(),
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

                if (activeConnections.Count > 0)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get connection statistics");
                return new ConnectionStatistics();
            }
        }

        private async Task<List<SignalRConnectionInfo>> GetAllConnectionsFromRedisAsync()
        {
            var allConnections = new List<SignalRConnectionInfo>();

            try
            {
                var connectionData = await _redis!.HashGetAllAsync(_connectionsKey);
                foreach (var data in connectionData)
                {
                    try
                    {
                        var connectionInfo = JsonSerializer.Deserialize<SignalRConnectionInfo>(data.Value!.ToString());
                        if (connectionInfo != null)
                        {
                            allConnections.Add(connectionInfo);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize connection info for {ConnectionId}", data.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all connections from Redis");
            }

            return allConnections;
        }

        private Task<int> GetGroupCountAsync()
        {
            try
            {
                if (_server != null)
                {
                    return Task.FromResult(_server.Keys(pattern: $"{_groupConnectionsKeyPrefix}:*").Count());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get group count from Redis");
            }

            return Task.FromResult(0);
        }

        private void CleanupStaleConnections(object? state)
        {
            // Fire-and-forget with proper exception handling - don't use async void
            _ = CleanupStaleConnectionsAsync();
        }

        private async Task CleanupStaleConnectionsAsync()
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                var allConnections = await GetAllConnectionsFromRedisAsync();
                List<Models.ConnectionInfo> staleConnections = [
                    ..allConnections.Where(c => c.IsStale(_staleConnectionThreshold))
                ];

                var cleanupTasks = new List<Task>();
                foreach (var connection in staleConnections)
                {
                    cleanupTasks.Add(CleanupStaleConnectionAsync(connection));
                }

                await Task.WhenAll(cleanupTasks);

                // Clean up empty groups
                var emptyGroupCount = await CleanupEmptyGroupsAsync();

                if (staleConnections.Count > 0)
                {
                    _logger.LogInformation(
                        "Cleaned up {Count} stale connections and {GroupCount} empty groups",
                        staleConnections.Count, emptyGroupCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stale connection cleanup");
            }
        }

        private async Task CleanupStaleConnectionAsync(SignalRConnectionInfo connection)
        {
            try
            {
                // Remove from connections hash
                await _redis!.HashDeleteAsync(_connectionsKey, connection.ConnectionId);

                // Remove from all groups
                if (_server != null)
                {
                    var removalTasks = new List<Task>();
                    foreach (var groupKey in _server.Keys(pattern: $"{_groupConnectionsKeyPrefix}:*"))
                    {
                        removalTasks.Add(_redis.SetRemoveAsync(groupKey, connection.ConnectionId));
                    }
                    await Task.WhenAll(removalTasks);
                }

                _logger.LogWarning(
                    "Cleaned up stale connection {ConnectionId} from {HubName} (idle for {IdleMinutes}min)",
                    connection.ConnectionId,
                    connection.HubName,
                    connection.IdleTime.TotalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup stale connection {ConnectionId}", connection.ConnectionId);
            }
        }

        private async Task<int> CleanupEmptyGroupsAsync()
        {
            try
            {
                if (_server != null)
                {
                    var allGroupKeys = _server.Keys(pattern: $"{_groupConnectionsKeyPrefix}:*").ToArray();
                    var emptyGroups = new ConcurrentBag<RedisKey>();
                    var checkTasks = allGroupKeys.Select(async groupKey =>
                    {
                        var count = await _redis!.SetLengthAsync(groupKey);
                        if (count == 0)
                        {
                            emptyGroups.Add(groupKey);
                        }
                    });

                    await Task.WhenAll(checkTasks);

                    if (!emptyGroups.IsEmpty)
                    {
                        await _redis!.KeyDeleteAsync(emptyGroups.ToArray());
                    }

                    return emptyGroups.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup empty groups");
            }

            return 0;
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
}