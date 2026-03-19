using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

using ConduitLLM.Configuration.Services;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Models.SignalR;
using ConduitLLM.Gateway.Models;

using Microsoft.AspNetCore.SignalR;
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
    public class SignalRMessageBatcher : ISignalRMessageBatcher, IHostedService, IDisposable
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

        private async Task SendBatchAsync(BatchKey batchKey, MessageBatch batch, string batchKeyString)
        {
            if (_redis == null)
            {
                return;
            }

            try
            {
                // Remove batch from active batches
                var removed = await _redis.HashDeleteAsync(_activeBatchesKey, batchKeyString);
                if (!removed)
                {
                    return; // Batch already processed
                }

                var batchLatency = DateTime.UtcNow - batch.CreatedAt;

                if (batch.Messages.Count == 0)
                {
                    return;
                }

                var messagesToSend = new List<object>(batch.Messages);
                var messageCount = messagesToSend.Count;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var hubContext = GetHubContext(scope, batch.HubName);
                    
                    if (hubContext == null)
                    {
                        _logger.LogError("Could not find hub context for {HubName}", batch.HubName);
                        return;
                    }

                    // Create batched message
                    var batchedMessage = new BatchedMessage
                    {
                        Messages = messagesToSend,
                        MethodName = batch.MethodName,
                        HubName = batch.HubName,
                        ConnectionId = batch.ConnectionId,
                        GroupName = batch.GroupName,
                        TotalSizeBytes = batch.TotalSizeBytes,
                        Priority = batch.Priority,
                        ContainsCriticalMessages = batch.ContainsCriticalMessages
                    };

                    // Send based on target
                    if (!string.IsNullOrEmpty(batch.ConnectionId))
                    {
                        await hubContext.Clients.Client(batch.ConnectionId)
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }
                    else if (!string.IsNullOrEmpty(batch.GroupName))
                    {
                        await hubContext.Clients.Group(batch.GroupName)
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }
                    else
                    {
                        await hubContext.Clients.All
                            .SendAsync($"{batch.MethodName}Batch", batchedMessage);
                    }

                    // Update statistics in Redis
                    await Task.WhenAll(
                        _redis.HashIncrementAsync(_statisticsKey, "totalBatchesSent"),
                        _redis.HashIncrementAsync(_statisticsKey, "totalBatchLatency", (long)batchLatency.TotalMilliseconds),
                        _redis.HashSetAsync(_statisticsKey, "lastBatchSentAt", DateTime.UtcNow.ToBinary().ToString())
                    );

                    _logger.LogDebug(
                        "Sent batch of {Count} messages for {HubName}.{MethodName}, latency: {Latency}ms",
                        messageCount, batch.HubName, batch.MethodName, batchLatency.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error sending batch for {HubName}.{MethodName}",
                        batch.HubName, batch.MethodName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SendBatchAsync for key {Key}, hub {HubName}.{MethodName}", batchKeyString, batch.HubName, batch.MethodName);
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

        private async Task SendMessageDirectlyAsync(
            string hubName, 
            string methodName, 
            object message, 
            string? connectionId, 
            string? groupName)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var hubContext = GetHubContext(scope, hubName);
                
                if (hubContext == null)
                {
                    _logger.LogError("Could not find hub context for {HubName}", hubName);
                    return;
                }

                if (!string.IsNullOrEmpty(connectionId))
                {
                    await hubContext.Clients.Client(connectionId).SendAsync(methodName, message);
                }
                else if (!string.IsNullOrEmpty(groupName))
                {
                    await hubContext.Clients.Group(groupName).SendAsync(methodName, message);
                }
                else
                {
                    await hubContext.Clients.All.SendAsync(methodName, message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error sending message directly for {HubName}.{MethodName}",
                    hubName, methodName);
            }
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

        private long EstimateMessageSize(object message)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                return json.Length * sizeof(char);
            }
            catch
            {
                // Fallback estimate
                return 1024;
            }
        }

        private static IHubContext<Hub>? GetHubContext(IServiceScope scope, string hubName)
            => Utilities.SignalRHubContextResolver.Resolve(scope.ServiceProvider, hubName);

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