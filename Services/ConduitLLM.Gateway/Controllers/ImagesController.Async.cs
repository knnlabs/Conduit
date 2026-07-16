using System.Diagnostics;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Gateway.Metrics;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;
using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Images controller - Asynchronous image generation functionality
    /// </summary>
    public partial class ImagesController
    {
        /// <summary>
        /// Creates an async image generation task.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <returns>Task information with status URL.</returns>
        [HttpPost("generations/async")]
        public async Task<IActionResult> CreateImageAsync([FromBody] ConduitLLM.Core.Models.ImageGenerationRequest request)
        {
            using var activity = GatewayRequestMetrics.StartImageGenerationActivity(
                request.Model ?? "unknown", isAsync: true);

            try
            {
                // Validate request
                if (string.IsNullOrWhiteSpace(request.Prompt))
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Prompt is required",
                            Type = "invalid_request_error",
                            Code = "missing_parameter",
                            Param = "prompt"
                        }
                    });
                }

                // Model parameter is required
                if (string.IsNullOrWhiteSpace(request.Model))
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Model is required",
                            Type = "invalid_request_error",
                            Code = "missing_parameter",
                            Param = "model"
                        }
                    });
                }
                
                var modelName = request.Model;
                
                // Check model capabilities
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    supportsImageGen = mapping.ModelProviderTypeAssociation?.Model?.SupportsImageGeneration ?? false;
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}",
                        LoggingSanitizer.S(modelName), supportsImageGen);
                }
                else
                {
                    _logger.LogWarning("No mapping found for model {Model}. Model must be configured in model mappings.", LoggingSanitizer.S(modelName));
                    supportsImageGen = false;
                }
                
                if (!supportsImageGen)
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Model {modelName} does not support image generation",
                            Type = "invalid_request_error",
                            Code = "unsupported_model",
                            Param = "model"
                        }
                    });
                }

                if (CurrentVirtualKeyId == null)
                {
                    return OpenAIError(401, "Virtual key not found in request context", "unauthorized");
                }
                var virtualKeyId = CurrentVirtualKeyId.Value;

                // Get virtual key information from service
                var virtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKeyId);
                if (virtualKey == null)
                {
                    return OpenAIError(401, "Virtual key not found", "unauthorized");
                }

                // Create correlation ID
                var correlationId = Guid.NewGuid().ToString();

                // Create the generation request event first so we can store it as metadata
                var generationRequest = new ImageGenerationRequested
                {
                    TaskId = "", // Will be filled in after task creation
                    VirtualKeyId = virtualKeyId,
                    VirtualKeyHash = virtualKey.KeyHash,
                    Request = new ConduitLLM.Core.Events.ImageGenerationRequest
                    {
                        Prompt = request.Prompt,
                        Model = request.Model,
                        N = request.N,
                        Size = request.Size,
                        Quality = request.Quality,
                        Style = request.Style,
                        ResponseFormat = request.ResponseFormat,
                        User = request.User,
                        Image = request.Image,
                        Mask = request.Mask,
                        Operation = request.Operation,
                        ExtensionData = request.ExtensionData
                    },
                    UserId = HttpContext.User.FindFirst("sub")?.Value ?? "anonymous",
                    Priority = 0, // Normal priority
                    RequestedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                };

                // Create metadata for the task including the serialized request
                var metadata = new TaskMetadata(virtualKeyId)
                {
                    Model = modelName,
                    Prompt = request.Prompt,
                    CorrelationId = correlationId,
                    Payload = System.Text.Json.JsonSerializer.Serialize(generationRequest)
                };

                // Create the task using the correct method signature
                var taskId = await _taskService.CreateTaskAsync(
                    taskType: "image_generation",
                    virtualKeyId: virtualKeyId,
                    metadata: metadata);

                // Update the request with the actual task ID
                generationRequest = generationRequest with { TaskId = taskId };

                // Publish the event directly to MassTransit for immediate processing
                PublishEventFireAndForget(generationRequest, "create async image generation", new { TaskId = taskId, Model = modelName });
                
                _logger.LogInformation("Created async image generation task {TaskId} for model {Model} and published event",
                    taskId, LoggingSanitizer.S(modelName));

                // Return accepted response with task information
                var response = new AsyncTaskResponse
                {
                    TaskId = taskId,
                    Status = TaskStateConstants.Queued, 
                    CheckStatusUrl = Url.Action(nameof(GetGenerationStatus), null, new { taskId }, Request.Scheme),
                    CreatedAt = DateTime.UtcNow
                };

                GatewayOpsMetrics.RecordMediaOperation("generate", "image_async", "queued");
                return Accepted(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating async image generation task");
                GatewayOpsMetrics.RecordMediaOperation("generate", "image_async", "error");
                return OpenAIError(500, "An error occurred while creating the task", "internal_error", "server_error");
            }
        }

        /// <summary>
        /// Gets the status of an async image generation task.
        /// </summary>
        /// <param name="taskId">The task ID.</param>
        /// <returns>Current task status and results if completed.</returns>
        [HttpGet("generations/{taskId}/status")]
        public async Task<IActionResult> GetGenerationStatus(string taskId)
        {
            try
            {
                _logger.LogInformation("GetGenerationStatus called for task {TaskId}", taskId);
                
                // Get task from service
                var task = await _taskService.GetTaskStatusAsync(taskId);
                if (task == null)
                {
                    _logger.LogWarning("Task {TaskId} not found by task service", taskId);
                    return NotFound(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Task not found",
                            Type = "invalid_request_error",
                            Code = "not_found",
                            Param = "task_id"
                        }
                    });
                }
                
                _logger.LogInformation("Task {TaskId} retrieved, State: {State}, HasMetadata: {HasMetadata}",
                    taskId, task.State, task.Metadata != null);

                // Verify user owns this task. Return 404 (not 403) to avoid leaking task existence.
                var callerVirtualKeyId = CurrentVirtualKeyId;
                if (callerVirtualKeyId != null && task.Metadata != null && task.Metadata.VirtualKeyId != callerVirtualKeyId.Value)
                {
                    _logger.LogWarning("Virtual key {CallerKeyId} attempted to access task {TaskId} owned by {OwnerKeyId}",
                        callerVirtualKeyId.Value, taskId, task.Metadata.VirtualKeyId);
                    return NotFound(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Task not found",
                            Type = "invalid_request_error",
                            Code = "not_found",
                            Param = "task_id"
                        }
                    });
                }

                // Build response
                var response = new AsyncTaskStatusResponse
                {
                    TaskId = task.TaskId,
                    Status = TaskStateConstants.FromTaskState(task.State),
                    CreatedAt = task.CreatedAt,
                    UpdatedAt = task.UpdatedAt,
                    Progress = task.Progress,
                    Result = task.State == TaskState.Completed ? task.Result : null,
                    Error = task.State == TaskState.Failed ? task.Error : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task status for {TaskId}", taskId);
                return OpenAIError(500, "An error occurred while getting task status", "internal_error", "server_error");
            }
        }

        /// <summary>
        /// Cancels an async image generation task.
        /// </summary>
        /// <param name="taskId">The task ID to cancel.</param>
        /// <returns>Cancellation result.</returns>
        [HttpDelete("generations/{taskId}")]
        public async Task<IActionResult> CancelGeneration(string taskId)
        {
            try
            {
                // Get task from service
                var task = await _taskService.GetTaskStatusAsync(taskId);
                if (task == null)
                {
                    return NotFound(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Task not found",
                            Type = "invalid_request_error",
                            Code = "not_found",
                            Param = "task_id"
                        }
                    });
                }

                // Verify user owns this task. Return 404 (not 403) to avoid leaking task existence.
                var callerVirtualKeyId = CurrentVirtualKeyId;
                if (callerVirtualKeyId != null && task.Metadata != null && task.Metadata.VirtualKeyId != callerVirtualKeyId.Value)
                {
                    _logger.LogWarning("Virtual key {CallerKeyId} attempted to cancel task {TaskId} owned by {OwnerKeyId}",
                        callerVirtualKeyId.Value, taskId, task.Metadata.VirtualKeyId);
                    return NotFound(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Task not found",
                            Type = "invalid_request_error",
                            Code = "not_found",
                            Param = "task_id"
                        }
                    });
                }

                // Check if task can be cancelled
                if (task.State == TaskState.Completed || task.State == TaskState.Failed || task.State == TaskState.Cancelled)
                {
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Task has already completed",
                            Type = "invalid_request_error",
                            Code = "invalid_operation"
                        }
                    });
                }

                // Publish cancellation event using the task owner's virtual key ID
                PublishEventFireAndForget(new ImageGenerationCancelled
                {
                    TaskId = taskId,
                    VirtualKeyId = task.Metadata?.VirtualKeyId ?? 0,
                    Reason = "Cancelled by user request",
                    CancelledAt = DateTime.UtcNow,
                    CorrelationId = Guid.NewGuid().ToString()
                }, "cancel image generation", new { TaskId = taskId });

                _logger.LogInformation("Published cancellation event for image generation task {TaskId}", taskId);

                return Ok(new { message = "Task cancellation requested", task_id = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling task {TaskId}", taskId);
                return OpenAIError(500, "An error occurred while cancelling the task", "internal_error", "server_error");
            }
        }
    }
}