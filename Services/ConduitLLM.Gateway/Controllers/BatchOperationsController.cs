using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Configuration.DTOs.BatchOperations;
using ConduitLLM.Core.Services.BatchOperations;
using ConduitLLM.Gateway.Filters;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// API controller for managing batch operations with real-time progress tracking.
    /// Supports idempotency tokens via X-Idempotency-Token header.
    /// </summary>
    [ApiController]
    [Route("v1/batch")]
    [Authorize]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class BatchOperationsController : GatewayControllerBase
    {
        private readonly IBatchOperationService _batchOperationService;
        private readonly IBatchVirtualKeyUpdateOperation _batchVirtualKeyUpdateOperation;
        private readonly IBatchWebhookSendOperation _batchWebhookSendOperation;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly BatchSpendUpdateOperation _batchSpendUpdateOperation;

        public BatchOperationsController(
            ILogger<BatchOperationsController> logger,
            IBatchOperationService batchOperationService,
            IBatchVirtualKeyUpdateOperation batchVirtualKeyUpdateOperation,
            IBatchWebhookSendOperation batchWebhookSendOperation,
            IVirtualKeyService virtualKeyService,
            BatchSpendUpdateOperation batchSpendUpdateOperation)
            : base(logger)
        {
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _batchVirtualKeyUpdateOperation = batchVirtualKeyUpdateOperation ?? throw new ArgumentNullException(nameof(batchVirtualKeyUpdateOperation));
            _batchWebhookSendOperation = batchWebhookSendOperation ?? throw new ArgumentNullException(nameof(batchWebhookSendOperation));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _batchSpendUpdateOperation = batchSpendUpdateOperation ?? throw new ArgumentNullException(nameof(batchSpendUpdateOperation));
        }

        /// <summary>
        /// Start a batch spend update operation.
        /// Supports idempotency via X-Idempotency-Token header to prevent duplicate processing.
        /// </summary>
        /// <param name="request">Batch spend update request</param>
        /// <returns>Operation result with tracking ID</returns>
        /// <remarks>
        /// Include X-Idempotency-Token header to enable duplicate detection.
        /// Duplicate requests with the same token will return the cached result.
        /// </remarks>
        [HttpPost("spend-updates")]
        [ProducesResponseType(typeof(BatchOperationStartResponse), 202)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartBatchSpendUpdate([FromBody] BatchSpendUpdateRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Validate request
            if (request.Updates == null || !request.Updates.Any())
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "No updates provided",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
            }

            if (request.Updates.Count() > 10000)
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Maximum 10,000 items per batch",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
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

            // Get idempotency token from header (optional)
            var idempotencyToken = HttpContext.Request.Headers["X-Idempotency-Token"].FirstOrDefault();

            // Execute batch spend update operation
            var result = await _batchSpendUpdateOperation.ExecuteAsync(
                spendUpdates,
                virtualKeyId,
                idempotencyToken,
                HttpContext.RequestAborted);

            Logger.LogInformation(
                "Started batch spend update operation {OperationId} with {Count} items (Idempotent: {Idempotent})",
                result.OperationId,
                request.Updates.Count(),
                !string.IsNullOrWhiteSpace(idempotencyToken));

            GatewayOpsMetrics.RecordBatchOperation("spend_update", "accepted", request.Updates.Count());
            return Accepted(new BatchOperationStartResponse
            {
                OperationId = result.OperationId,
                OperationType = "spend_update",
                TotalItems = request.Updates.Count(),
                StatusUrl = $"/v1/batch/operations/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Start a batch virtual key update operation
        /// </summary>
        /// <param name="request">Batch virtual key update request</param>
        /// <returns>Operation result with tracking ID</returns>
        [HttpPost("virtual-key-updates")]
        [ProducesResponseType(typeof(BatchOperationStartResponse), 202)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartBatchVirtualKeyUpdate([FromBody] BatchVirtualKeyUpdateRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Check if user has admin permissions
            var virtualKeyInfo = await _virtualKeyService.GetVirtualKeyInfoAsync(virtualKeyId);
            bool isAdmin = false;
            if (virtualKeyInfo != null && !string.IsNullOrEmpty(virtualKeyInfo.Metadata))
            {
                try
                {
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(virtualKeyInfo.Metadata);
                    if (metadata != null && metadata.TryGetValue("isAdmin", out var isAdminValue))
                    {
                        isAdmin = isAdminValue?.ToString()?.ToLower() == "true";
                    }
                }
                catch
                {
                    // Invalid metadata format
                }
            }

            if (!isAdmin)
            {
                return Forbid("Admin permissions required for batch virtual key updates");
            }

            // Validate request
            if (request.Updates == null || !request.Updates.Any())
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "No updates provided",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
            }

            if (request.Updates.Count() > 1000)
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Maximum 1,000 items per batch",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
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
                StatusUrl = $"/v1/batch/operations/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Start a batch webhook send operation
        /// </summary>
        /// <param name="request">Batch webhook send request</param>
        /// <returns>Operation result with tracking ID</returns>
        [HttpPost("webhook-sends")]
        [ProducesResponseType(typeof(BatchOperationStartResponse), 202)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartBatchWebhookSend([FromBody] BatchWebhookSendRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();

            // Validate request
            if (request.Webhooks == null || !request.Webhooks.Any())
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "No webhooks provided",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
            }

            if (request.Webhooks.Count() > 5000)
            {
                return BadRequest(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Maximum 5,000 webhooks per batch",
                        Type = "invalid_request_error",
                        Code = "invalid_request"
                    }
                });
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
                StatusUrl = $"/v1/batch/operations/{result.OperationId}",
                TaskId = result.OperationId,
                Message = "Batch operation started. Subscribe to TaskHub with the taskId for real-time updates."
            });
        }

        /// <summary>
        /// Get the status of a batch operation
        /// </summary>
        /// <param name="operationId">Operation ID</param>
        /// <returns>Current operation status</returns>
        [HttpGet("operations/{operationId}")]
        [ProducesResponseType(typeof(BatchOperationStatusResponse), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetOperationStatus(string operationId)
        {
            Logger.LogDebug("Getting status for batch operation {OperationId}", operationId);
            var status = _batchOperationService.GetOperationStatus(operationId);
            if (status == null)
            {
                Logger.LogWarning("Batch operation {OperationId} not found", operationId);
                return NotFound(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Operation not found",
                        Type = "not_found_error",
                        Code = "not_found"
                    }
                });
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
        [HttpPost("operations/{operationId}/cancel")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> CancelOperation(string operationId)
        {
            var status = _batchOperationService.GetOperationStatus(operationId);
            if (status == null)
            {
                return NotFound(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Operation not found",
                        Type = "not_found_error",
                        Code = "not_found"
                    }
                });
            }

            if (!status.CanCancel)
            {
                return Conflict(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Operation cannot be cancelled",
                        Type = "invalid_request_error",
                        Code = "operation_not_cancellable"
                    }
                });
            }

            var cancelled = await _batchOperationService.CancelBatchOperationAsync(operationId);
            if (!cancelled)
            {
                return Conflict(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Failed to cancel operation",
                        Type = "invalid_request_error",
                        Code = "cancellation_failed"
                    }
                });
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
