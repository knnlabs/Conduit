using System.Text.Json;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.Entities;

namespace ConduitLLM.Gateway.Services
{
    /// <summary>
    /// Service for managing batch operation history
    /// </summary>
    public class BatchOperationHistoryService : IBatchOperationHistoryService
    {
        private readonly IBatchOperationHistoryRepository _repository;
        private readonly ILogger<BatchOperationHistoryService> _logger;

        public BatchOperationHistoryService(
            IBatchOperationHistoryRepository repository,
            ILogger<BatchOperationHistoryService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RecordOperationStartAsync(
            string operationId,
            string operationType,
            int virtualKeyId,
            int totalItems,
            BatchOperationOptions options)
        {
            try
            {
                var history = new BatchOperationHistory
                {
                    OperationId = operationId,
                    OperationType = operationType,
                    VirtualKeyId = virtualKeyId,
                    TotalItems = totalItems,
                    SuccessCount = 0,
                    FailedCount = 0,
                    Status = BatchOperationStatusEnum.Running.ToString(),
                    StartedAt = DateTime.UtcNow,
                    CanResume = options.EnableCheckpointing,
                    Metadata = JsonSerializer.Serialize(options.Metadata)
                };

                await _repository.SaveAsync(history);
                
                _logger.LogInformation(
                    "Recorded start of batch operation {OperationId} - Type: {OperationType}, Items: {TotalItems}",
                    operationId, operationType, totalItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to record batch operation start for {OperationId}",
                    operationId);
                // Don't throw - history recording should not break the operation
            }
        }

        public async Task RecordOperationCompletionAsync(
            string operationId,
            BatchOperationResult result)
        {
            try
            {
                var existing = await _repository.GetByIdAsync(operationId);
                if (existing == null)
                {
                    // The start record is what carries virtual-key attribution; without it
                    // we cannot persist a truthful history row (fabricating VirtualKeyId = 0
                    // would misattribute the operation), so log and skip.
                    _logger.LogWarning(
                        "Cannot record completion of batch operation {OperationId}: no start record exists",
                        operationId);
                    return;
                }

                existing.SuccessCount = result.SuccessCount;
                existing.FailedCount = result.FailedCount;
                existing.Status = result.Status.ToString();
                existing.CompletedAt = result.CompletedAt ?? DateTime.UtcNow;
                existing.DurationSeconds = result.Duration.TotalSeconds;
                existing.ItemsPerSecond = result.ItemsPerSecond;

                if (result.Status == BatchOperationStatusEnum.Failed && result.Errors.Any())
                {
                    existing.ErrorMessage = $"{result.FailedCount} items failed";
                    existing.ErrorDetails = JsonSerializer.Serialize(result.Errors);
                }
                else if (result.Status == BatchOperationStatusEnum.Cancelled)
                {
                    // The result does not carry the cancellation cause (user request,
                    // shutdown, timeout, ...) — record the fact without inventing one
                    existing.CancellationReason = "Operation was cancelled";
                }

                // Store summary of results
                if (result.ProcessedItems.Any())
                {
                    var summary = new
                    {
                        TotalProcessed = result.ProcessedItems.Count,
                        SuccessCount = result.SuccessCount,
                        FailedCount = result.FailedCount,
                        Duration = result.Duration.ToString(),
                        ItemsPerSecond = result.ItemsPerSecond
                    };
                    existing.ResultSummary = JsonSerializer.Serialize(summary);
                }

                await _repository.UpdateAsync(existing);

                _logger.LogInformation(
                    "Recorded completion of batch operation {OperationId} - Status: {Status}, Success: {Success}, Failed: {Failed}",
                    operationId, result.Status, result.SuccessCount, result.FailedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to record batch operation completion for {OperationId}",
                    operationId);
                // Don't throw - history recording should not break the operation
            }
        }

        public async Task UpdateCheckpointAsync(
            string operationId,
            int lastProcessedIndex,
            object checkpointData)
        {
            try
            {
                var existing = await _repository.GetByIdAsync(operationId);
                if (existing == null)
                {
                    _logger.LogWarning(
                        "Cannot update checkpoint - operation {OperationId} not found",
                        operationId);
                    return;
                }

                existing.LastProcessedIndex = lastProcessedIndex;
                existing.CheckpointData = JsonSerializer.Serialize(checkpointData);

                await _repository.UpdateAsync(existing);
                
                _logger.LogDebug(
                    "Updated checkpoint for operation {OperationId} - Last processed: {Index}",
                    operationId, lastProcessedIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to update checkpoint for {OperationId}",
                    operationId);
                // Don't throw - checkpoint update should not break the operation
            }
        }

        public async Task<BatchOperationResumptionData?> GetResumptionDataAsync(string operationId)
        {
            try
            {
                var history = await _repository.GetByIdAsync(operationId);
                if (history == null || !history.CanResume)
                {
                    return null;
                }

                return new BatchOperationResumptionData
                {
                    OperationId = history.OperationId,
                    OperationType = history.OperationType,
                    LastProcessedIndex = history.LastProcessedIndex ?? 0,
                    CheckpointData = history.CheckpointData,
                    SuccessCount = history.SuccessCount,
                    FailedCount = history.FailedCount,
                    StartedAt = history.StartedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to get resumption data for {OperationId}",
                    operationId);
                return null;
            }
        }
    }
}