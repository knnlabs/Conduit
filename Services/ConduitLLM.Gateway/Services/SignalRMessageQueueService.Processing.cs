using ConduitLLM.Gateway.Models;

using StackExchange.Redis;
using System.Text.Json;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageQueueService
    {
        private void ProcessMessages(object? state)
        {
            // Fire-and-forget with proper exception handling - don't use async void
            _ = ProcessMessagesAsync();
        }

        private async Task ProcessMessagesAsync()
        {
            if (_redis == null || _currentCircuitState == Polly.CircuitBreaker.CircuitState.Open)
            {
                if (_currentCircuitState == Polly.CircuitBreaker.CircuitState.Open)
                {
                    _logger.LogDebug("Circuit breaker is open, skipping message processing");
                }
                return;
            }

            try
            {
                // Read pending messages from the consumer group
                var streamEntries = await _redis.StreamReadGroupAsync(
                    _messageStreamKey,
                    _consumerGroup,
                    _consumerName,
                    ">",
                    count: _processingBatchSize);

                // TODO: Add pending message recovery in future iteration
                // For now, focus on basic streaming functionality

                if (streamEntries.Length == 0)
                {
                    return;
                }

                _logger.LogDebug("Processing batch of {Count} messages from Redis stream", streamEntries.Length);

                // Process messages in parallel with limited concurrency
                var tasks = streamEntries.Select(async entry =>
                {
                    await _processingLock.WaitAsync();
                    try
                    {
                        await ProcessStreamEntry(entry);
                    }
                    finally
                    {
                        _processingLock.Release();
                    }
                });

                await Task.WhenAll(tasks);
                _lastProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing messages from Redis stream");
            }
        }

        private async Task ProcessStreamEntry(StreamEntry entry)
        {
            try
            {
                var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
                if (!dataField.Value.HasValue)
                {
                    _logger.LogWarning("Stream entry {EntryId} missing data field", entry.Id);
                    await _redis!.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, entry.Id);
                    return;
                }

                var message = JsonSerializer.Deserialize<QueuedMessage>(dataField.Value!.ToString());
                if (message == null)
                {
                    _logger.LogWarning("Failed to deserialize message from stream entry {EntryId}", entry.Id);
                    await _redis!.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, entry.Id);
                    return;
                }

                // Check if message is ready for delivery
                if (message.NextDeliveryAt > DateTime.UtcNow)
                {
                    _logger.LogDebug("Message {MessageId} not ready for delivery, skipping", message.Message.MessageId);
                    return; // Don't acknowledge yet, will be picked up later
                }

                // Check if message has expired
                if (message.Message.IsExpired)
                {
                    _logger.LogWarning("Message {MessageId} expired, moving to dead letter", message.Message.MessageId);
                    await MoveToDeadLetterAsync(message, "Message expired", entry.Id);
                    return;
                }

                var success = await ProcessSingleMessageAsync(message);
                if (!success && message.DeliveryAttempts >= _maxRetryAttempts)
                {
                    await MoveToDeadLetterAsync(message, $"Failed after {_maxRetryAttempts} attempts", entry.Id);
                }
                else if (!success)
                {
                    // Re-enqueue for retry with delay
                    message.NextDeliveryAt = CalculateNextDeliveryTime(message.DeliveryAttempts);
                    await EnqueueMessageAsync(message);
                    await _redis!.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, entry.Id);
                }
                else
                {
                    // Success - acknowledge the message
                    await _redis!.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, entry.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stream entry {EntryId}", entry.Id);
                // Don't acknowledge on error - message will be retried
            }
        }
    }
}
