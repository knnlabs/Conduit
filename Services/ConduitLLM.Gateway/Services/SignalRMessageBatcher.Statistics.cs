using System.Text.Json;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageBatcher
    {
        private async Task InitializeStatisticsAsync()
        {
            if (_redis == null) return;

            try
            {
                var exists = await _redis.HashExistsAsync(_statisticsKey, "totalMessagesBatched");
                if (!exists)
                {
                    var stats = new Dictionary<string, string>
                    {
                        ["totalMessagesBatched"] = "0",
                        ["totalBatchesSent"] = "0",
                        ["totalBatchLatency"] = "0",
                        ["lastBatchSentAt"] = DateTime.UtcNow.ToBinary().ToString()
                    };

                    await _redis.HashSetAsync(_statisticsKey, stats.Select(kvp => new HashEntry(kvp.Key, kvp.Value)).ToArray());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize statistics in Redis");
            }
        }

        public async Task<BatchingStatistics> GetStatisticsAsync()
        {
            if (_redis == null)
            {
                return new BatchingStatistics { IsBatchingEnabled = _isBatchingEnabled };
            }

            try
            {
                var globalStats = await _redis.HashGetAllAsync(_statisticsKey);
                var methodStats = await _redis.HashGetAllAsync(_messagesByMethodKey);
                var pendingMessages = await GetCurrentPendingMessagesAsync();

                var stats = new BatchingStatistics
                {
                    TotalMessagesBatched = GetLongValue(globalStats, "totalMessagesBatched"),
                    TotalBatchesSent = GetLongValue(globalStats, "totalBatchesSent"),
                    CurrentPendingMessages = pendingMessages,
                    LastBatchSentAt = GetDateTimeValue(globalStats, "lastBatchSentAt"),
                    IsBatchingEnabled = _isBatchingEnabled,
                    MessagesByMethod = methodStats.ToDictionary(kvp => kvp.Name.ToString(), kvp => (long)kvp.Value)
                };

                if (stats.TotalBatchesSent > 0)
                {
                    stats.AverageMessagesPerBatch = (double)stats.TotalMessagesBatched / stats.TotalBatchesSent;
                    var totalBatchLatency = GetLongValue(globalStats, "totalBatchLatency");
                    stats.AverageBatchLatency = TimeSpan.FromMilliseconds(totalBatchLatency / stats.TotalBatchesSent);
                    stats.NetworkCallsSaved = stats.TotalMessagesBatched - stats.TotalBatchesSent;
                    stats.BatchEfficiencyPercentage = (1.0 - ((double)stats.TotalBatchesSent / stats.TotalMessagesBatched)) * 100;
                }

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get statistics from Redis");
                return new BatchingStatistics { IsBatchingEnabled = _isBatchingEnabled };
            }
        }

        private long GetLongValue(HashEntry[] hashEntries, string key)
        {
            var entry = hashEntries.FirstOrDefault(h => h.Name == key);
            return entry.Value.HasValue && long.TryParse(entry.Value.ToString(), out var value) ? value : 0;
        }

        private DateTime GetDateTimeValue(HashEntry[] hashEntries, string key)
        {
            var entry = hashEntries.FirstOrDefault(h => h.Name == key);
            if (entry.Value.HasValue && long.TryParse(entry.Value.ToString(), out var binary))
            {
                try
                {
                    return DateTime.FromBinary(binary);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse timestamp from binary value, using current time");
                    return DateTime.UtcNow;
                }
            }
            return DateTime.UtcNow;
        }

        private async Task<long> GetCurrentPendingMessagesAsync()
        {
            try
            {
                var activeBatches = await _redis!.HashGetAllAsync(_activeBatchesKey);
                long totalPending = 0;

                foreach (var batchData in activeBatches)
                {
                    try
                    {
                        var batch = JsonSerializer.Deserialize<MessageBatch>(batchData.Value!.ToString());
                        if (batch != null)
                        {
                            totalPending += batch.Messages.Count;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize batch data for pending message count");
                    }
                }

                return totalPending;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get pending messages count");
                return 0;
            }
        }

        public async Task FlushAllBatchesAsync()
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot flush batches");
                return;
            }

            _logger.LogInformation("Flushing all pending batches");

            try
            {
                var activeBatches = await _redis.HashGetAllAsync(_activeBatchesKey);
                var flushTasks = new List<Task>();

                foreach (var batchEntry in activeBatches)
                {
                    try
                    {
                        var batch = JsonSerializer.Deserialize<MessageBatch>(batchEntry.Value!.ToString());
                        if (batch != null)
                        {
                            var batchKey = new BatchKey(batch.HubName, batch.MethodName, batch.ConnectionId, batch.GroupName);
                            flushTasks.Add(SendBatchAsync(batchKey, batch, batchEntry.Name!));
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize batch data during flush for key {Key}", batchEntry.Name);
                    }
                }

                await Task.WhenAll(flushTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing all batches");
            }
        }
    }
}
