using System.Text.Json;
using System.Threading.Channels;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Models.SignalR;

using StackExchange.Redis;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service that batches SignalR messages to improve performance
    /// </summary>
    public interface ISignalRMessageBatcher
    {
        /// <summary>
        /// Adds a message to the batch queue
        /// </summary>
        Task AddMessageAsync(string hubName, string methodName, object message, string? connectionId = null, string? groupName = null, int priority = 0);

        /// <summary>
        /// Gets current batching statistics
        /// </summary>
        Task<BatchingStatistics> GetStatisticsAsync();

        /// <summary>
        /// Forces immediate sending of all pending batches
        /// </summary>
        Task FlushAllBatchesAsync();

        /// <summary>
        /// Pauses batching (messages sent immediately)
        /// </summary>
        void PauseBatching();

        /// <summary>
        /// Resumes batching
        /// </summary>
        void ResumeBatching();
    }

    /// <summary>
    /// Statistics about message batching
    /// </summary>
    public class BatchingStatistics
    {
        public long TotalMessagesBatched { get; set; }
        public long TotalBatchesSent { get; set; }
        public double AverageMessagesPerBatch { get; set; }
        public long CurrentPendingMessages { get; set; }
        public DateTime LastBatchSentAt { get; set; }
        public TimeSpan AverageBatchLatency { get; set; }
        public long NetworkCallsSaved { get; set; }
        public bool IsBatchingEnabled { get; set; }
        public Dictionary<string, long> MessagesByMethod { get; set; } = new();
        public double BatchEfficiencyPercentage { get; set; }
    }

    /// <summary>
    /// Implementation of SignalR message batcher.
    /// Uses Channel-based signaling for batch processing to ensure proper error handling
    /// and graceful shutdown instead of fire-and-forget Task.Run patterns.
    /// </summary>
    public partial class SignalRMessageBatcher : ISignalRMessageBatcher, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRMessageBatcher> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly RedisConnectionFactory _redisConnectionFactory;

        // Redis connection
        private IDatabase? _redis;

        // Redis keys
        private readonly string _activeBatchesKey;
        private readonly string _batchQueueKey;
        private readonly string _messagesByMethodKey;
        private readonly string _statisticsKey;

        // Synchronization
        private readonly SemaphoreSlim _batchProcessingLock;

        // Timers
        private Timer? _batchTimer;
        private readonly object _timerLock = new();

        // Channel-based signal processing - replaces fire-and-forget Task.Run
        private enum BatchSignal { ProcessBatches, FlushAll }
        private readonly Channel<BatchSignal> _signalChannel;
        private Task? _signalProcessingTask;
        private CancellationTokenSource? _shutdownCts;

        // Configuration
        private readonly TimeSpan _batchWindow;
        private readonly int _maxBatchSize;
        private readonly long _maxBatchSizeBytes;
        private readonly bool _groupByMethod;

        // State
        private bool _isBatchingEnabled = true;
        private readonly object _stateLock = new();

        public SignalRMessageBatcher(
            ILogger<SignalRMessageBatcher> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            RedisConnectionFactory redisConnectionFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _redisConnectionFactory = redisConnectionFactory;

            // Redis keys
            _activeBatchesKey = RedisKeys.SignalR.ActiveBatches;
            _batchQueueKey = RedisKeys.SignalR.BatchQueue;
            _messagesByMethodKey = RedisKeys.SignalR.BatchStatsMethods;
            _statisticsKey = RedisKeys.SignalR.BatchStatsGlobal;

            // Load configuration
            _batchWindow = TimeSpan.FromMilliseconds(configuration.GetValue<int>("SignalR:Batching:WindowMs", 100));
            _maxBatchSize = configuration.GetValue<int>("SignalR:Batching:MaxBatchSize", 50);
            _maxBatchSizeBytes = configuration.GetValue<long>("SignalR:Batching:MaxBatchSizeBytes", 1024 * 1024); // 1MB default
            _groupByMethod = configuration.GetValue<bool>("SignalR:Batching:GroupByMethod", true);

            _batchProcessingLock = new SemaphoreSlim(1, 1);

            // Bounded channel to prevent unbounded memory growth
            _signalChannel = Channel.CreateBounded<BatchSignal>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "SignalR Message Batcher starting with window: {Window}ms, max size: {MaxSize}",
                _batchWindow.TotalMilliseconds, _maxBatchSize);

            try
            {
                var connection = await _redisConnectionFactory.GetConnectionAsync();
                _redis = connection.GetDatabase();

                // Initialize statistics in Redis if they don't exist
                await InitializeStatisticsAsync();

                // Start signal processing task for handling batch operations
                _shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _signalProcessingTask = ProcessSignalsAsync(_shutdownCts.Token);

                _batchTimer = new Timer(
                    ProcessBatches,
                    null,
                    _batchWindow,
                    _batchWindow);

                _logger.LogInformation("SignalR Message Batcher started with Redis backend");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start SignalR Message Batcher");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Message Batcher stopping");

            lock (_timerLock)
            {
                _batchTimer?.Change(Timeout.Infinite, 0);
            }

            // Complete the signal channel to stop accepting new signals
            _signalChannel.Writer.Complete();

            // Flush remaining batches directly (don't go through channel since it's completed)
            try
            {
                await FlushAllBatchesAsync().WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Batch flush operation timed out during shutdown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing batches during shutdown");
            }

            // Wait for signal processing task to complete
            if (_signalProcessingTask != null)
            {
                try
                {
                    _shutdownCts?.Cancel();
                    await _signalProcessingTask.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Signal processing task did not complete in time during shutdown");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error waiting for signal processing task during shutdown");
                }
            }
        }

        public async Task AddMessageAsync(
            string hubName, 
            string methodName, 
            object message, 
            string? connectionId = null, 
            string? groupName = null,
            int priority = 0)
        {
            if (!_isBatchingEnabled)
            {
                // Send immediately if batching is disabled
                await SendMessageDirectlyAsync(hubName, methodName, message, connectionId, groupName);
                return;
            }

            var batchKey = new BatchKey(hubName, methodName, connectionId, groupName);
            var messageSize = EstimateMessageSize(message);

            if (_redis == null)
            {
                _logger.LogWarning("Redis not available, sending message directly");
                await SendMessageDirectlyAsync(hubName, methodName, message, connectionId, groupName);
                return;
            }

            try
            {
                var batchKeyString = batchKey.ToString();
                var batch = await GetOrCreateBatchAsync(batchKeyString, batchKey);

                // Check if adding this message would exceed limits
                if (batch.Messages.Count >= _maxBatchSize ||
                    batch.TotalSizeBytes + messageSize > _maxBatchSizeBytes)
                {
                    // Queue this batch for immediate sending
                    if (!batch.IsQueued)
                    {
                        batch.IsQueued = true;
                        await _redis.ListRightPushAsync(_batchQueueKey, batchKeyString);

                        // Signal for immediate processing via channel (replaces Task.Run)
                        _signalChannel.Writer.TryWrite(BatchSignal.ProcessBatches);
                    }

                    // Create a new batch for this message
                    batch = CreateNewBatch(batchKey);
                }

                // Add message to batch
                batch.Messages.Add(message);
                batch.TotalSizeBytes += messageSize;
                batch.Priority = Math.Max(batch.Priority, priority);
                
                if (message is SignalRMessage signalRMessage && signalRMessage.IsCritical)
                {
                    batch.ContainsCriticalMessages = true;
                }

                // Save updated batch to Redis
                var batchData = JsonSerializer.Serialize(batch);
                await _redis.HashSetAsync(_activeBatchesKey, batchKeyString, batchData);

                // Update statistics
                await _redis.HashIncrementAsync(_statisticsKey, "totalMessagesBatched");
                await _redis.HashIncrementAsync(_messagesByMethodKey, methodName);

                _logger.LogDebug(
                    "Added message to batch for {HubName}.{MethodName}, batch size: {Size}",
                    hubName, methodName, batch.Messages.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add message to batch, sending directly");
                await SendMessageDirectlyAsync(hubName, methodName, message, connectionId, groupName);
            }
        }

        public void PauseBatching()
        {
            lock (_stateLock)
            {
                _isBatchingEnabled = false;
                _logger.LogInformation("Message batching paused");
            }

            // Signal to flush pending batches via channel (replaces Task.Run)
            _signalChannel.Writer.TryWrite(BatchSignal.FlushAll);
        }

        public void ResumeBatching()
        {
            lock (_stateLock)
            {
                _isBatchingEnabled = true;
                _logger.LogInformation("Message batching resumed");
            }
        }

        public void Dispose()
        {
            _batchTimer?.Dispose();
            _batchProcessingLock?.Dispose();
            _shutdownCts?.Dispose();
        }

        /// <summary>
        /// Key for identifying unique batch targets
        /// </summary>
        private class BatchKey
        {
            public string HubName { get; }
            public string MethodName { get; }
            public string? ConnectionId { get; }
            public string? GroupName { get; }

            public BatchKey(string hubName, string methodName, string? connectionId, string? groupName)
            {
                HubName = hubName;
                MethodName = methodName;
                ConnectionId = connectionId;
                GroupName = groupName;
            }

            public override string ToString()
            {
                return $"{HubName}:{MethodName}:{ConnectionId ?? "all"}:{GroupName ?? "none"}";
            }
        }

        /// <summary>
        /// Container for messages being batched
        /// </summary>
        private class MessageBatch
        {
            public List<object> Messages { get; set; } = new();
            public string HubName { get; set; } = null!;
            public string MethodName { get; set; } = null!;
            public string? ConnectionId { get; set; }
            public string? GroupName { get; set; }
            public DateTime CreatedAt { get; set; }
            public long TotalSizeBytes { get; set; }
            public int Priority { get; set; }
            public bool ContainsCriticalMessages { get; set; }
            public bool IsQueued { get; set; }
        }
    }
}