using ConduitLLM.Gateway.Models;

using Polly.CircuitBreaker;

using StackExchange.Redis;
using System.Text.Json;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageQueueService
    {
        public QueueStatistics GetStatistics()
        {
            if (_redis == null)
            {
                return new QueueStatistics
                {
                    ProcessedMessages = _processedMessages,
                    FailedMessages = _failedMessages,
                    LastProcessedAt = _lastProcessedAt,
                    CircuitBreakerState = _currentCircuitState,
                    ConsecutiveFailures = _consecutiveFailures
                };
            }

            try
            {
                var pendingMessages = _redis.StreamLength(_messageStreamKey);
                var deadLetterMessages = _redis.StreamLength(_deadLetterStreamKey);

                return new QueueStatistics
                {
                    PendingMessages = (int)pendingMessages,
                    DeadLetterMessages = (int)deadLetterMessages,
                    ProcessedMessages = _processedMessages,
                    FailedMessages = _failedMessages,
                    LastProcessedAt = _lastProcessedAt,
                    CircuitBreakerState = _currentCircuitState,
                    ConsecutiveFailures = _consecutiveFailures
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get queue statistics from Redis");
                return new QueueStatistics
                {
                    ProcessedMessages = _processedMessages,
                    FailedMessages = _failedMessages,
                    LastProcessedAt = _lastProcessedAt,
                    CircuitBreakerState = _currentCircuitState,
                    ConsecutiveFailures = _consecutiveFailures
                };
            }
        }

        public IEnumerable<QueuedMessage> GetDeadLetterMessages()
        {
            if (_redis == null)
            {
                return Enumerable.Empty<QueuedMessage>();
            }

            try
            {
                var streamEntries = _redis.StreamRange(_deadLetterStreamKey, "-", "+", count: 100);
                var messages = new List<QueuedMessage>();

                foreach (var entry in streamEntries)
                {
                    try
                    {
                        var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
                        if (dataField.Value.HasValue)
                        {
                            var message = JsonSerializer.Deserialize<QueuedMessage>(dataField.Value!.ToString());
                            if (message != null)
                            {
                                messages.Add(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize dead letter message from Redis stream entry {EntryId}", entry.Id);
                    }
                }

                return messages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get dead letter messages from Redis");
                return Enumerable.Empty<QueuedMessage>();
            }
        }

        public async Task RequeueDeadLetterAsync(string messageId)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot requeue dead letter message: {MessageId}", messageId);
                return;
            }

            try
            {
                // Find the message in dead letter stream
                var streamEntries = _redis.StreamRange(_deadLetterStreamKey, "-", "+");
                StreamEntry? targetEntry = null;

                foreach (var entry in streamEntries)
                {
                    var messageIdField = entry.Values.FirstOrDefault(v => v.Name == "messageId");
                    if (messageIdField.Value == messageId)
                    {
                        targetEntry = entry;
                        break;
                    }
                }

                if (targetEntry.HasValue)
                {
                    // Deserialize and modify the message
                    var dataField = targetEntry.Value.Values.FirstOrDefault(v => v.Name == "data");
                    if (dataField.Value.HasValue)
                    {
                        var message = JsonSerializer.Deserialize<QueuedMessage>(dataField.Value!.ToString());
                        if (message != null)
                        {
                            message.IsDeadLetter = false;
                            message.DeadLetterReason = null;
                            message.DeliveryAttempts = 0;
                            message.LastError = null;
                            message.NextDeliveryAt = DateTime.UtcNow;

                            // Re-enqueue to main stream
                            await EnqueueMessageAsync(message);

                            // Remove from dead letter stream
                            await _redis.StreamDeleteAsync(_deadLetterStreamKey, new RedisValue[] { targetEntry.Value.Id });

                            _logger.LogInformation("Requeued dead letter message {MessageId}", messageId);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Dead letter message {MessageId} not found for requeue", messageId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to requeue dead letter message {MessageId}", messageId);
                throw;
            }
        }

        private async Task MoveToDeadLetterAsync(QueuedMessage message, string reason, RedisValue? originalEntryId = null)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, cannot move message {MessageId} to dead letter", message.Message.MessageId);
                return;
            }

            try
            {
                message.IsDeadLetter = true;
                message.DeadLetterReason = reason;

                var messageData = JsonSerializer.Serialize(message);
                var streamFields = new NameValueEntry[]
                {
                    new("data", messageData),
                    new("messageId", message.Message.MessageId),
                    new("hubName", message.HubName),
                    new("methodName", message.MethodName),
                    new("reason", reason),
                    new("deadLetteredAt", DateTime.UtcNow.ToString("O"))
                };

                await _redis.StreamAddAsync(_deadLetterStreamKey, streamFields);

                // If we have the original entry ID, acknowledge it from the main stream
                if (originalEntryId.HasValue)
                {
                    await _redis.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, originalEntryId.Value);
                }

                _logger.LogWarning(
                    "Message {MessageId} moved to dead letter queue: {Reason}",
                    message.Message.MessageId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move message {MessageId} to dead letter queue", message.Message.MessageId);
                throw;
            }
        }
    }
}
