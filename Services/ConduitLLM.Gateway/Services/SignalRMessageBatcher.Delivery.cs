using System.Text.Json;

using ConduitLLM.Gateway.Models;

using Microsoft.AspNetCore.SignalR;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageBatcher
    {
        private async Task SendBatchAsync(BatchKey batchKey, MessageBatch batch, string batchKeyString)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                // Remove batch from active batches
                var removed = await _redis.HashDeleteAsync(_activeBatchesKey, batchKeyString);
                if (!removed)
                {
                    return; // Batch already processed
                }

                var batchLatency = DateTime.UtcNow - batch.CreatedAt;

                if (batch.Messages.Count == 0)
                {
                    return;
                }

                var messagesToSend = new List<object>(batch.Messages);
                var messageCount = messagesToSend.Count;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var hubContext = GetHubContext(scope, batch.HubName);

                    if (hubContext == null)
                    {
                        _logger.LogError("Could not find hub context for {HubName}", batch.HubName);
                        return;
                    }

                    // Create batched message
                    var batchedMessage = new BatchedMessage
                    {
                        Messages = messagesToSend,
                        MethodName = batch.MethodName,
                        HubName = batch.HubName,
                        ConnectionId = batch.ConnectionId,
                        GroupName = batch.GroupName,
                        TotalSizeBytes = batch.TotalSizeBytes,
                        Priority = batch.Priority,
                        ContainsCriticalMessages = batch.ContainsCriticalMessages
                    };

                    // Send based on target
                    if (!string.IsNullOrEmpty(batch.ConnectionId))
                    {
                        await hubContext.Clients.Client(batch.ConnectionId)
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }
                    else if (!string.IsNullOrEmpty(batch.GroupName))
                    {
                        await hubContext.Clients.Group(batch.GroupName)
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }
                    else
                    {
                        await hubContext.Clients.All
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }

                    // Update statistics in Redis
                    await Task.WhenAll(
                        _redis.HashIncrementAsync(_statisticsKey, "totalBatchesSent"),
                        _redis.HashIncrementAsync(_statisticsKey, "totalBatchLatency", (long)batchLatency.TotalMilliseconds),
                        _redis.HashSetAsync(_statisticsKey, "lastBatchSentAt", DateTime.UtcNow.ToBinary().ToString())
                    );

                    _logger.LogDebug(
                        "Sent batch of {Count} messages for {HubName}.{MethodName}, latency: {Latency}ms",
                        messageCount, batch.HubName, batch.MethodName, batchLatency.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error sending batch for {HubName}.{MethodName}",
                        batch.HubName, batch.MethodName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendBatchAsync for key {Key}, hub {HubName}.{MethodName}", batchKeyString, batch.HubName, batch.MethodName);
            }
        }

        private async Task SendMessageDirectlyAsync(
            string hubName,
            string methodName,
            object message,
            string? connectionId,
            string? groupName)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var hubContext = GetHubContext(scope, hubName);

                if (hubContext == null)
                {
                    _logger.LogError("Could not find hub context for {HubName}", hubName);
                    return;
                }

                if (!string.IsNullOrEmpty(connectionId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(methodName, message);
                }
                else if (!string.IsNullOrEmpty(groupName))
                {
                    await hubContext.Clients.Group(groupName).SendAsync(methodName, message);
                }
                else
                {
                    await hubContext.Clients.All.SendAsync(methodName, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending message directly for {HubName}.{MethodName}",
                    hubName, methodName);
            }
        }

        private long EstimateMessageSize(object message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                return json.Length * sizeof(char);
            }
            catch
            {
                // Fallback estimate
                return 1024;
            }
        }

        private static IHubContext<Hub>? GetHubContext(IServiceScope scope, string hubName)
            => Utilities.SignalRHubContextResolver.Resolve(scope.ServiceProvider, hubName);
    }
}
