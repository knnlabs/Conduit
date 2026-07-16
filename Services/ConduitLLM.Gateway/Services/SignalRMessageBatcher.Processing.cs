using System.Text.Json;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageBatcher
    {
        /// <summary>
        /// Processes batch signals from the channel with proper error handling.
        /// This replaces fire-and-forget Task.Run patterns.
        /// </summary>
        private async Task ProcessSignalsAsync(CancellationToken ct)
        {
            _logger.LogDebug("Signal processing task started");

            try
            {
                await foreach (var signal in _signalChannel.Reader.ReadAllAsync(ct))
                {
                    try
                    {
                        switch (signal)
                        {
                            case BatchSignal.ProcessBatches:
                                await ProcessBatchesAsync();
                                break;
                            case BatchSignal.FlushAll:
                                await FlushAllBatchesAsync();
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch signal {Signal}", signal);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _logger.LogDebug("Signal processing task cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in signal processing task");
            }

            _logger.LogDebug("Signal processing task completed");
        }

        private void ProcessBatches(object? state)
        {
            // Signal to process batches via channel (replaces fire-and-forget Task.Run)
            // The signal processing task handles this with proper error handling
            _signalChannel.Writer.TryWrite(BatchSignal.ProcessBatches);
        }

        private async Task ProcessBatchesAsync()
        {
            if (!await _batchProcessingLock.WaitAsync(0))
            {
                // Already processing
                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                if (_redis == null)
                {
                    return;
                }

                var batchesToSend = new List<(BatchKey Key, MessageBatch Batch, string KeyString)>();

                // Check all active batches
                var activeBatches = await _redis.HashGetAllAsync(_activeBatchesKey);
                foreach (var batchEntry in activeBatches)
                {
                    try
                    {
                        var batch = JsonSerializer.Deserialize<MessageBatch>(batchEntry.Value!.ToString());
                        if (batch != null && batch.Messages.Count > 0 &&
                            (now - batch.CreatedAt >= _batchWindow || batch.IsQueued))
                        {
                            var batchKey = new BatchKey(batch.HubName, batch.MethodName, batch.ConnectionId, batch.GroupName);
                            batchesToSend.Add((batchKey, batch, batchEntry.Name!));
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize batch data for key {Key}", batchEntry.Name);
                    }
                }

                // Send all ready batches
                var sendTasks = batchesToSend.Select(item => SendBatchAsync(item.Key, item.Batch, item.KeyString));
                await Task.WhenAll(sendTasks);

                // Process explicitly queued batches
                string? queuedBatchKey;
                while ((queuedBatchKey = await _redis.ListLeftPopAsync(_batchQueueKey)) != null)
                {
                    var batchData = await _redis.HashGetAsync(_activeBatchesKey, queuedBatchKey);
                    if (batchData.HasValue)
                    {
                        try
                        {
                            var batch = JsonSerializer.Deserialize<MessageBatch>(batchData.ToString());
                            if (batch != null)
                            {
                                var batchKey = new BatchKey(batch.HubName, batch.MethodName, batch.ConnectionId, batch.GroupName);
                                await SendBatchAsync(batchKey, batch, queuedBatchKey);
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize queued batch data for key {Key}", queuedBatchKey);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message batches");
            }
            finally
            {
                _batchProcessingLock.Release();
            }
        }

        private async Task<MessageBatch> GetOrCreateBatchAsync(string batchKeyString, BatchKey batchKey)
        {
            var batchData = await _redis!.HashGetAsync(_activeBatchesKey, batchKeyString);

            if (batchData.HasValue)
            {
                try
                {
                    var existingBatch = JsonSerializer.Deserialize<MessageBatch>(batchData.ToString());
                    if (existingBatch != null)
                    {
                        return existingBatch;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize existing batch for key {Key}, creating new batch", batchKeyString);
                }
            }

            // Create new batch if not found or deserialization failed
            return CreateNewBatch(batchKey);
        }

        private MessageBatch CreateNewBatch(BatchKey key)
        {
            return new MessageBatch
            {
                HubName = key.HubName,
                MethodName = key.MethodName,
                ConnectionId = key.ConnectionId,
                GroupName = key.GroupName,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
