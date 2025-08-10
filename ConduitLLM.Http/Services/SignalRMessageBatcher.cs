using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Http.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Services
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
        BatchingStatistics GetStatistics();

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
    /// Implementation of SignalR message batcher
    /// </summary>
    public class SignalRMessageBatcher : ISignalRMessageBatcher, IHostedService, IDisposable
    {
        private readonly ILogger<SignalRMessageBatcher> _logger;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        
        // Batching data structures
        private readonly ConcurrentDictionary<string, MessageBatch> _activeBatches = new();
        private readonly ConcurrentQueue<BatchKey> _batchQueue = new();
        private readonly SemaphoreSlim _batchProcessingLock;
        
        // Timers
        private Timer? _batchTimer;
        private readonly object _timerLock = new();
        
        // Configuration
        private readonly TimeSpan _batchWindow;
        private readonly int _maxBatchSize;
        private readonly long _maxBatchSizeBytes;
        private readonly bool _groupByMethod;
        
        // Statistics
        private long _totalMessagesBatched;
        private long _totalBatchesSent;
        private long _totalBatchLatency;
        private DateTime _lastBatchSentAt = DateTime.UtcNow;
        private readonly ConcurrentDictionary<string, long> _messagesByMethod = new();
        
        // State
        private bool _isBatchingEnabled = true;
        private readonly object _stateLock = new();

        public SignalRMessageBatcher(
            ILogger<SignalRMessageBatcher> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _serviceProvider = serviceProvider;

            // Load configuration
            _batchWindow = TimeSpan.FromMilliseconds(configuration.GetValue<int>("SignalR:Batching:WindowMs", 100));
            _maxBatchSize = configuration.GetValue<int>("SignalR:Batching:MaxBatchSize", 50);
            _maxBatchSizeBytes = configuration.GetValue<long>("SignalR:Batching:MaxBatchSizeBytes", 1024 * 1024); // 1MB default
            _groupByMethod = configuration.GetValue<bool>("SignalR:Batching:GroupByMethod", true);

            _batchProcessingLock = new SemaphoreSlim(1, 1);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "SignalR Message Batcher starting with window: {Window}ms, max size: {MaxSize}",
                _batchWindow.TotalMilliseconds, _maxBatchSize);

            _batchTimer = new Timer(
                ProcessBatches,
                null,
                _batchWindow,
                _batchWindow);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalR Message Batcher stopping");

            lock (_timerLock)
            {
                _batchTimer?.Change(Timeout.Infinite, 0);
            }

            // Flush remaining batches
            FlushAllBatchesAsync().Wait(TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
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

            var batch = _activeBatches.AddOrUpdate(
                batchKey.ToString(),
                key => CreateNewBatch(batchKey),
                (key, existingBatch) => existingBatch);

            lock (batch.SyncRoot)
            {
                // Check if adding this message would exceed limits
                if (batch.Messages.Count >= _maxBatchSize || 
                    batch.TotalSizeBytes + messageSize > _maxBatchSizeBytes)
                {
                    // Queue this batch for immediate sending
                    if (!batch.IsQueued)
                    {
                        batch.IsQueued = true;
                        _batchQueue.Enqueue(batchKey);
                        
                        // Trigger immediate processing
                        _ = Task.Run(async () => await ProcessBatchesAsync());
                    }

                    // Create a new batch for this message
                    batch = _activeBatches.AddOrUpdate(
                        batchKey.ToString(),
                        key => CreateNewBatch(batchKey),
                        (key, _) => CreateNewBatch(batchKey));
                }

                batch.Messages.Add(message);
                batch.TotalSizeBytes += messageSize;
                batch.Priority = Math.Max(batch.Priority, priority);
                
                if (message is SignalRMessage signalRMessage && signalRMessage.IsCritical)
                {
                    batch.ContainsCriticalMessages = true;
                }

                Interlocked.Increment(ref _totalMessagesBatched);
                _messagesByMethod.AddOrUpdate(methodName, 1, (_, count) => count + 1);
            }

            _logger.LogTrace(
                "Added message to batch for {HubName}.{MethodName}, batch size: {Size}",
                hubName, methodName, batch.Messages.Count);
        }

        public BatchingStatistics GetStatistics()
        {
            var stats = new BatchingStatistics
            {
                TotalMessagesBatched = _totalMessagesBatched,
                TotalBatchesSent = _totalBatchesSent,
                CurrentPendingMessages = _activeBatches.Sum(b => b.Value.Messages.Count),
                LastBatchSentAt = _lastBatchSentAt,
                IsBatchingEnabled = _isBatchingEnabled,
                MessagesByMethod = _messagesByMethod.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };

            if (_totalBatchesSent > 0)
            {
                stats.AverageMessagesPerBatch = (double)_totalMessagesBatched / _totalBatchesSent;
                stats.AverageBatchLatency = TimeSpan.FromMilliseconds(_totalBatchLatency / _totalBatchesSent);
                stats.NetworkCallsSaved = _totalMessagesBatched - _totalBatchesSent;
                stats.BatchEfficiencyPercentage = (1.0 - ((double)_totalBatchesSent / _totalMessagesBatched)) * 100;
            }

            return stats;
        }

        public async Task FlushAllBatchesAsync()
        {
            _logger.LogInformation("Flushing all pending batches");

            var allBatchKeys = _activeBatches.Keys.ToList();
            foreach (var key in allBatchKeys)
            {
                if (_activeBatches.TryGetValue(key, out var batch))
                {
                    var batchKey = new BatchKey(batch.HubName, batch.MethodName, batch.ConnectionId, batch.GroupName);
                    await SendBatchAsync(batchKey, batch);
                }
            }
        }

        public void PauseBatching()
        {
            lock (_stateLock)
            {
                _isBatchingEnabled = false;
                _logger.LogInformation("Message batching paused");
            }

            // Flush pending batches
            _ = Task.Run(async () => await FlushAllBatchesAsync());
        }

        public void ResumeBatching()
        {
            lock (_stateLock)
            {
                _isBatchingEnabled = true;
                _logger.LogInformation("Message batching resumed");
            }
        }

        private async void ProcessBatches(object? state)
        {
            await ProcessBatchesAsync();
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
                var batchesToSend = new List<(BatchKey Key, MessageBatch Batch)>();

                // Check all active batches
                foreach (var kvp in _activeBatches)
                {
                    var batch = kvp.Value;
                    
                    lock (batch.SyncRoot)
                    {
                        if (batch.Messages.Count > 0 && 
                            (now - batch.CreatedAt >= _batchWindow || batch.IsQueued))
                        {
                            var batchKey = new BatchKey(batch.HubName, batch.MethodName, batch.ConnectionId, batch.GroupName);
                            batchesToSend.Add((batchKey, batch));
                        }
                    }
                }

                // Send all ready batches
                foreach (var (key, batch) in batchesToSend)
                {
                    await SendBatchAsync(key, batch);
                }

                // Process explicitly queued batches
                while (_batchQueue.TryDequeue(out var batchKey))
                {
                    if (_activeBatches.TryGetValue(batchKey.ToString(), out var batch))
                    {
                        await SendBatchAsync(batchKey, batch);
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

        private async Task SendBatchAsync(BatchKey batchKey, MessageBatch batch)
        {
            if (_activeBatches.TryRemove(batchKey.ToString(), out _))
            {
                List<object> messagesToSend;
                int messageCount;
                var batchLatency = DateTime.UtcNow - batch.CreatedAt;

                lock (batch.SyncRoot)
                {
                    if (batch.Messages.Count == 0)
                    {
                        return;
                    }

                    messagesToSend = new List<object>(batch.Messages);
                    messageCount = messagesToSend.Count;
                    batch.Messages.Clear();
                }

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

                    Interlocked.Increment(ref _totalBatchesSent);
                    Interlocked.Add(ref _totalBatchLatency, (long)batchLatency.TotalMilliseconds);
                    _lastBatchSentAt = DateTime.UtcNow;

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

        private IHubContext<Hub>? GetHubContext(IServiceScope scope, string hubName)
        {
            var hubType = Type.GetType($"ConduitLLM.Http.Hubs.{hubName}, ConduitLLM.Http") ??
                          Type.GetType($"ConduitLLM.Http.Hubs.{hubName}, ConduitLLM.Http");
            
            if (hubType == null)
            {
                return null;
            }

            var contextType = typeof(IHubContext<>).MakeGenericType(hubType);
            return scope.ServiceProvider.GetService(contextType) as IHubContext<Hub>;
        }

        public void Dispose()
        {
            _batchTimer?.Dispose();
            _batchProcessingLock?.Dispose();
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
            public List<object> Messages { get; } = new();
            public string HubName { get; set; } = null!;
            public string MethodName { get; set; } = null!;
            public string? ConnectionId { get; set; }
            public string? GroupName { get; set; }
            public DateTime CreatedAt { get; set; }
            public long TotalSizeBytes { get; set; }
            public int Priority { get; set; }
            public bool ContainsCriticalMessages { get; set; }
            public bool IsQueued { get; set; }
            public object SyncRoot { get; } = new();
        }
    }
}