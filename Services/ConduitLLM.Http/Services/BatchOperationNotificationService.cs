using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Http.Hubs;

namespace ConduitLLM.Http.Services
{
    /// <summary>
    /// Service for sending real-time batch operation notifications through SignalR
    /// </summary>
    public class BatchOperationNotificationService : IBatchOperationNotificationService
    {
        private readonly IHubContext<TaskHub> _hubContext;
        private readonly ILogger<BatchOperationNotificationService> _logger;

        public BatchOperationNotificationService(
            IHubContext<TaskHub> hubContext,
            ILogger<BatchOperationNotificationService> logger)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task NotifyBatchOperationStartedAsync(
            string operationId,
            string operationType,
            int totalItems,
            int virtualKeyId,
            BatchOperationOptions options)
        {
            try
            {
                var notification = new BatchOperationStartedNotification
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    TotalItems = totalItems,
                    VirtualKeyId = virtualKeyId,
                    MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                    SupportsCancellation = true,
                    SupportsResume = options.EnableCheckpointing,
                    StartedAt = DateTime.UtcNow,
                    Metadata = options.Metadata
                };

                // Send to specific task subscribers
                await _hubContext.Clients.Group($"task-{operationId}")
                    .SendAsync("BatchOperationStarted", notification);

                // Send to virtual key's batch operation subscribers
                await _hubContext.Clients.Group($"vkey-{virtualKeyId}-batch_{operationType}")
                    .SendAsync("BatchOperationStarted", notification);

                _logger.LogInformation(
                    "Batch operation {OperationId} of type {OperationType} started with {TotalItems} items",
                    operationId, operationType, totalItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error sending BatchOperationStarted notification for operation {OperationId}",
                    operationId);
            }
        }

        public async Task NotifyBatchOperationProgressAsync(
            string operationId,
            int processedCount,
            int successCount,
            int failedCount,
            double itemsPerSecond,
            TimeSpan elapsedTime,
            TimeSpan estimatedTimeRemaining,
            string? currentItem = null,
            string? message = null)
        {
            try
            {
                var progressPercentage = 0;
                if (processedCount > 0)
                {
                    // We need total items to calculate percentage
                    // This would be tracked in the batch operation context
                    // For now, we'll include it in the notification
                }

                var notification = new BatchOperationProgressNotification
                {
                    OperationId = operationId,
                    ProcessedCount = processedCount,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    ProgressPercentage = progressPercentage,
                    ItemsPerSecond = itemsPerSecond,
                    ElapsedTime = elapsedTime,
                    EstimatedTimeRemaining = estimatedTimeRemaining,
                    CurrentItem = currentItem,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                };

                // Send to task subscribers
                await _hubContext.Clients.Group($"task-{operationId}")
                    .SendAsync("BatchOperationProgress", notification);

                _logger.LogDebug(
                    "Batch operation {OperationId} progress: {ProcessedCount} processed, {SuccessCount} succeeded, {FailedCount} failed",
                    operationId, processedCount, successCount, failedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending BatchOperationProgress notification for operation {OperationId}",
                    operationId);
            }
        }

        public async Task NotifyBatchItemCompletedAsync(
            string operationId,
            int itemIndex,
            string? itemIdentifier,
            bool success,
            string? error,
            TimeSpan duration,
            object? result = null)
        {
            try
            {
                var notification = new BatchOperationItemCompletedNotification
                {
                    OperationId = operationId,
                    ItemIndex = itemIndex,
                    ItemIdentifier = itemIdentifier,
                    Success = success,
                    Error = error,
                    Duration = duration,
                    Result = result,
                    CompletedAt = DateTime.UtcNow
                };

                // Send to task subscribers who want item-level updates
                await _hubContext.Clients.Group($"task-{operationId}-items")
                    .SendAsync("BatchItemCompleted", notification);

                _logger.LogDebug(
                    "Batch operation {OperationId} item {ItemIndex} completed: {Success}",
                    operationId, itemIndex, success ? "Success" : "Failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending BatchItemCompleted notification for operation {OperationId} item {ItemIndex}",
                    operationId, itemIndex);
            }
        }

        public async Task NotifyBatchOperationCompletedAsync(
            string operationId,
            string operationType,
            BatchOperationStatusEnum status,
            int totalItems,
            int successCount,
            int failedCount,
            TimeSpan duration,
            double averageItemsPerSecond,
            object? resultSummary = null)
        {
            try
            {
                var notification = new BatchOperationCompletedNotification
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    Status = status.ToString(),
                    TotalItems = totalItems,
                    SuccessCount = successCount,
                    FailedCount = failedCount,
                    Duration = duration,
                    AverageItemsPerSecond = averageItemsPerSecond,
                    CompletedAt = DateTime.UtcNow,
                    ResultSummary = resultSummary,
                    Errors = new List<BatchOperationError>() // Would be populated from context
                };

                // Send to task subscribers
                await _hubContext.Clients.Group($"task-{operationId}")
                    .SendAsync("BatchOperationCompleted", notification);

                _logger.LogInformation(
                    "Batch operation {OperationId} completed with status {Status}: {SuccessCount}/{TotalItems} succeeded in {Duration}",
                    operationId, status, successCount, totalItems, duration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending BatchOperationCompleted notification for operation {OperationId}",
                    operationId);
            }
        }

        public async Task NotifyBatchOperationFailedAsync(
            string operationId,
            string operationType,
            string error,
            bool isRetryable,
            int processedCount,
            int failedCount,
            string? stackTrace = null)
        {
            try
            {
                var notification = new BatchOperationFailedNotification
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    Error = error,
                    IsRetryable = isRetryable,
                    ProcessedCount = processedCount,
                    FailedCount = failedCount,
                    FailedAt = DateTime.UtcNow,
                    StackTrace = stackTrace
                };

                // Send to task subscribers
                await _hubContext.Clients.Group($"task-{operationId}")
                    .SendAsync("BatchOperationFailed", notification);

                _logger.LogError(
                    "Batch operation {OperationId} failed: {Error}. Processed: {ProcessedCount}, Failed: {FailedCount}",
                    operationId, error, processedCount, failedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending BatchOperationFailed notification for operation {OperationId}",
                    operationId);
            }
        }

        public async Task NotifyBatchOperationCancelledAsync(
            string operationId,
            string operationType,
            string? reason,
            int processedCount,
            int remainingCount,
            bool canResume)
        {
            try
            {
                var notification = new BatchOperationCancelledNotification
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    Reason = reason,
                    ProcessedCount = processedCount,
                    RemainingCount = remainingCount,
                    CanResume = canResume,
                    CancelledAt = DateTime.UtcNow
                };

                // Send to task subscribers
                await _hubContext.Clients.Group($"task-{operationId}")
                    .SendAsync("BatchOperationCancelled", notification);

                _logger.LogInformation(
                    "Batch operation {OperationId} cancelled: {Reason}. Processed: {ProcessedCount}, Remaining: {RemainingCount}",
                    operationId, reason ?? "User requested", processedCount, remainingCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error sending BatchOperationCancelled notification for operation {OperationId}",
                    operationId);
            }
        }
    }
}