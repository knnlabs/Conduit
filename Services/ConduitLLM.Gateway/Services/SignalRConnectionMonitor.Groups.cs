using System.Text.Json;

using SignalRConnectionInfo = ConduitLLM.Gateway.Models.ConnectionInfo;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRConnectionMonitor
    {
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
    }
}
