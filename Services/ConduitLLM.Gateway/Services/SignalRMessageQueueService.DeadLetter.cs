using System.Text.Json;

using ConduitLLM.Gateway.Models;

using Polly.CircuitBreaker;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageQueueService
    {
        private const string MoveToDeadLetterScript = """
            local deadLetterId = redis.call('XADD', KEYS[1], '*',
                'data', ARGV[1],
                'messageId', ARGV[2],
                'hubName', ARGV[3],
                'methodName', ARGV[4],
                'reason', ARGV[5],
                'deadLetteredAt', ARGV[6])
            if ARGV[8] ~= '' then
                redis.call('XACK', KEYS[2], ARGV[7], ARGV[8])
            end
            return deadLetterId
            """;

        public QueueStatistics GetStatistics()
        {
            if (_redis == null)
            {
                return new QueueStatistics
                {
                    ProcessedMessages = _processedMessages,
                    FailedMessages = _failedMessages,
                    ClaimedMessages = _claimedMessages,
                    RetriedMessages = _retriedMessages,
                    LastProcessedAt = _lastProcessedAt,
                    CircuitBreakerState = _currentCircuitState,
                    ConsecutiveFailures = _consecutiveFailures
                };
            }

            try
            {
                var groupInfo = _redis.StreamGroupInfo(_messageStreamKey)
                    .FirstOrDefault(group => group.Name == _consumerGroup);
                var pendingInfo = _redis.StreamPending(_messageStreamKey, _consumerGroup);
                var oldestPending = pendingInfo.PendingMessageCount == 0
                    ? Array.Empty<StreamPendingMessageInfo>()
                    : _redis.StreamPendingMessages(
                        _messageStreamKey,
                        _consumerGroup,
                        count: 1,
                        consumerName: RedisValue.Null);
                var delayedMessages = _redis.SortedSetLength(_delayedMessageKey);
                var deadLetterMessages = _redis.StreamLength(_deadLetterStreamKey);
                var pendingMessages = groupInfo.PendingMessageCount + (groupInfo.Lag ?? 0) + delayedMessages;

                return new QueueStatistics
                {
                    PendingMessages = (int)Math.Min(int.MaxValue, pendingMessages),
                    DelayedMessages = (int)Math.Min(int.MaxValue, delayedMessages),
                    DeadLetterMessages = (int)Math.Min(int.MaxValue, deadLetterMessages),
                    ClaimedMessages = Volatile.Read(ref _claimedMessages),
                    RetriedMessages = Volatile.Read(ref _retriedMessages),
                    OldestPendingAgeSeconds = oldestPending.Length == 0
                        ? 0
                        : oldestPending[0].IdleTimeInMilliseconds / 1000d,
                    ProcessedMessages = Volatile.Read(ref _processedMessages),
                    FailedMessages = Volatile.Read(ref _failedMessages),
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
                    ClaimedMessages = _claimedMessages,
                    RetriedMessages = _retriedMessages,
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
                            var message = JsonSerializer.Deserialize<QueuedMessage>(
                                dataField.Value!.ToString(),
                                QueueSerializerOptions);
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
                        var message = JsonSerializer.Deserialize<QueuedMessage>(
                            dataField.Value!.ToString(),
                            QueueSerializerOptions);
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

                var messageData = JsonSerializer.Serialize(message, QueueSerializerOptions);
                await WriteDeadLetterAsync(
                    messageData,
                    message.Message.MessageId,
                    message.HubName,
                    message.MethodName,
                    reason,
                    originalEntryId);

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

        private async Task MoveRawToDeadLetterAsync(
            string rawData,
            string reason,
            RedisValue originalEntryId)
        {
            await WriteDeadLetterAsync(
                rawData,
                string.Empty,
                string.Empty,
                string.Empty,
                reason,
                originalEntryId);
            Interlocked.Increment(ref _failedMessages);

            _logger.LogWarning(
                "Stream entry {EntryId} moved to the dead letter queue: {Reason}",
                originalEntryId,
                reason);
        }

        private async Task WriteDeadLetterAsync(
            string data,
            string messageId,
            string hubName,
            string methodName,
            string reason,
            RedisValue? originalEntryId)
        {
            await _redis!.ScriptEvaluateAsync(
                MoveToDeadLetterScript,
                new RedisKey[] { _deadLetterStreamKey, _messageStreamKey },
                new RedisValue[]
                {
                    data,
                    messageId,
                    hubName,
                    methodName,
                    reason,
                    DateTime.UtcNow.ToString("O"),
                    _consumerGroup,
                    originalEntryId?.ToString() ?? string.Empty
                });
        }

    }
}
