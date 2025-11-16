using Microsoft.AspNetCore.Authorization;
using ConduitLLM.Configuration.DTOs;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.DTOs.BatchOperations;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// API controller for managing batch operations with real-time progress tracking
    /// </summary>
    [ApiController]
    [Route("v1/batch")]
    [Authorize]
    public class BatchOperationsController : ControllerBase
    {
        private readonly ILogger<BatchOperationsController> _logger;
        private readonly IBatchOperationService _batchOperationService;
        private readonly IBatchSpendUpdateOperation _batchSpendUpdateOperation;
        private readonly IBatchVirtualKeyUpdateOperation _batchVirtualKeyUpdateOperation;
        private readonly IBatchWebhookSendOperation _batchWebhookSendOperation;
        private readonly IVirtualKeyService _virtualKeyService;

        public BatchOperationsController(
            ILogger<BatchOperationsController> logger,
            IBatchOperationService batchOperationService,
            IBatchSpendUpdateOperation batchSpendUpdateOperation,
            IBatchVirtualKeyUpdateOperation batchVirtualKeyUpdateOperation,
            IBatchWebhookSendOperation batchWebhookSendOperation,
            IVirtualKeyService virtualKeyService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchOperationService = batchOperationService ?? throw new ArgumentNullException(nameof(batchOperationService));
            _batchSpendUpdateOperation = batchSpendUpdateOperation ?? throw new ArgumentNullException(nameof(batchSpendUpdateOperation));
            _batchVirtualKeyUpdateOperation = batchVirtualKeyUpdateOperation ?? throw new ArgumentNullException(nameof(batchVirtualKeyUpdateOperation));
            _batchWebhookSendOperation = batchWebhookSendOperation ?? throw new ArgumentNullException(nameof(batchWebhookSendOperation));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
        }

        /// <summary>
        /// Start a batch spend update operation
        /// </summary>
        /// <param name="request">Batch spend update request</param>
        /// <returns>Operation result with tracking ID</returns>
        [HttpPost("spend-updates")]
        [ProducesResponseType(typeof(BatchOperationStartResponse), 202)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> StartBatchSpendUpdate([FromBody] BatchSpendUpdateRequest request)
        {
            var virtualKeyId = GetVirtualKeyId();
            
            // Validate request
            if (request.Updates == null || request.Updates.Count() == 0)
            {
                return BadRequest(new ErrorResponseDto("No updates provided"));
            }

            if (request.Updates.Count() > 10000)
            {
                return BadRequest(new ErrorResponseDto("Maximum 10,000 items per batch"));
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

            // Start operation
            var result = await _batchSpendUpdateOperation.ExecuteAsync(
                spendUpdates,
                virtualKeyId,
                HttpContext.RequestAborted);

            _logger.LogInformation(
                "Started batch spend update operation {OperationId} with {Count} items",
                result.OperationId,
                request.Updates.Count());

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
            if (request.Updates == null || request.Updates.Count() == 0)
            {
                return BadRequest(new ErrorResponseDto("No updates provided"));
            }

            if (request.Updates.Count() > 1000)
            {
                return BadRequest(new ErrorResponseDto("Maximum 1,000 items per batch"));
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

            _logger.LogInformation(
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
            if (request.Webhooks == null || request.Webhooks.Count() == 0)
            {
                return BadRequest(new ErrorResponseDto("No webhooks provided"));
            }

            if (request.Webhooks.Count() > 5000)
            {
                return BadRequest(new ErrorResponseDto("Maximum 5,000 webhooks per batch"));
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

            _logger.LogInformation(
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
            var status = _batchOperationService.GetOperationStatus(operationId);
            if (status == null)
            {
                return NotFound(new ErrorResponseDto("Operation not found"));
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
                return NotFound(new ErrorResponseDto("Operation not found"));
            }

            if (!status.CanCancel)
            {
                return Conflict(new ErrorResponseDto("Operation cannot be cancelled"));
            }

            var cancelled = await _batchOperationService.CancelBatchOperationAsync(operationId);
            if (!cancelled)
            {
                return Conflict(new ErrorResponseDto("Failed to cancel operation"));
            }

            _logger.LogInformation("Cancelled batch operation {OperationId}", operationId);
            return NoContent();
        }

        private int GetVirtualKeyId()
        {
            var claim = User.FindFirst("VirtualKeyId");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
