using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
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
                    supportsImageGen = mapping.SupportsImageGeneration;
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}", 
                        modelName, supportsImageGen);
                }
                else
                {
                    _logger.LogWarning("No mapping found for model {Model}. Model must be configured in model mappings.", modelName);
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

                // Get virtual key ID from authenticated user claims
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out var virtualKeyId))
                {
                    return Unauthorized(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Invalid authentication",
                            Type = "invalid_request_error",
                            Code = "unauthorized"
                        }
                    });
                }

                // Get virtual key information from service
                var virtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(virtualKeyId);
                if (virtualKey == null)
                {
                    return Unauthorized(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = "Virtual key not found",
                            Type = "invalid_request_error",
                            Code = "unauthorized"
                        }
                    });
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
                        User = request.User
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
                    taskId, modelName);

                // Return accepted response with task information
                var response = new AsyncTaskResponse
                {
                    TaskId = taskId,
                    Status = TaskStateConstants.Queued, 
                    CheckStatusUrl = Url.Action(nameof(GetGenerationStatus), null, new { taskId }, Request.Scheme),
                    CreatedAt = DateTime.UtcNow
                };

                return Accepted(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating async image generation task");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "An error occurred while creating the task",
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
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

                // Verify user owns this task by comparing virtual key IDs
                var userVirtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (task.Metadata != null && !string.IsNullOrEmpty(userVirtualKeyIdClaim) && int.TryParse(userVirtualKeyIdClaim, out var userVirtualKeyId))
                {
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    var metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadataDict != null && metadataDict.TryGetValue("virtualKeyId", out var keyIdObj))
                    {
                        var taskVirtualKeyId = Convert.ToInt32(keyIdObj.ToString());
                        _logger.LogInformation("Validating task ownership - Task VirtualKeyId: {TaskKeyId}, User VirtualKeyId: {UserKeyId}", 
                            taskVirtualKeyId, userVirtualKeyId);
                        
                        // Compare the virtual key IDs
                        if (taskVirtualKeyId != userVirtualKeyId)
                        {
                            _logger.LogWarning("Virtual key ID mismatch for task {TaskId} - Expected: {Expected}, Got: {Got}", 
                                taskId, taskVirtualKeyId, userVirtualKeyId);
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
                        
                        _logger.LogInformation("Virtual key validation successful for task {TaskId}", taskId);
                    }
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
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "An error occurred while getting task status",
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
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

                // Verify user owns this task by comparing virtual key IDs
                var userVirtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (task.Metadata != null && !string.IsNullOrEmpty(userVirtualKeyIdClaim) && int.TryParse(userVirtualKeyIdClaim, out var userVirtualKeyId))
                {
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    var metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadataDict != null && metadataDict.TryGetValue("virtualKeyId", out var keyIdObj))
                    {
                        var taskVirtualKeyId = Convert.ToInt32(keyIdObj.ToString());
                        _logger.LogInformation("Validating task ownership - Task VirtualKeyId: {TaskKeyId}, User VirtualKeyId: {UserKeyId}", 
                            taskVirtualKeyId, userVirtualKeyId);
                        
                        // Compare the virtual key IDs
                        if (taskVirtualKeyId != userVirtualKeyId)
                        {
                            _logger.LogWarning("Virtual key ID mismatch for task {TaskId} - Expected: {Expected}, Got: {Got}", 
                                taskId, taskVirtualKeyId, userVirtualKeyId);
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
                        
                        _logger.LogInformation("Virtual key validation successful for task {TaskId}", taskId);
                    }
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

                // Get virtual key ID from metadata for event publishing
                var cancelVirtualKeyId = 0;
                if (task.Metadata != null)
                {
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    var metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadataDict != null && metadataDict.TryGetValue("virtualKeyId", out var keyIdObj))
                    {
                        cancelVirtualKeyId = Convert.ToInt32(keyIdObj.ToString());
                    }
                }

                // Publish cancellation event
                PublishEventFireAndForget(new ImageGenerationCancelled
                {
                    TaskId = taskId,
                    VirtualKeyId = cancelVirtualKeyId,
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
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "An error occurred while cancelling the task",
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }
    }
}