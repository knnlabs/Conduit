using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Gateway.Models;

using Microsoft.AspNetCore.SignalR;

using Polly;
using Polly.CircuitBreaker;

using StackExchange.Redis;
using System.Text.Json;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service that manages queued messages for reliable SignalR delivery
    /// </summary>
    public interface ISignalRMessageQueueService
    {
        /// <summary>
        /// Enqueues a message for delivery
        /// </summary>
        Task EnqueueMessageAsync(QueuedMessage message);

        /// <summary>
        /// Gets current queue statistics
        /// </summary>
        QueueStatistics GetStatistics();

        /// <summary>
        /// Gets messages in the dead letter queue
        /// </summary>
        IEnumerable<QueuedMessage> GetDeadLetterMessages();

        /// <summary>
        /// Requeues a dead letter message for retry
        /// </summary>
        Task RequeueDeadLetterAsync(string messageId);
    }

    /// <summary>
    /// Statistics about the message queue
    /// </summary>
    public class QueueStatistics
    {
        public int PendingMessages { get; set; }
        public int DeadLetterMessages { get; set; }
        public int ProcessedMessages { get; set; }
        public int FailedMessages { get; set; }
        public DateTime LastProcessedAt { get; set; }
        public CircuitState CircuitBreakerState { get; set; }
        public int ConsecutiveFailures { get; set; }
    }

    /// <summary>
    /// Implementation of SignalR message queue service using Redis Streams
    /// </summary>
    public class SignalRMessageQueueService : ISignalRMessageQueueService, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRMessageQueueService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISignalRAcknowledgmentService _acknowledgmentService;
        private readonly RedisConnectionFactory _redisConnectionFactory;
        
        private readonly SemaphoreSlim _processingLock;
        private IDatabase? _redis;
        
        private Timer? _processingTimer;
        private readonly IAsyncPolicy<bool> _retryPolicy;
        private readonly IAsyncPolicy<bool> _circuitBreaker;
        private CircuitState _currentCircuitState = CircuitState.Closed;
        
        // Redis keys
        private readonly string _messageStreamKey;
        private readonly string _deadLetterStreamKey;
        private readonly string _consumerGroup;
        private readonly string _consumerName;
        
        // Configuration
        private readonly int _maxRetryAttempts;
        private readonly TimeSpan _initialRetryDelay;
        private readonly TimeSpan _maxRetryDelay;
        private readonly int _processingBatchSize;
        private readonly TimeSpan _processingInterval;
        private readonly int _circuitBreakerFailureThreshold;
        private readonly TimeSpan _circuitBreakerDuration;
        
        // Statistics
        private int _processedMessages;
        private int _failedMessages;
        private DateTime _lastProcessedAt = DateTime.UtcNow;
        private int _consecutiveFailures;

        public SignalRMessageQueueService(
            ILogger<SignalRMessageQueueService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ISignalRAcknowledgmentService acknowledgmentService,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _acknowledgmentService = acknowledgmentService;
            _redisConnectionFactory = redisConnectionFactory;
            
            // Redis keys
            var instanceId = Environment.MachineName;
            _messageStreamKey = RedisKeys.SignalR.MessageStream;
            _deadLetterStreamKey = RedisKeys.SignalR.DeadLetterStream;
            _consumerGroup = "signalr-processors";
            _consumerName = $"processor-{instanceId}-{Environment.ProcessId}";

            // Load configuration
            _maxRetryAttempts = configuration.GetValue<int>("SignalR:MessageQueue:MaxRetryAttempts", 5);
            _initialRetryDelay = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:MessageQueue:InitialRetryDelaySeconds", 2));
            _maxRetryDelay = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:MessageQueue:MaxRetryDelaySeconds", 32));
            _processingBatchSize = configuration.GetValue<int>("SignalR:MessageQueue:ProcessingBatchSize", 100);
            _processingInterval = TimeSpan.FromMilliseconds(configuration.GetValue<int>("SignalR:MessageQueue:ProcessingIntervalMs", 100));
            _circuitBreakerFailureThreshold = configuration.GetValue<int>("SignalR:MessageQueue:CircuitBreakerFailureThreshold", 5);
            _circuitBreakerDuration = TimeSpan.FromSeconds(configuration.GetValue<int>("SignalR:MessageQueue:CircuitBreakerDurationSeconds", 30));

            _processingLock = new SemaphoreSlim(_processingBatchSize);

            // Configure retry policy with exponential backoff
            _retryPolicy = Policy<bool>
                .HandleResult(success => !success)
                .WaitAndRetryAsync(
                    _maxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Min(
                        _initialRetryDelay.TotalSeconds * Math.Pow(2, retryAttempt - 1),
                        _maxRetryDelay.TotalSeconds)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var message = context.TryGetValue("message", out var msg) ? msg as QueuedMessage : null;
                        _logger.LogWarning(
                            "Retrying message {MessageId} delivery, attempt {RetryCount}/{MaxRetries}, delay: {Delay}ms",
                            message?.Message.MessageId, retryCount, _maxRetryAttempts, timespan.TotalMilliseconds);
                    });

            // Configure circuit breaker
            _circuitBreaker = Policy<bool>
                .HandleResult(success => !success)
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: _circuitBreakerFailureThreshold,
                    durationOfBreak: _circuitBreakerDuration,
                    onBreak: (result, duration) =>
                    {
                        _currentCircuitState = CircuitState.Open;
                        _logger.LogError(
                            "Circuit breaker opened due to {Failures} consecutive failures. Duration: {Duration}s",
                            _circuitBreakerFailureThreshold, duration.TotalSeconds);
                    },
                    onReset: () =>
                    {
                        _currentCircuitState = CircuitState.Closed;
                        _logger.LogInformation("Circuit breaker reset, resuming message processing");
                        _consecutiveFailures = 0;
                    },
                    onHalfOpen: () =>
                    {
                        _currentCircuitState = CircuitState.HalfOpen;
                        _logger.LogInformation("Circuit breaker is half-open, testing message delivery");
                    });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Message Queue Service starting");
            
            try
            {
                var connection = await _redisConnectionFactory.GetConnectionAsync();
                _redis = connection.GetDatabase();
                
                // Create consumer group if it doesn't exist
                try
                {
                    await _redis.StreamCreateConsumerGroupAsync(_messageStreamKey, _consumerGroup, "0-0", true);
                }
                catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
                {
                    // Consumer group already exists, continue
                }
                
                // Create dead letter stream consumer group
                try
                {
                    await _redis.StreamCreateConsumerGroupAsync(_deadLetterStreamKey, _consumerGroup, "0-0", true);
                }
                catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
                {
                    // Consumer group already exists, continue
                }
                
                _processingTimer = new Timer(
                    ProcessMessages,
                    null,
                    _processingInterval,
                    _processingInterval);
                    
                _logger.LogInformation("SignalR Message Queue Service started with consumer: {ConsumerName}", _consumerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR Message Queue Service");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Message Queue Service stopping");
            
            _processingTimer?.Change(Timeout.Infinite, 0);

            // Wait for any in-flight processing to complete
            try
            {
                _processingLock?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (ObjectDisposedException)
            {
                // Already disposed, ignore
            }

            return Task.CompletedTask;
        }

        public async Task EnqueueMessageAsync(QueuedMessage message)
        {
            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, message will be lost: {MessageId}", message.Message.MessageId);
                return;
            }
            
            if (message.Message.IsExpired)
            {
                _logger.LogWarning("Attempted to enqueue expired message {MessageId}", message.Message.MessageId);
                return;
            }

            try
            {
                var messageData = JsonSerializer.Serialize(message);
                var streamFields = new NameValueEntry[]
                {
                    new("data", messageData),
                    new("messageId", message.Message.MessageId),
                    new("hubName", message.HubName),
                    new("methodName", message.MethodName),
                    new("priority", message.Message.Priority.ToString()),
                    new("createdAt", message.Message.Timestamp.ToString("O")),
                    new("nextDeliveryAt", message.NextDeliveryAt.ToString("O"))
                };
                
                await _redis.StreamAddAsync(_messageStreamKey, streamFields);
                
                _logger.LogDebug(
                    "Enqueued message {MessageId} for {HubName}.{MethodName} to Redis stream",
                    message.Message.MessageId, message.HubName, message.MethodName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue message {MessageId} to Redis stream", message.Message.MessageId);
                throw;
            }
        }

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

        private void ProcessMessages(object? state)
        {
            // Fire-and-forget with proper exception handling - don't use async void
            _ = ProcessMessagesAsync();
        }

        private async Task ProcessMessagesAsync()
        {
            if (_redis == null || _currentCircuitState == CircuitState.Open)
            {
                if (_currentCircuitState == CircuitState.Open)
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

        private async Task<bool> ProcessSingleMessageAsync(QueuedMessage queuedMessage)
        {
            queuedMessage.DeliveryAttempts++;
            queuedMessage.LastAttemptAt = DateTime.UtcNow;

            var context = new Context();
            context["message"] = queuedMessage;

            try
            {
                var result = await _circuitBreaker.ExecuteAsync(async (ctx) =>
                {
                    return await _retryPolicy.ExecuteAsync(async (retryCtx) =>
                    {
                        return await DeliverMessageAsync(queuedMessage);
                    }, ctx);
                }, context);

                if (result)
                {
                    _processedMessages++;
                    _consecutiveFailures = 0;
                    _logger.LogInformation(
                        "Successfully delivered message {MessageId} after {Attempts} attempts",
                        queuedMessage.Message.MessageId, queuedMessage.DeliveryAttempts);
                }
                else
                {
                    _failedMessages++;
                    _consecutiveFailures++;
                }

                return result;
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning(
                    "Circuit breaker is open, message {MessageId} delivery postponed",
                    queuedMessage.Message.MessageId);
                queuedMessage.LastError = "Circuit breaker open";
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Unexpected error delivering message {MessageId}",
                    queuedMessage.Message.MessageId);
                queuedMessage.LastError = ex.Message;
                _failedMessages++;
                _consecutiveFailures++;
                return false;
            }
        }

        private async Task<bool> DeliverMessageAsync(QueuedMessage queuedMessage)
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = GetHubContext(scope, queuedMessage.HubName);
            
            if (hubContext == null)
            {
                _logger.LogError("Could not find hub context for {HubName}", queuedMessage.HubName);
                queuedMessage.LastError = $"Hub {queuedMessage.HubName} not found";
                return false;
            }

            try
            {
                // Update retry count
                queuedMessage.Message.RetryCount = queuedMessage.DeliveryAttempts - 1;

                // Send the message
                if (!string.IsNullOrEmpty(queuedMessage.ConnectionId))
                {
                    // Direct message to specific connection
                    await hubContext.Clients.Client(queuedMessage.ConnectionId)
                        .SendAsync(queuedMessage.MethodName, queuedMessage.Message);
                }
                else if (!string.IsNullOrEmpty(queuedMessage.GroupName))
                {
                    // Message to group
                    await hubContext.Clients.Group(queuedMessage.GroupName)
                        .SendAsync(queuedMessage.MethodName, queuedMessage.Message);
                }
                else
                {
                    _logger.LogError("Message {MessageId} has no target connection or group", 
                        queuedMessage.Message.MessageId);
                    return false;
                }

                // Register for acknowledgment if it's a critical message
                if (queuedMessage.Message.IsCritical)
                {
                    var pending = await _acknowledgmentService.RegisterMessageAsync(
                        queuedMessage.Message,
                        queuedMessage.ConnectionId ?? "group-message",
                        queuedMessage.HubName,
                        queuedMessage.MethodName,
                        queuedMessage.AcknowledgmentTimeout);

                    // Wait for acknowledgment
                    var acknowledged = await pending.CompletionSource.Task;
                    return acknowledged;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error delivering message {MessageId} to {HubName}.{MethodName}",
                    queuedMessage.Message.MessageId, queuedMessage.HubName, queuedMessage.MethodName);
                queuedMessage.LastError = ex.Message;
                return false;
            }
        }

        private IHubContext<Hub>? GetHubContext(IServiceScope scope, string hubName)
        {
            // This is a simplified version - in production, you'd want a more robust hub resolution mechanism
            var hubType = Type.GetType($"ConduitLLM.Gateway.Hubs.{hubName}, ConduitLLM.Gateway") ??
                          Type.GetType($"ConduitLLM.Gateway.Hubs.{hubName}, ConduitLLM.Gateway");
            
            if (hubType == null)
            {
                return null;
            }

            var contextType = typeof(IHubContext<>).MakeGenericType(hubType);
            return scope.ServiceProvider.GetService(contextType) as IHubContext<Hub>;
        }

        private DateTime CalculateNextDeliveryTime(int attempts)
        {
            var delay = TimeSpan.FromSeconds(Math.Min(
                _initialRetryDelay.TotalSeconds * Math.Pow(2, attempts),
                _maxRetryDelay.TotalSeconds));
            
            return DateTime.UtcNow.Add(delay);
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

        public void Dispose()
        {
            _processingTimer?.Dispose();
            _processingLock?.Dispose();
        }
    }
}