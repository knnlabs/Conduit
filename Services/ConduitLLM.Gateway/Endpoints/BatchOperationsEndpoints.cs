using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs.BatchOperations;
using ConduitLLM.Core.Services.BatchOperations;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

namespace ConduitLLM.Gateway.Endpoints
{
    /// <summary>
    /// API controller for managing batch operations with real-time progress tracking.
    /// Supports idempotency via the Idempotency-Key header.
    /// </summary>
    public class BatchOperationsEndpoints : GatewayEndpointHandlerBase
    {
        private readonly IBatchOperationService _batchOperationService;
        private readonly IBatchVirtualKeyUpdateOperation _batchVirtualKeyUpdateOperation;
        private readonly IBatchWebhookSendOperation _batchWebhookSendOperation;
        private readonly BatchSpendUpdateOperation _batchSpendUpdateOperation;

        public BatchOperationsEndpoints(
            ILogger<BatchOperationsEndpoints> logger,
            IHttpContextAccessor httpContextAccessor,
            IBatchOperationService batchOperationService,
            IBatchVirtualKeyUpdateOperation batchVirtualKeyUpdateOperation,
            IBatchWebhookSendOperation batchWebhookSendOperation,
            BatchSpendUpdateOperation batchSpendUpdateOperation)
            : base(null, httpContextAccessor, logger)
        {
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _batchVirtualKeyUpdateOperation = batchVirtualKeyUpdateOperation ?? throw new ArgumentNullException(nameof(batchVirtualKeyUpdateOperation));
            _batchWebhookSendOperation = batchWebhookSendOperation ?? throw new ArgumentNullException(nameof(batchWebhookSendOperation));
            _batchSpendUpdateOperation = batchSpendUpdateOperation ?? throw new ArgumentNullException(nameof(batchSpendUpdateOperation));
        }

        /// <summary>
        /// Start a batch spend update operation.
        /// Supports idempotency via the Idempotency-Key header to prevent duplicate processing.
        /// </summary>
        /// <param name="request">Batch spend update request</param>
        /// <param name="idempotencyKey">Optional key used to deduplicate retries.</param>
        /// <returns>Operation result with tracking ID</returns>
        /// <remarks>
        /// Include the Idempotency-Key header to enable duplicate detection.
        /// Duplicate requests with the same token will return the cached result.
        /// </remarks>
        public async Task<IResult> StartBatchSpendUpdate(
            BatchSpendUpdateRequest request,
            string? idempotencyKey)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Validate request
            if (request.Updates == null || !request.Updates.Any())
            {
                return OpenAIError(400, "No updates provided", "invalid_request");
            }

            if (request.Updates.Count() > 10000)
            {
                return OpenAIError(400, "Maximum 10,000 items per batch", "invalid_request");
            }

            // Convert to internal model
            var spendUpdates = request.Updates.Select(u => new SpendUpdateItem
            {
                VirtualKeyId = u.VirtualKeyId,
                Amount = u.Amount,
                Model = u.Model,
                Provider = u.ProviderType.ToString(),
                RequestMetadata = u.Metadata
            }).ToList();

            // Execute batch spend update operation
            var result = await _batchSpendUpdateOperation.ExecuteAsync(
                spendUpdates,
                virtualKeyId,
                idempotencyKey,
                HttpContext.RequestAborted);

            Logger.LogInformation(
                "Started batch spend update operation {OperationId} with {Count} items (Idempotent: {Idempotent})",
                result.OperationId,
                request.Updates.Count(),
                !string.IsNullOrWhiteSpace(idempotencyKey));

            GatewayOpsMetrics.RecordBatchOperation("spend_update", "accepted", request.Updates.Count());
            return Accepted(new BatchOperationStartResponse
            {
                OperationId = result.OperationId,
                OperationType = "spend_update",
                TotalItems = request.Updates.Count(),
                StatusUrl = $"/internal/operations/batch/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Start a batch virtual key update operation
        /// </summary>
        /// <param name="request">Batch virtual key update request</param>
        /// <returns>Operation result with tracking ID</returns>
        public async Task<IResult> StartBatchVirtualKeyUpdate(BatchVirtualKeyUpdateRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Validate request
            if (request.Updates == null || !request.Updates.Any())
            {
                return OpenAIError(400, "No updates provided", "invalid_request");
            }

            if (request.Updates.Count() > 1000)
            {
                return OpenAIError(400, "Maximum 1,000 items per batch", "invalid_request");
            }

            // Convert to internal model
            var keyUpdates = request.Updates.Select(u => new VirtualKeyUpdateItem
            {
                VirtualKeyId = u.VirtualKeyId,
                AllowedModels = u.AllowedModels,
                RateLimits = u.RateLimits,
                IsEnabled = u.IsEnabled,
                ExpiresAt = u.ExpiresAt,
                Notes = u.Notes
            }).ToList();

            // Start operation
            var result = await _batchVirtualKeyUpdateOperation.ExecuteAsync(
                keyUpdates,
                virtualKeyId,
                HttpContext.RequestAborted);

            Logger.LogInformation(
                "Started batch virtual key update operation {OperationId} with {Count} items",
                result.OperationId,
                request.Updates.Count());

            return Accepted(new BatchOperationStartResponse
            {
                OperationId = result.OperationId,
                OperationType = "virtual_key_update",
                TotalItems = request.Updates.Count(),
                StatusUrl = $"/internal/operations/batch/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Start a batch webhook send operation
        /// </summary>
        /// <param name="request">Batch webhook send request</param>
        /// <returns>Operation result with tracking ID</returns>
        public async Task<IResult> StartBatchWebhookSend(BatchWebhookSendRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Validate request
            if (request.Webhooks == null || !request.Webhooks.Any())
            {
                return OpenAIError(400, "No webhooks provided", "invalid_request");
            }

            if (request.Webhooks.Count() > 5000)
            {
                return OpenAIError(400, "Maximum 5,000 webhooks per batch", "invalid_request");
            }

            // Convert to internal model
            var webhookSends = request.Webhooks.Select(w => new WebhookSendItem
            {
                WebhookUrl = w.Url,
                VirtualKeyId = virtualKeyId,
                EventType = w.EventType,
                Payload = w.Payload,
                Headers = w.Headers,
                Secret = w.Secret
            }).ToList();

            // Start operation
            var result = await _batchWebhookSendOperation.ExecuteAsync(
                webhookSends,
                virtualKeyId,
                HttpContext.RequestAborted);

            Logger.LogInformation(
                "Started batch webhook send operation {OperationId} with {Count} items",
                result.OperationId,
                request.Webhooks.Count());

            return Accepted(new BatchOperationStartResponse
            {
                OperationId = result.OperationId,
                OperationType = "webhook_send",
                TotalItems = request.Webhooks.Count(),
                StatusUrl = $"/internal/operations/batch/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Get the status of a batch operation
        /// </summary>
        /// <param name="operationId">Operation ID</param>
        /// <returns>Current operation status</returns>
        public IResult GetOperationStatus(string operationId)
        {
            Logger.LogDebug("Getting status for batch operation {OperationId}", operationId);
            var status = _batchOperationService.GetOperationStatus(operationId);
            if (status == null)
            {
                Logger.LogWarning("Batch operation {OperationId} not found", operationId);
                return OpenAIError(404, "Operation not found", "not_found", "not_found_error");
            }

            return Ok(new BatchOperationStatusResponse
            {
                OperationId = status.OperationId,
                OperationType = status.OperationType,
                Status = status.Status.ToString(),
                TotalItems = status.TotalItems,
                ProcessedCount = status.ProcessedCount,
                SuccessCount = status.SuccessCount,
                FailedCount = status.FailedCount,
                ProgressPercentage = status.ProgressPercentage,
                ElapsedTime = status.ElapsedTime,
                EstimatedTimeRemaining = status.EstimatedTimeRemaining,
                ItemsPerSecond = status.ItemsPerSecond,
                CurrentItem = status.CurrentItem,
                CanCancel = status.CanCancel
            });
        }

        /// <summary>
        /// Cancel an active batch operation
        /// </summary>
        /// <param name="operationId">Operation ID to cancel</param>
        /// <returns>Cancellation result</returns>
        public async Task<IResult> CancelOperation(string operationId)
        {
            var status = _batchOperationService.GetOperationStatus(operationId);
            if (status == null)
            {
                return OpenAIError(404, "Operation not found", "not_found", "not_found_error");
            }

            if (!status.CanCancel)
            {
                return OpenAIError(409, "Operation cannot be cancelled", "operation_not_cancellable");
            }

            var cancelled = await _batchOperationService.CancelBatchOperationAsync(operationId);
            if (!cancelled)
            {
                return OpenAIError(409, "Failed to cancel operation", "cancellation_failed");
            }

            Logger.LogInformation("Cancelled batch operation {OperationId}", operationId);
            return NoContent();
        }

        private int GetVirtualKeyId()
        {
            var claim = User.FindFirst("VirtualKeyId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
