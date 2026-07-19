using System.Collections.Concurrent;

using StackExchange.Redis;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRConnectionMonitor
    {
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
                _logger.LogError(ex, "Failed to cleanup empty groups from prefix {Prefix}", _groupConnectionsKeyPrefix);
            }

            return 0;
        }
    }
}
