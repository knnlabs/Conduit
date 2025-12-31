using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Core.Services.BatchOperations
{
    /// <summary>
    /// Batch operation for updating spend amounts across multiple virtual keys.
    /// Refactored implementation using BatchOperationBase for consistency and idempotency.
    /// </summary>
    public class BatchSpendUpdateOperation : BatchOperationBase<SpendUpdateItem>
    {
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ISpendNotificationService _spendNotificationService;

        public BatchSpendUpdateOperation(
            ILogger<BatchSpendUpdateOperation> logger,
            IBatchOperationService batchOperationService,
            IVirtualKeyService virtualKeyService,
            ISpendNotificationService spendNotificationService,
            IBatchOperationIdempotencyService? idempotencyService = null)
            : base(logger, batchOperationService, idempotencyService)
        {
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _spendNotificationService = spendNotificationService ?? throw new ArgumentNullException(nameof(spendNotificationService));
        }

        /// <summary>
        /// Execute batch spend update operation with idempotency support
        /// </summary>
        /// <param name="spendUpdates">List of spend updates to apply</param>
        /// <param name="virtualKeyId">Virtual key ID for authorization and tracking</param>
        /// <param name="idempotencyToken">Optional token to prevent duplicate processing</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Result of the batch operation</returns>
        public new virtual async Task<BatchOperationResult> ExecuteAsync(
            List<SpendUpdateItem> spendUpdates,
            int virtualKeyId,
            string? idempotencyToken = null,
            CancellationToken cancellationToken = default)
        {
            return await base.ExecuteAsync(
                spendUpdates,
                virtualKeyId,
                idempotencyToken,
                cancellationToken);
        }

        protected override string GetOperationType() => "spend_update";

        protected override Task ValidateBatchAsync(List<SpendUpdateItem> items, CancellationToken cancellationToken)
        {
            if (items == null || items.Count == 0)
            {
                throw new ArgumentException("Spend updates list cannot be null or empty", nameof(items));
            }

            // Additional batch-level validation if needed
            return Task.CompletedTask;
        }

        protected override async Task ValidateItemAsync(SpendUpdateItem item, CancellationToken cancellationToken)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (item.VirtualKeyId <= 0)
            {
                throw new InvalidOperationException($"Invalid virtual key ID: {item.VirtualKeyId}");
            }

            if (item.Amount < 0)
            {
                throw new InvalidOperationException($"Spend amount cannot be negative: {item.Amount}");
            }

            // Validate virtual key exists
            var virtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(
                item.VirtualKeyId,
                cancellationToken);

            if (virtualKey == null)
            {
                throw new InvalidOperationException($"Virtual key not found: {item.VirtualKeyId}");
            }
        }

        protected override async Task<BatchItemResult> ProcessItemAsync(
            SpendUpdateItem item,
            CancellationToken cancellationToken)
        {
            // Apply spend update (exceptions will propagate to base class retry logic)
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
                Data = new
                {
                    VirtualKeyId = item.VirtualKeyId,
                    Amount = item.Amount,
                    Model = item.Model,
                    Provider = item.Provider
                }
            };
        }

        protected override string GetItemIdentifier(SpendUpdateItem item) => $"VKey-{item.VirtualKeyId}";

        protected override BatchOperationOptions ConfigureBatchOptions(int virtualKeyId)
        {
            return new BatchOperationOptions
            {
                VirtualKeyId = virtualKeyId,
                MaxDegreeOfParallelism = 10, // Limit parallelism for database operations
                ContinueOnError = true,
                EnableCheckpointing = true,
                CheckpointInterval = 50,
                Metadata = new Dictionary<string, object>
                {
                    ["updateType"] = "spend_batch_update",
                    ["source"] = "batch_operation_v2",
                    ["framework"] = "BatchOperationBase"
                }
            };
        }

        protected override RetryOptions RetryOptions => new()
        {
            MaxRetries = 3,
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0
        };

        protected override bool IsRetryableException(Exception exception)
        {
            // Retry on transient database errors and timeout
            return exception is TimeoutException
                || exception is TaskCanceledException
                || (exception.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false)
                || base.IsRetryableException(exception);
        }
    }
}
