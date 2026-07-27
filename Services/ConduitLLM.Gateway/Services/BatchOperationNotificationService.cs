using Microsoft.AspNetCore.SignalR;
using ConduitLLM.Configuration.DTOs.BatchOperations;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs.SignalR;
using ConduitLLM.Core.Services;
using ConduitLLM.Gateway.Hubs;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for sending real-time batch operation notifications through SignalR.
    /// Inherits from SignalRNotificationServiceBase for standardized error handling.
    /// </summary>
    public class BatchOperationNotificationService
        : SignalRNotificationServiceBase<TaskHub>,
          IBatchOperationNotificationService
    {
        public BatchOperationNotificationService(
            IHubContext<TaskHub> hubContext,
            ILogger<BatchOperationNotificationService> logger)
            : base(hubContext, logger)
        {
        }

        public async Task NotifyBatchOperationStartedAsync(
            string operationId,
            string operationType,
            int totalItems,
            int virtualKeyId,
            BatchOperationOptions options)
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

            await SendToGroupAsync($"task-{operationId}", "BatchOperationStarted", notification);
            await SendToGroupAsync($"vkey-{virtualKeyId}-batch_{operationType}", "BatchOperationStarted", notification);

            Logger.LogInformation(
                "Batch operation {OperationId} of type {OperationType} started with {TotalItems} items",
                operationId, operationType, totalItems);
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
            var notification = new BatchOperationProgressNotification
            {
                OperationId = operationId,
                ProcessedCount = processedCount,
                SuccessCount = successCount,
                FailedCount = failedCount,
                ProgressPercentage = 0,
                ItemsPerSecond = itemsPerSecond,
                ElapsedTime = elapsedTime,
                EstimatedTimeRemaining = estimatedTimeRemaining,
                CurrentItem = currentItem,
                Message = message,
                Timestamp = DateTime.UtcNow
            };

            await SendToGroupAsync($"task-{operationId}", "BatchOperationProgress", notification);

            Logger.LogDebug(
                "Batch operation {OperationId} progress: {ProcessedCount} processed, {SuccessCount} succeeded, {FailedCount} failed",
                operationId, processedCount, successCount, failedCount);
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
                Errors = new List<BatchItemError>()
            };

            await SendToGroupAsync($"task-{operationId}", "BatchOperationCompleted", notification);

            Logger.LogInformation(
                "Batch operation {OperationId} completed with status {Status}: {SuccessCount}/{TotalItems} succeeded in {Duration}",
                operationId, status, successCount, totalItems, duration);
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

            await SendToGroupAsync($"task-{operationId}", "BatchOperationFailed", notification);

            Logger.LogError(
                "Batch operation {OperationId} failed: {Error}. Processed: {ProcessedCount}, Failed: {FailedCount}",
                operationId, error, processedCount, failedCount);
        }

        public async Task NotifyBatchOperationCancelledAsync(
            string operationId,
            string operationType,
            string? reason,
            int processedCount,
            int remainingCount,
            bool canResume)
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

            await SendToGroupAsync($"task-{operationId}", "BatchOperationCancelled", notification);

            Logger.LogInformation(
                "Batch operation {OperationId} cancelled: {Reason}. Processed: {ProcessedCount}, Remaining: {RemainingCount}",
                operationId, reason ?? "User requested", processedCount, remainingCount);
        }
    }
}
