using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Gateway.Models;

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
    public partial class SignalRMessageQueueService : ISignalRMessageQueueService, IHostedService, IDisposable
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

        public void Dispose()
        {
            _processingTimer?.Dispose();
            _processingLock?.Dispose();
        }
    }
}