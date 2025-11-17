using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
namespace ConduitLLM.Core.Services.BatchOperations
{
    /// <summary>
    /// Batch operation for updating spend amounts across multiple virtual keys.
    /// DEPRECATED: Use BatchSpendUpdateOperationV2 instead for idempotency support and retry logic.
    /// </summary>
    /// <remarks>
    /// This implementation is maintained for backward compatibility but lacks:
    /// - Idempotency tracking (risk of duplicate processing)
    /// - Automatic retry logic for transient failures
    /// - Standardized error handling patterns
    ///
    /// Migration: Use BatchSpendUpdateOperationV2 and provide X-Idempotency-Token header.
    /// Scheduled for removal: TBD (after all clients migrate to V2)
    /// </remarks>
    [Obsolete("Use BatchSpendUpdateOperationV2 instead. This version lacks idempotency support and will be removed in a future release.", false)]
    public class BatchSpendUpdateOperation : IBatchSpendUpdateOperation
    {
        private readonly ILogger<BatchSpendUpdateOperation> _logger;
        private readonly IBatchOperationService _batchOperationService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ISpendNotificationService _spendNotificationService;

        public BatchSpendUpdateOperation(
            ILogger<BatchSpendUpdateOperation> logger,
            IBatchOperationService batchOperationService,
            IVirtualKeyService virtualKeyService,
            ISpendNotificationService spendNotificationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _spendNotificationService = spendNotificationService ?? throw new ArgumentNullException(nameof(spendNotificationService));
        }

        /// <summary>
        /// Execute batch spend update operation
        /// </summary>
        public async Task<BatchOperationResult> ExecuteAsync(
            List<SpendUpdateItem> spendUpdates,
            int virtualKeyId,
            CancellationToken cancellationToken = default)
        {
            var options = new BatchOperationOptions
            {
                VirtualKeyId = virtualKeyId,
                MaxDegreeOfParallelism = 10, // Limit parallelism for database operations
                ContinueOnError = true,
                EnableCheckpointing = true,
                CheckpointInterval = 50,
                Metadata = new Dictionary<string, object>
                {
                    ["updateType"] = "spend_batch_update",
                    ["source"] = "batch_operation"
                }
            };

            return await _batchOperationService.StartBatchOperationAsync(
                "spend_update",
                spendUpdates,
                ProcessSpendUpdateAsync,
                options,
                cancellationToken);
        }

        private async Task<BatchItemResult> ProcessSpendUpdateAsync(
            SpendUpdateItem item,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Validate virtual key exists
                var virtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(item.VirtualKeyId, cancellationToken);
                if (virtualKey == null)
                {
                    return new BatchItemResult
                    {
                        Success = false,
                        ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                        Error = "Virtual key not found",
                        Duration = stopwatch.Elapsed
                    };
                }

                // Apply spend update
                await _virtualKeyService.UpdateSpendAsync(item.VirtualKeyId, item.Amount);

                // Send real-time notification
                await _spendNotificationService.NotifySpendUpdatedAsync(
                    item.VirtualKeyId,
                    item.Amount,
                    item.Model,
                    item.Provider);

                return new BatchItemResult
                {
                    Success = true,
                    ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                    Duration = stopwatch.Elapsed,
                    Data = new
                    {
                        VirtualKeyId = item.VirtualKeyId,
                        Amount = item.Amount
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to process spend update for virtual key {VirtualKeyId}", 
                    item.VirtualKeyId);

                return new BatchItemResult
                {
                    Success = false,
                    ItemIdentifier = $"VKey-{item.VirtualKeyId}",
                    Error = ex.Message,
                    Duration = stopwatch.Elapsed
                };
            }
        }
    }
}