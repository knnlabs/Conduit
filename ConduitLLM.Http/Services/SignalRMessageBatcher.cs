using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Constants;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Batches multiple SignalR messages for efficient delivery
    /// </summary>
    public class SignalRMessageBatcher : BackgroundService
    {
        private readonly ILogger<SignalRMessageBatcher> _logger;
        private readonly Dictionary<string, BatchContext> _batches;
        private readonly SemaphoreSlim _batchLock;
        private readonly TimeSpan _batchWindow = TimeSpan.FromMilliseconds(100);
        private readonly int _maxBatchSize = 50;

        private class BatchContext
        {
            public List<BatchedMessage> Messages { get; } = new();
            public DateTime BatchStartTime { get; set; } = DateTime.UtcNow;
            public IClientProxy? ClientProxy { get; set; }
        }

        private record BatchedMessage(
            string MethodName,
            object[] Arguments,
            DateTime QueuedAt);

        public SignalRMessageBatcher(ILogger<SignalRMessageBatcher> logger)
        {
            _logger = logger;
            _batches = new Dictionary<string, BatchContext>();
            _batchLock = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Add a message to the batch queue
        /// </summary>
        public async Task QueueBatchedMessage(
            string groupKey,
            IClientProxy clientProxy,
            string methodName,
            params object[] args)
        {
            await _batchLock.WaitAsync();
            try
            {
                if (!_batches.TryGetValue(groupKey, out var context))
                {
                    context = new BatchContext { ClientProxy = clientProxy };
                    _batches[groupKey] = context;
                }

                context.Messages.Add(new BatchedMessage(methodName, args, DateTime.UtcNow));

                // Send immediately if batch is full
                if (context.Messages.Count >= _maxBatchSize)
                {
                    await SendBatch(groupKey, context);
                    _batches.Remove(groupKey);
                }
            }
            finally
            {
                _batchLock.Release();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR message batcher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(50, stoppingToken); // Check batches every 50ms
                    await ProcessPendingBatches(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SignalR message batcher");
                }
            }

            // Send any remaining batches
            await FlushAllBatches();
            _logger.LogInformation("SignalR message batcher stopped");
        }

        private async Task ProcessPendingBatches(CancellationToken cancellationToken)
        {
            await _batchLock.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;
                var keysToProcess = new List<string>();

                foreach (var kvp in _batches)
                {
                    if (now - kvp.Value.BatchStartTime >= _batchWindow)
                    {
                        keysToProcess.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToProcess)
                {
                    if (_batches.TryGetValue(key, out var context))
                    {
                        await SendBatch(key, context);
                        _batches.Remove(key);
                    }
                }
            }
            finally
            {
                _batchLock.Release();
            }
        }

        private async Task SendBatch(string groupKey, BatchContext context)
        {
            if (context.Messages.Count == 0 || context.ClientProxy == null)
                return;

            try
            {
                // Group messages by method name for more efficient delivery
                var messageGroups = context.Messages
                    .GroupBy(m => m.MethodName)
                    .ToList();

                foreach (var group in messageGroups)
                {
                    var messages = group.Select(m => m.Arguments[0]).ToArray();
                    
                    // Send as a batch
                    await context.ClientProxy.SendAsync(
                        $"{group.Key}Batch",
                        new
                        {
                            messages,
                            count = messages.Length,
                            batchId = Guid.NewGuid().ToString(),
                            timestamp = DateTime.UtcNow
                        });

                    _logger.LogDebug(
                        "Sent batch of {Count} {MethodName} messages to {GroupKey}",
                        messages.Length, group.Key, groupKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send batch of {Count} messages to {GroupKey}",
                    context.Messages.Count, groupKey);
            }
        }

        private async Task FlushAllBatches()
        {
            await _batchLock.WaitAsync();
            try
            {
                foreach (var kvp in _batches)
                {
                    await SendBatch(kvp.Key, kvp.Value);
                }
                _batches.Clear();
            }
            finally
            {
                _batchLock.Release();
            }
        }

        public override void Dispose()
        {
            _batchLock?.Dispose();
            base.Dispose();
        }
    }
}