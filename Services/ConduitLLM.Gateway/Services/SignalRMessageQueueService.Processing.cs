using System.Text.Json;

using ConduitLLM.Gateway.Models;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    public partial class SignalRMessageQueueService
    {
        private const string ScheduleDelayedMessageScript = """
            redis.call('ZADD', KEYS[1], ARGV[1], ARGV[2])
            if ARGV[4] ~= '' then
                redis.call('XACK', KEYS[2], ARGV[3], ARGV[4])
            end
            return 1
            """;

        private const string PromoteDelayedMessagesScript = """
            local values = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', ARGV[1], 'LIMIT', 0, ARGV[2])
            local promoted = 0
            local poisoned = 0

            for _, value in ipairs(values) do
                local ok, envelope = pcall(cjson.decode, value)
                if ok and envelope.Data then
                    redis.call('XADD', KEYS[2], '*',
                        'data', envelope.Data,
                        'messageId', envelope.MessageId or '',
                        'hubName', envelope.HubName or '',
                        'methodName', envelope.MethodName or '',
                        'priority', envelope.Priority or '0',
                        'createdAt', envelope.CreatedAt or '',
                        'nextDeliveryAt', envelope.NextDeliveryAt or '')
                    promoted = promoted + 1
                else
                    redis.call('XADD', KEYS[3], '*',
                        'data', value,
                        'reason', 'Malformed delayed message envelope',
                        'deadLetteredAt', ARGV[3])
                    poisoned = poisoned + 1
                end
                redis.call('ZREM', KEYS[1], value)
            end

            return { promoted, poisoned }
            """;

        private sealed class DelayedMessageEnvelope
        {
            public required string ScheduleId { get; init; }
            public required string Data { get; init; }
            public required string MessageId { get; init; }
            public required string HubName { get; init; }
            public required string MethodName { get; init; }
            public required string Priority { get; init; }
            public required string CreatedAt { get; init; }
            public required string NextDeliveryAt { get; init; }
        }

        private void ProcessMessages(object? state)
        {
            _ = ProcessMessagesAsync();
        }

        internal async Task ProcessMessagesAsync()
        {
            if (_redis == null)
            {
                return;
            }

            if (!await _processingCycleLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                await PromoteDueMessagesAsync();

                var claimedEntries = await ClaimAbandonedMessagesAsync();
                var remainingCapacity = Math.Max(0, _processingBatchSize - claimedEntries.Length);
                var newEntries = remainingCapacity == 0
                    ? Array.Empty<StreamEntry>()
                    : await _redis.StreamReadGroupAsync(
                        _messageStreamKey,
                        _consumerGroup,
                        _consumerName,
                        ">",
                        count: remainingCapacity);

                var streamEntries = claimedEntries.Concat(newEntries).ToArray();
                if (streamEntries.Length == 0)
                {
                    return;
                }

                _logger.LogDebug(
                    "Processing {Count} Redis stream messages ({ClaimedCount} reclaimed)",
                    streamEntries.Length,
                    claimedEntries.Length);

                var tasks = streamEntries.Select(async entry =>
                {
                    await _processingLock.WaitAsync();
                    try
                    {
                        await ProcessStreamEntryAsync(entry);
                    }
                    finally
                    {
                        _processingLock.Release();
                    }
                }).ToArray();

                await Task.WhenAll(tasks);
                _lastProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing messages from Redis stream");
            }
            finally
            {
                _processingCycleLock.Release();
            }
        }

        private async Task<StreamEntry[]> ClaimAbandonedMessagesAsync()
        {
            StreamEntry[] claimedEntries;

            if (_autoClaimSupported != false)
            {
                try
                {
                    var result = await _redis!.StreamAutoClaimAsync(
                        _messageStreamKey,
                        _consumerGroup,
                        _consumerName,
                        (long)_pendingMessageIdleTimeout.TotalMilliseconds,
                        _claimCursor,
                        _processingBatchSize);

                    _autoClaimSupported = true;
                    _claimCursor = result.NextStartId.HasValue ? result.NextStartId : "0-0";
                    claimedEntries = result.ClaimedEntries;

                    if (result.DeletedIds.Length > 0)
                    {
                        _logger.LogDebug(
                            "Redis removed {Count} deleted stream entries from the pending list during recovery",
                            result.DeletedIds.Length);
                    }

                    RecordClaimedMessages(claimedEntries.Length);
                    return claimedEntries;
                }
                catch (RedisServerException ex) when (
                    ex.Message.Contains("unknown command", StringComparison.OrdinalIgnoreCase) &&
                    ex.Message.Contains("XAUTOCLAIM", StringComparison.OrdinalIgnoreCase))
                {
                    _autoClaimSupported = false;
                    _logger.LogWarning("Redis does not support XAUTOCLAIM; falling back to XPENDING and XCLAIM");
                }
            }

            var pending = await _redis!.StreamPendingMessagesAsync(
                _messageStreamKey,
                _consumerGroup,
                _processingBatchSize,
                RedisValue.Null,
                minId: null,
                maxId: null,
                minIdleTimeInMs: (long)_pendingMessageIdleTimeout.TotalMilliseconds);

            if (pending.Length == 0)
            {
                return Array.Empty<StreamEntry>();
            }

            claimedEntries = await _redis.StreamClaimAsync(
                _messageStreamKey,
                _consumerGroup,
                _consumerName,
                (long)_pendingMessageIdleTimeout.TotalMilliseconds,
                pending.Select(message => message.MessageId).ToArray());

            RecordClaimedMessages(claimedEntries.Length);
            return claimedEntries;
        }

        private void RecordClaimedMessages(int count)
        {
            if (count == 0)
            {
                return;
            }

            Interlocked.Add(ref _claimedMessages, count);
            _metrics?.RecordQueueMessagesClaimed(count);
            _logger.LogInformation(
                "Reclaimed {Count} abandoned SignalR stream messages for consumer {ConsumerName}",
                count,
                _consumerName);
        }

        private async Task PromoteDueMessagesAsync()
        {
            var result = await _redis!.ScriptEvaluateAsync(
                PromoteDelayedMessagesScript,
                new RedisKey[] { _delayedMessageKey, _messageStreamKey, _deadLetterStreamKey },
                new RedisValue[]
                {
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    _processingBatchSize,
                    DateTime.UtcNow.ToString("O")
                });

            var counts = (RedisResult[])result!;
            var promoted = (long)counts[0];
            var poisoned = (long)counts[1];

            if (promoted > 0)
            {
                _logger.LogDebug("Promoted {Count} due SignalR messages to the delivery stream", promoted);
            }

            if (poisoned > 0)
            {
                Interlocked.Add(ref _failedMessages, checked((int)poisoned));
                _logger.LogError("Moved {Count} malformed delayed SignalR messages to the dead letter stream", poisoned);
            }
        }

        private async Task ScheduleDelayedMessageAsync(
            QueuedMessage message,
            string? serializedMessage = null,
            RedisValue? originalEntryId = null)
        {
            serializedMessage ??= JsonSerializer.Serialize(message, QueueSerializerOptions);

            var envelope = new DelayedMessageEnvelope
            {
                ScheduleId = Guid.NewGuid().ToString("N"),
                Data = serializedMessage,
                MessageId = message.Message.MessageId,
                HubName = message.HubName,
                MethodName = message.MethodName,
                Priority = message.Message.Priority.ToString(),
                CreatedAt = message.Message.Timestamp.ToString("O"),
                NextDeliveryAt = message.NextDeliveryAt.ToString("O")
            };

            await _redis!.ScriptEvaluateAsync(
                ScheduleDelayedMessageScript,
                new RedisKey[] { _delayedMessageKey, _messageStreamKey },
                new RedisValue[]
                {
                    new DateTimeOffset(message.NextDeliveryAt).ToUnixTimeMilliseconds(),
                    JsonSerializer.Serialize(envelope),
                    _consumerGroup,
                    originalEntryId?.ToString() ?? string.Empty
                });

            _logger.LogDebug(
                "Scheduled message {MessageId} for delivery at {NextDeliveryAt}",
                message.Message.MessageId,
                message.NextDeliveryAt);
        }

        private async Task ProcessStreamEntryAsync(StreamEntry entry)
        {
            string? serializedMessage = null;

            try
            {
                var dataField = entry.Values.FirstOrDefault(value => value.Name == "data");
                if (!dataField.Value.HasValue)
                {
                    await MoveRawToDeadLetterAsync(string.Empty, "Stream entry missing data field", entry.Id);
                    return;
                }

                serializedMessage = dataField.Value.ToString();
                QueuedMessage? message;

                try
                {
                    message = JsonSerializer.Deserialize<QueuedMessage>(serializedMessage, QueueSerializerOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize SignalR stream entry {EntryId}", entry.Id);
                    await MoveRawToDeadLetterAsync(serializedMessage, "Invalid queued message payload", entry.Id);
                    return;
                }

                if (message?.Message == null)
                {
                    await MoveRawToDeadLetterAsync(serializedMessage, "Queued message payload is incomplete", entry.Id);
                    return;
                }

                if (message.NextDeliveryAt > DateTime.UtcNow)
                {
                    // This also migrates future-dated entries left pending by older versions.
                    await ScheduleDelayedMessageAsync(message, serializedMessage, entry.Id);
                    return;
                }

                if (message.Message.IsExpired)
                {
                    await MoveToDeadLetterAsync(message, "Message expired", entry.Id);
                    return;
                }

                var success = await ProcessSingleMessageAsync(message);
                if (success)
                {
                    await _redis!.StreamAcknowledgeAsync(_messageStreamKey, _consumerGroup, entry.Id);
                    return;
                }

                if (message.DeliveryAttempts >= _maxRetryAttempts)
                {
                    await MoveToDeadLetterAsync(
                        message,
                        $"Failed after {_maxRetryAttempts} delivery attempts",
                        entry.Id);
                    return;
                }

                message.NextDeliveryAt = CalculateNextDeliveryTime(message.DeliveryAttempts);
                await ScheduleDelayedMessageAsync(message, originalEntryId: entry.Id);
                Interlocked.Increment(ref _retriedMessages);
                _metrics?.RecordQueueMessageRetry();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SignalR stream entry {EntryId}", entry.Id);
                await DeadLetterAfterRepeatedProcessingFailureAsync(entry, serializedMessage, ex);
            }
        }

        private async Task DeadLetterAfterRepeatedProcessingFailureAsync(
            StreamEntry entry,
            string? serializedMessage,
            Exception exception)
        {
            try
            {
                var pending = await _redis!.StreamPendingMessagesAsync(
                    _messageStreamKey,
                    _consumerGroup,
                    count: 1,
                    consumerName: RedisValue.Null,
                    minId: entry.Id,
                    maxId: entry.Id);

                if (pending.Length > 0 && pending[0].DeliveryCount >= _maxRetryAttempts)
                {
                    await MoveRawToDeadLetterAsync(
                        serializedMessage ?? string.Empty,
                        $"Processing failed {pending[0].DeliveryCount} times: {exception.GetType().Name}",
                        entry.Id);
                }
            }
            catch (Exception dispositionException)
            {
                // Redis may be the source of the original exception. Leaving the entry pending
                // preserves it for recovery once Redis is healthy again.
                _logger.LogError(
                    dispositionException,
                    "Could not inspect or dead-letter failed stream entry {EntryId}; it remains pending",
                    entry.Id);
            }
        }
    }
}
