using ConduitLLM.Gateway.Models;

using StackExchange.Redis;
using System.Text.Json;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRConnectionMonitor
    {
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
                _logger.LogWarning(ex, "Failed to get group count from Redis using pattern {Pattern}", $"{_groupConnectionsKeyPrefix}:*");
            }

            return Task.FromResult(0);
        }
    }
}
