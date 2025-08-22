using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Service for managing and tracking batch operations with real-time progress updates.
    /// Supports cancellation, resumption, and detailed progress tracking for bulk operations.
    /// </summary>
    public class BatchOperationService : IBatchOperationService
    {
        private readonly ILogger<BatchOperationService> _logger;
        private readonly ITaskHub _taskHub;
        private readonly IBatchOperationNotificationService? _notificationService;
        private readonly IBatchOperationHistoryService? _historyService;
        private readonly ConcurrentDictionary<string, BatchOperationContext> _activeOperations;

        public BatchOperationService(
            ILogger<BatchOperationService> logger,
            ITaskHub taskHub,
            IBatchOperationNotificationService? notificationService = null,
            IBatchOperationHistoryService? historyService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _taskHub = taskHub ?? throw new ArgumentNullException(nameof(taskHub));
            _notificationService = notificationService;
            _historyService = historyService;
            _activeOperations = new ConcurrentDictionary<string, BatchOperationContext>();
        }

        /// <summary>
        /// Starts a new batch operation with progress tracking
        /// </summary>
        public async Task<BatchOperationResult> StartBatchOperationAsync<T>(
            string operationType,
            IEnumerable<T> items,
            Func<T, CancellationToken, Task<BatchItemResult>> processItemFunc,
            BatchOperationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new BatchOperationOptions();
            var itemsList = items.ToList();
            var operationId = Guid.NewGuid().ToString();
            var virtualKeyId = options.VirtualKeyId;

            var context = new BatchOperationContext
            {
                OperationId = operationId,
                OperationType = operationType,
                TotalItems = itemsList.Count,
                StartTime = DateTime.UtcNow,
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
                Options = options
            };

            if (!_activeOperations.TryAdd(operationId, context))
            {
                throw new InvalidOperationException($"Failed to register batch operation {operationId}");
            }

            try
            {
                // Notify task started
                var metadata = new Dictionary<string, object>
                {
                    ["operationType"] = operationType,
                    ["totalItems"] = itemsList.Count,
                    ["virtualKeyId"] = virtualKeyId,
                    ["parallelism"] = options.MaxDegreeOfParallelism,
                    ["supportsCancellation"] = true,
                    ["supportsResume"] = options.EnableCheckpointing
                };

                await _taskHub.TaskStarted(operationId, $"batch_{operationType}", metadata);

                // Send batch operation started notification if service is available
                if (_notificationService != null)
                {
                    await _notificationService.NotifyBatchOperationStartedAsync(
                        operationId,
                        operationType,
                        itemsList.Count,
                        virtualKeyId,
                        options);
                }

                // Record operation start in history
                if (_historyService != null)
                {
                    await _historyService.RecordOperationStartAsync(
                        operationId,
                        operationType,
                        virtualKeyId,
                        itemsList.Count,
                        options);
                }

                // Process items
                var result = await ProcessBatchAsync(
                    context,
                    itemsList,
                    processItemFunc,
                    options);

                // Notify completion
                if (result.Status == BatchOperationStatusEnum.Completed)
                {
                    await _taskHub.TaskCompleted(operationId, result);
                    
                    if (_notificationService != null)
                    {
                        await _notificationService.NotifyBatchOperationCompletedAsync(
                            operationId,
                            operationType,
                            result.Status,
                            result.TotalItems,
                            result.SuccessCount,
                            result.FailedCount,
                            result.Duration,
                            result.ItemsPerSecond,
                            result);
                    }
                }
                else if (result.Status == BatchOperationStatusEnum.Cancelled)
                {
                    await _taskHub.TaskCancelled(operationId, "Operation cancelled by user");
                    
                    if (_notificationService != null)
                    {
                        var remainingCount = result.TotalItems - context.ProcessedCount;
                        await _notificationService.NotifyBatchOperationCancelledAsync(
                            operationId,
                            operationType,
                            "User requested cancellation",
                            context.ProcessedCount,
                            remainingCount,
                            options.EnableCheckpointing);
                    }
                }
                else if (result.Status == BatchOperationStatusEnum.Failed)
                {
                    await _taskHub.TaskFailed(operationId, 
                        $"Batch operation failed: {result.FailedCount} items failed", 
                        isRetryable: true);
                    
                    if (_notificationService != null)
                    {
                        await _notificationService.NotifyBatchOperationFailedAsync(
                            operationId,
                            operationType,
                            $"Batch operation failed: {result.FailedCount} items failed",
                            true,
                            context.ProcessedCount,
                            result.FailedCount);
                    }
                }

                // Record operation completion in history
                if (_historyService != null)
                {
                    await _historyService.RecordOperationCompletionAsync(operationId, result);
                }

                return result;
            }
            finally
            {
                _activeOperations.TryRemove(operationId, out _);
                context.CancellationTokenSource?.Dispose();
            }
        }

        /// <summary>
        /// Cancels an active batch operation
        /// </summary>
        public async Task<bool> CancelBatchOperationAsync(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var context))
            {
                try
                {
                    context.CancellationTokenSource?.Cancel();
                    _logger.LogInformation("Batch operation {OperationId} cancellation requested", operationId);
                    
                    await _taskHub.TaskCancelled(operationId, "Cancelled by user request");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling batch operation {OperationId}", operationId);
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the current status of a batch operation
        /// </summary>
        public BatchOperationStatus? GetOperationStatus(string operationId)
        {
            if (_activeOperations.TryGetValue(operationId, out var context))
            {
                var elapsed = DateTime.UtcNow - context.StartTime;
                var itemsPerSecond = context.ProcessedCount > 0 
                    ? context.ProcessedCount / elapsed.TotalSeconds 
                    : 0;
                
                var remainingItems = context.TotalItems - context.ProcessedCount;
                var estimatedTimeRemaining = itemsPerSecond > 0 
                    ? TimeSpan.FromSeconds(remainingItems / itemsPerSecond) 
                    : TimeSpan.Zero;

                return new BatchOperationStatus
                {
                    OperationId = operationId,
                    OperationType = context.OperationType,
                    TotalItems = context.TotalItems,
                    ProcessedCount = context.ProcessedCount,
                    SuccessCount = context.SuccessCount,
                    FailedCount = context.FailedCount,
                    ProgressPercentage = context.TotalItems > 0 
                        ? (context.ProcessedCount * 100) / context.TotalItems 
                        : 0,
                    ElapsedTime = elapsed,
                    EstimatedTimeRemaining = estimatedTimeRemaining,
                    ItemsPerSecond = itemsPerSecond,
                    Status = context.Status,
                    CurrentItem = context.CurrentItem
                };
            }

            return null;
        }

        private async Task<BatchOperationResult> ProcessBatchAsync<T>(
            BatchOperationContext context,
            List<T> items,
            Func<T, CancellationToken, Task<BatchItemResult>> processItemFunc,
            BatchOperationOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var errors = new ConcurrentBag<BatchItemError>();
            var processedItems = new ConcurrentBag<BatchItemResult>();
            
            // Local counters for thread-safe increments
            int processedCount = 0;
            int successCount = 0;
            int failedCount = 0;
            
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                    CancellationToken = context.CancellationTokenSource.Token
                };

                await Parallel.ForEachAsync(items, parallelOptions, async (item, ct) =>
                {
                    if (ct.IsCancellationRequested)
                        return;

                    var itemIndex = items.IndexOf(item);
                    context.CurrentItem = $"Item {itemIndex + 1} of {items.Count}";

                    try
                    {
                        // Process item
                        var result = await processItemFunc(item, ct);
                        processedItems.Add(result);

                        // Update counters
                        Interlocked.Increment(ref processedCount);
                        if (result.Success)
                        {
                            Interlocked.Increment(ref successCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref failedCount);
                            errors.Add(new BatchItemError
                            {
                                ItemIndex = itemIndex,
                                Error = result.Error ?? "Unknown error",
                                ItemIdentifier = result.ItemIdentifier
                            });
                        }

                        // Update context counters
                        context.ProcessedCount = processedCount;
                        context.SuccessCount = successCount;
                        context.FailedCount = failedCount;
                        
                        // Report progress
                        var progress = (processedCount * 100) / context.TotalItems;
                        var elapsed = stopwatch.Elapsed;
                        var itemsPerSecond = processedCount / elapsed.TotalSeconds;
                        var remainingItems = context.TotalItems - processedCount;
                        var estimatedRemaining = itemsPerSecond > 0 
                            ? TimeSpan.FromSeconds(remainingItems / itemsPerSecond) 
                            : TimeSpan.Zero;

                        var progressMessage = $"Processed {processedCount}/{context.TotalItems} " +
                                            $"({successCount} succeeded, {failedCount} failed) - " +
                                            $"{itemsPerSecond:F1} items/sec - " +
                                            $"ETA: {estimatedRemaining:mm\\:ss}";

                        await _taskHub.TaskProgress(context.OperationId, progress, progressMessage);

                        // Send detailed progress notification if service is available
                        if (_notificationService != null)
                        {
                            await _notificationService.NotifyBatchOperationProgressAsync(
                                context.OperationId,
                                processedCount,
                                successCount,
                                failedCount,
                                itemsPerSecond,
                                elapsed,
                                estimatedRemaining,
                                context.CurrentItem,
                                progressMessage);
                        }

                        // Save checkpoint if enabled
                        if (options.EnableCheckpointing && processedCount % options.CheckpointInterval == 0)
                        {
                            await SaveCheckpointAsync(context, processedItems.ToList(), errors.ToList());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing batch item {ItemIndex}", itemIndex);
                        
                        Interlocked.Increment(ref processedCount);
                        Interlocked.Increment(ref failedCount);
                        
                        errors.Add(new BatchItemError
                        {
                            ItemIndex = itemIndex,
                            Error = ex.Message,
                            ItemIdentifier = item?.ToString()
                        });

                        if (!options.ContinueOnError)
                        {
                            context.Status = BatchOperationStatusEnum.Failed;
                            throw;
                        }
                    }
                });

                context.Status = context.CancellationTokenSource.Token.IsCancellationRequested
                    ? BatchOperationStatusEnum.Cancelled
                    : BatchOperationStatusEnum.Completed;
            }
            catch (OperationCanceledException)
            {
                context.Status = BatchOperationStatusEnum.Cancelled;
                _logger.LogInformation("Batch operation {OperationId} was cancelled", context.OperationId);
            }
            catch (Exception ex)
            {
                context.Status = BatchOperationStatusEnum.Failed;
                _logger.LogError(ex, "Batch operation {OperationId} failed", context.OperationId);
            }

            stopwatch.Stop();

            var result = new BatchOperationResult
            {
                OperationId = context.OperationId,
                OperationType = context.OperationType,
                Status = context.Status,
                TotalItems = context.TotalItems,
                SuccessCount = context.SuccessCount,
                FailedCount = context.FailedCount,
                Duration = stopwatch.Elapsed,
                ItemsPerSecond = context.ProcessedCount / stopwatch.Elapsed.TotalSeconds,
                Errors = errors.ToList(),
                ProcessedItems = processedItems.ToList()
            };

            // Log summary
            _logger.LogInformation(
                "Batch operation {OperationId} completed: {Status} - " +
                "Processed {ProcessedCount}/{TotalItems} items in {Duration:mm\\:ss} - " +
                "Success: {SuccessCount}, Failed: {FailedCount} - " +
                "Rate: {ItemsPerSecond:F1} items/sec",
                context.OperationId,
                context.Status,
                context.ProcessedCount,
                context.TotalItems,
                stopwatch.Elapsed,
                context.SuccessCount,
                context.FailedCount,
                result.ItemsPerSecond);

            return result;
        }

        private async Task SaveCheckpointAsync(
            BatchOperationContext context,
            List<BatchItemResult> processedItems,
            List<BatchItemError> errors)
        {
            try
            {
                if (_historyService != null)
                {
                    var checkpointData = new
                    {
                        ProcessedCount = context.ProcessedCount,
                        SuccessCount = context.SuccessCount,
                        FailedCount = context.FailedCount,
                        ProcessedItems = processedItems.Select(p => new { p.ItemIdentifier, p.Success }),
                        Errors = errors.Select(e => new { e.ItemIndex, e.ItemIdentifier, e.Error })
                    };

                    await _historyService.UpdateCheckpointAsync(
                        context.OperationId,
                        context.ProcessedCount,
                        checkpointData);
                }
                else
                {
                    // Fallback to logging if history service not available
                    _logger.LogDebug(
                        "Saving checkpoint for operation {OperationId} - Processed: {ProcessedCount}",
                        context.OperationId,
                        context.ProcessedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save checkpoint for operation {OperationId}", context.OperationId);
            }
        }

        private class BatchOperationContext
        {
            public string OperationId { get; set; } = string.Empty;
            public string OperationType { get; set; } = string.Empty;
            public int TotalItems { get; set; }
            public int ProcessedCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailedCount { get; set; }
            public DateTime StartTime { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; } = new();
            public BatchOperationOptions Options { get; set; } = new();
            public BatchOperationStatusEnum Status { get; set; } = BatchOperationStatusEnum.Running;
            public string? CurrentItem { get; set; }
        }
    }

}