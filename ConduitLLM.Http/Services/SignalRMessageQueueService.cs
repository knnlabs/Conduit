using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Http.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace ConduitLLM.Http.Services
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
    /// Implementation of SignalR message queue service
    /// </summary>
    public class SignalRMessageQueueService : ISignalRMessageQueueService, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRMessageQueueService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ISignalRAcknowledgmentService _acknowledgmentService;
        
        private readonly ConcurrentQueue<QueuedMessage> _messageQueue = new();
        private readonly ConcurrentBag<QueuedMessage> _deadLetterQueue = new();
        private readonly SemaphoreSlim _processingLock;
        
        private Timer? _processingTimer;
        private readonly IAsyncPolicy<bool> _retryPolicy;
        private readonly IAsyncPolicy<bool> _circuitBreaker;
        private CircuitState _currentCircuitState = CircuitState.Closed;
        
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
            ISignalRAcknowledgmentService acknowledgmentService)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _acknowledgmentService = acknowledgmentService;

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

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Message Queue Service starting");
            
            _processingTimer = new Timer(
                ProcessMessages,
                null,
                _processingInterval,
                _processingInterval);

            return Task.CompletedTask;
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

        public Task EnqueueMessageAsync(QueuedMessage message)
        {
            if (message.Message.IsExpired)
            {
                _logger.LogWarning("Attempted to enqueue expired message {MessageId}", message.Message.MessageId);
                return Task.CompletedTask;
            }

            _messageQueue.Enqueue(message);
            _logger.LogDebug(
                "Enqueued message {MessageId} for {HubName}.{MethodName}",
                message.Message.MessageId, message.HubName, message.MethodName);

            return Task.CompletedTask;
        }

        public QueueStatistics GetStatistics()
        {
            return new QueueStatistics
            {
                PendingMessages = _messageQueue.Count(),
                DeadLetterMessages = _deadLetterQueue.Count(),
                ProcessedMessages = _processedMessages,
                FailedMessages = _failedMessages,
                LastProcessedAt = _lastProcessedAt,
                CircuitBreakerState = _currentCircuitState,
                ConsecutiveFailures = _consecutiveFailures
            };
        }

        public IEnumerable<QueuedMessage> GetDeadLetterMessages()
        {
            return _deadLetterQueue.ToList();
        }

        public Task RequeueDeadLetterAsync(string messageId)
        {
            var message = _deadLetterQueue.FirstOrDefault(m => m.Message.MessageId == messageId);
            if (message != null)
            {
                message.IsDeadLetter = false;
                message.DeadLetterReason = null;
                message.DeliveryAttempts = 0;
                message.LastError = null;
                message.NextDeliveryAt = DateTime.UtcNow;
                
                _messageQueue.Enqueue(message);
                _logger.LogInformation("Requeued dead letter message {MessageId}", messageId);
            }
            
            return Task.CompletedTask;
        }

        private async void ProcessMessages(object? state)
        {
            if (_currentCircuitState == CircuitState.Open)
            {
                _logger.LogDebug("Circuit breaker is open, skipping message processing");
                return;
            }

            var messagesToProcess = new List<QueuedMessage>();
            var now = DateTime.UtcNow;

            // Dequeue messages that are ready for delivery
            while (messagesToProcess.Count() < _processingBatchSize && _messageQueue.TryPeek(out var peekedMessage))
            {
                if (peekedMessage.NextDeliveryAt <= now)
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        if (!message.Message.IsExpired)
                        {
                            messagesToProcess.Add(message);
                        }
                        else
                        {
                            _logger.LogWarning("Message {MessageId} expired, moving to dead letter", message.Message.MessageId);
                            MoveToDeadLetter(message, "Message expired");
                        }
                    }
                }
                else
                {
                    break; // No more messages ready for delivery
                }
            }

            if (messagesToProcess.Count() == 0)
            {
                return;
            }

            _logger.LogDebug("Processing batch of {Count} messages", messagesToProcess.Count());

            // Process messages in parallel with limited concurrency
            var tasks = messagesToProcess.Select(async message =>
            {
                await _processingLock.WaitAsync();
                try
                {
                    var success = await ProcessSingleMessageAsync(message);
                    if (!success && message.DeliveryAttempts >= _maxRetryAttempts)
                    {
                        MoveToDeadLetter(message, $"Failed after {_maxRetryAttempts} attempts");
                    }
                    else if (!success)
                    {
                        // Re-enqueue for retry
                        message.NextDeliveryAt = CalculateNextDeliveryTime(message.DeliveryAttempts);
                        _messageQueue.Enqueue(message);
                    }
                }
                finally
                {
                    _processingLock.Release();
                }
            });

            await Task.WhenAll(tasks);
            _lastProcessedAt = DateTime.UtcNow;
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
                    _logger.LogDebug(
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
            var hubType = Type.GetType($"ConduitLLM.Http.Hubs.{hubName}, ConduitLLM.Http") ??
                          Type.GetType($"ConduitLLM.Http.Hubs.{hubName}, ConduitLLM.Http");
            
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

        private void MoveToDeadLetter(QueuedMessage message, string reason)
        {
            message.IsDeadLetter = true;
            message.DeadLetterReason = reason;
            _deadLetterQueue.Add(message);
            
            _logger.LogWarning(
                "Message {MessageId} moved to dead letter queue: {Reason}",
                message.Message.MessageId, reason);
        }

        public void Dispose()
        {
            _processingTimer?.Dispose();
            _processingLock?.Dispose();
        }
    }
}