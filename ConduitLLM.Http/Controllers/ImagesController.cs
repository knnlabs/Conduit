using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Constants;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Http.Authorization;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles image generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/images")]
    [Authorize(AuthenticationSchemes = "VirtualKey,EphemeralKey")]
    [RequireBalance]
    [Tags("Images")]
    public class ImagesController : EventPublishingControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<ImagesController> _logger;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IAsyncTaskService _taskService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IMediaLifecycleService _mediaLifecycleService;
        private readonly IHttpClientFactory _httpClientFactory;

        public ImagesController(
            ILLMClientFactory clientFactory,
            IMediaStorageService storageService,
            ILogger<ImagesController> logger,
            IModelProviderMappingService modelMappingService,
            IAsyncTaskService taskService,
            IPublishEndpoint publishEndpoint,
            IVirtualKeyService virtualKeyService,
            IMediaLifecycleService mediaLifecycleService,
            IHttpClientFactory httpClientFactory)
            : base(publishEndpoint, logger)
        {
            _clientFactory = clientFactory;
            _storageService = storageService;
            _logger = logger;
            _modelMappingService = modelMappingService;
            _taskService = taskService;
            _virtualKeyService = virtualKeyService;
            _mediaLifecycleService = mediaLifecycleService;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Creates one or more images given a prompt.
        /// </summary>
        /// <param name="request">The image generation request.</param>
        /// <returns>Generated images.</returns>
        [HttpPost("generations")]
        public async Task<IActionResult> CreateImage([FromBody] ConduitLLM.Core.Models.ImageGenerationRequest request)
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
                
                // First check model mappings for image generation capability
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    // Check if the mapping indicates image generation support
                    supportsImageGen = mapping.SupportsImageGeneration;
                    
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}", 
                        modelName, supportsImageGen);
                    
                    // Store provider info for usage tracking
                    HttpContext.Items["ProviderId"] = mapping.ProviderId;
                    HttpContext.Items["ProviderType"] = mapping.Provider?.ProviderType;
                }
                else
                {
                    // Model must be mapped to be used
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

                // If we don't have a mapping, try to create a client anyway (for direct model names)
                if (mapping == null)
                {
                    _logger.LogWarning("No provider mapping found for model {Model}, attempting direct client creation", modelName);
                }

                // Create client for the model
                var client = _clientFactory.GetClient(modelName);
                
                // Update request with the provider's model ID if we have a mapping
                if (mapping != null)
                {
                    request.Model = mapping.ProviderModelId;
                }
                
                // Generate images
                var response = await client.CreateImageAsync(request);

                // Store generated images if they're base64 or external URLs
                for (int i = 0; i < response.Data.Count; i++)
                {
                    var imageData = response.Data[i];
                    Stream? imageStream = null;
                    string contentType = "image/png";
                    string extension = "png";
                    
                    _logger.LogInformation("Processing image {Index}: URL={Url}, HasB64={HasB64}", 
                        i, imageData.Url ?? "null", !string.IsNullOrEmpty(imageData.B64Json));
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(imageData.B64Json))
                        {
                            // Decode base64 to binary
                            try
                            {
                                var imageBytes = Convert.FromBase64String(imageData.B64Json);
                                imageStream = new MemoryStream(imageBytes);
                                _logger.LogInformation("Decoded base64 image, size: {Size} bytes", imageBytes.Length);
                            }
                            catch (FormatException ex)
                            {
                                _logger.LogError(ex, "Failed to decode base64 image data");
                                continue;
                            }
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url) && 
                                (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                 imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Stream external image directly to storage without buffering
                            using var httpClient = _httpClientFactory.CreateClient("ImageDownload");
                            httpClient.Timeout = TimeSpan.FromSeconds(60); // Increased timeout for streaming
                            
                            try
                            {
                                // Use GetAsync with HttpCompletionOption.ResponseHeadersRead for streaming
                                using var imageResponse = await httpClient.GetAsync(imageData.Url, 
                                    System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                                
                                if (imageResponse.IsSuccessStatusCode)
                                {
                                    // Try to determine content type from response
                                    if (imageResponse.Content.Headers.ContentType != null)
                                    {
                                        contentType = imageResponse.Content.Headers.ContentType.MediaType ?? contentType;
                                        extension = contentType.Split('/').LastOrDefault() ?? "png";
                                        if (extension == "jpeg") extension = "jpg";
                                    }
                                    else if (imageData.Url.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                                             imageData.Url.Contains(".jpg", StringComparison.OrdinalIgnoreCase))
                                    {
                                        contentType = "image/jpeg";
                                        extension = "jpg";
                                    }
                                    
                                    // Copy the stream to memory to avoid disposal issues
                                    var responseStream = await imageResponse.Content.ReadAsStreamAsync();
                                    var memoryStream = new MemoryStream();
                                    await responseStream.CopyToAsync(memoryStream);
                                    memoryStream.Position = 0;
                                    imageStream = memoryStream;
                                    
                                    _logger.LogInformation("Downloaded image data: {Bytes} bytes", memoryStream.Length);
                                }
                                else
                                {
                                    _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                                        imageData.Url, imageResponse.StatusCode);
                                    continue;
                                }
                            }
                            catch (TaskCanceledException ex)
                            {
                                _logger.LogWarning(ex, "Timeout downloading image from {Url}", imageData.Url);
                                continue;
                            }
                            catch (System.Net.Http.HttpRequestException ex)
                            {
                                _logger.LogWarning(ex, "HTTP error downloading image from {Url}", imageData.Url);
                                continue;
                            }
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url))
                        {
                            // Log non-HTTP URLs that we're not downloading
                            _logger.LogWarning("Image URL is not an HTTP/HTTPS URL, will not download: {Url}", imageData.Url);
                        }
                        else
                        {
                            _logger.LogWarning("Image data has neither URL nor base64 content");
                        }
                        
                        if (imageStream != null)
                        {
                            // Store in media storage directly with streaming
                            var metadata = new MediaMetadata
                            {
                                ContentType = contentType,
                                FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.{extension}",
                                MediaType = MediaType.Image,
                                CustomMetadata = new()
                                {
                                    ["prompt"] = request.Prompt,
                                    ["model"] = request.Model ?? "unknown",
                                    ["provider"] = mapping?.ProviderId.ToString() ?? "unknown",
                                    ["originalUrl"] = imageData.Url ?? ""
                                }
                            };

                            if (request.User != null)
                            {
                                metadata.CreatedBy = request.User;
                            }

                            // Create progress reporter for large image downloads
                            var progress = new Progress<long>(bytesProcessed =>
                            {
                                _logger.LogDebug("Image storage progress: {BytesProcessed} bytes processed", bytesProcessed);
                            });
                            
                            var storageResult = await _storageService.StoreAsync(imageStream, metadata, progress);

                            // Track media ownership for lifecycle management
                            try
                            {
                                // Get virtual key ID from HttpContext
                                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                                if (!string.IsNullOrEmpty(virtualKeyIdClaim) && int.TryParse(virtualKeyIdClaim, out var virtualKeyId))
                                {
                                    var mediaMetadata = new Core.Interfaces.MediaLifecycleMetadata
                                    {
                                        ContentType = contentType,
                                        SizeBytes = storageResult.SizeBytes,
                                        Provider = mapping?.Provider?.ProviderType.ToString() ?? "unknown",
                                        Model = request.Model ?? "unknown",
                                        Prompt = request.Prompt,
                                        StorageUrl = storageResult.Url,
                                        PublicUrl = storageResult.Url
                                    };

                                    await _mediaLifecycleService.TrackMediaAsync(
                                        virtualKeyId,
                                        storageResult.StorageKey,
                                        "image",
                                        mediaMetadata);
                                    
                                    _logger.LogInformation("Tracked media {StorageKey} for virtual key {VirtualKeyId}", 
                                        storageResult.StorageKey, virtualKeyId);
                                }
                                else
                                {
                                    _logger.LogWarning("Could not determine virtual key ID for media tracking");
                                }
                            }
                            catch (Exception trackEx)
                            {
                                // Don't fail the request if tracking fails
                                _logger.LogError(trackEx, "Failed to track media ownership, but continuing with response");
                            }
                            
                            // Update response with our proxied URL
                            _logger.LogInformation("Setting image URL: {Url}", storageResult.Url);
                            imageData.Url = storageResult.Url;
                            
                            // Handle response format
                            if (request.ResponseFormat == "b64_json")
                            {
                                // Read from storage to convert to base64
                                var storedStream = await _storageService.GetStreamAsync(storageResult.StorageKey);
                                if (storedStream != null)
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        await storedStream.CopyToAsync(ms);
                                        imageData.B64Json = Convert.ToBase64String(ms.ToArray());
                                    }
                                    storedStream.Dispose();
                                }
                                imageData.Url = null; // Clear URL when returning base64
                            }
                            else if (request.ResponseFormat == "url")
                            {
                                // Clear any base64 data when URL format is requested
                                imageData.B64Json = null;
                            }
                        }
                    }
                    finally
                    {
                        imageStream?.Dispose();
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating images");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "An error occurred while generating images",
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }

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