using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles image generation requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/images")]
    [Authorize]
    public class ImagesController : ControllerBase
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IMediaStorageService _storageService;
        private readonly ILogger<ImagesController> _logger;
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IImageGenerationQueue _imageQueue;
        private readonly IAsyncTaskService _taskService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IVirtualKeyService _virtualKeyService;

        public ImagesController(
            ILLMClientFactory clientFactory,
            IMediaStorageService storageService,
            ILogger<ImagesController> logger,
            IProviderDiscoveryService discoveryService,
            IModelProviderMappingService modelMappingService,
            IImageGenerationQueue imageQueue,
            IAsyncTaskService taskService,
            IPublishEndpoint publishEndpoint,
            IVirtualKeyService virtualKeyService)
        {
            _clientFactory = clientFactory;
            _storageService = storageService;
            _logger = logger;
            _discoveryService = discoveryService;
            _modelMappingService = modelMappingService;
            _imageQueue = imageQueue;
            _taskService = taskService;
            _publishEndpoint = publishEndpoint;
            _virtualKeyService = virtualKeyService;
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
                    return BadRequest(new { error = new { message = "Prompt is required", type = "invalid_request_error" } });
                }

                var modelName = request.Model ?? "dall-e-3";
                
                // First check model mappings for image generation capability
                var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelName);
                bool supportsImageGen = false;
                
                if (mapping != null)
                {
                    // Check if the mapping indicates image generation support
                    supportsImageGen = mapping.SupportsImageGeneration;
                    
                    _logger.LogInformation("Model {Model} mapping found, supports image generation: {Supports}", 
                        modelName, supportsImageGen);
                }
                else
                {
                    // Fall back to discovery service if no mapping exists
                    _logger.LogInformation("No mapping found for {Model}, using discovery service", modelName);
                    supportsImageGen = await _discoveryService.TestModelCapabilityAsync(
                        modelName, 
                        ModelCapability.ImageGeneration);
                }
                
                if (!supportsImageGen)
                {
                    return BadRequest(new { error = new { message = $"Model {modelName} does not support image generation", type = "invalid_request_error" } });
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
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(imageData.B64Json))
                        {
                            // Convert base64 to bytes
                            var imageBytes = Convert.FromBase64String(imageData.B64Json);
                            imageStream = new MemoryStream(imageBytes);
                        }
                        else if (!string.IsNullOrEmpty(imageData.Url) && 
                                (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                                 imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Download external image to proxy it through our storage
                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(30);
                            
                            var imageResponse = await httpClient.GetAsync(imageData.Url);
                            if (imageResponse.IsSuccessStatusCode)
                            {
                                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                                imageStream = new MemoryStream(imageBytes);
                                
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
                            }
                            else
                            {
                                _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                                    imageData.Url, imageResponse.StatusCode);
                                continue;
                            }
                        }
                        
                        if (imageStream != null)
                        {
                            // Read the image bytes first if we need base64
                            byte[]? imageBytes = null;
                            if (request.ResponseFormat == "b64_json")
                            {
                                // Read bytes for base64 conversion
                                using (var ms = new MemoryStream())
                                {
                                    await imageStream.CopyToAsync(ms);
                                    imageBytes = ms.ToArray();
                                }
                                // Create new stream for storage
                                imageStream = new MemoryStream(imageBytes);
                            }
                            
                            // Store in media storage
                            var metadata = new MediaMetadata
                            {
                                ContentType = contentType,
                                FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.{extension}",
                                MediaType = MediaType.Image,
                                CustomMetadata = new()
                                {
                                    ["prompt"] = request.Prompt,
                                    ["model"] = request.Model ?? "unknown",
                                    ["provider"] = mapping?.ProviderName ?? "unknown",
                                    ["originalUrl"] = imageData.Url ?? ""
                                }
                            };

                            if (request.User != null)
                            {
                                metadata.CreatedBy = request.User;
                            }

                            var storageResult = await _storageService.StoreAsync(imageStream, metadata);

                            // TODO: Media Ownership Tracking - We need to record this media in a database table
                            // to track which virtual key owns it. Currently we only store CreatedBy in metadata.
                            // Without DB tracking, we can't clean up media when virtual keys are deleted.
                            // See: docs/TODO-Media-Lifecycle-Management.md for implementation plan
                            
                            // Update response with our proxied URL
                            imageData.Url = storageResult.Url;
                            
                            // Handle response format
                            if (request.ResponseFormat == "b64_json" && imageBytes != null)
                            {
                                // Use the bytes we already read
                                imageData.B64Json = Convert.ToBase64String(imageBytes);
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
                return StatusCode(500, new { error = new { message = "An error occurred while generating images", type = "server_error" } });
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
                    return BadRequest(new { error = new { message = "Prompt is required", type = "invalid_request_error" } });
                }

                var modelName = request.Model ?? "dall-e-3";
                
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
                    _logger.LogInformation("No mapping found for {Model}, using discovery service", modelName);
                    supportsImageGen = await _discoveryService.TestModelCapabilityAsync(
                        modelName, 
                        ModelCapability.ImageGeneration);
                }
                
                if (!supportsImageGen)
                {
                    return BadRequest(new { error = new { message = $"Model {modelName} does not support image generation", type = "invalid_request_error" } });
                }

                // Get virtual key information
                var virtualKeyHash = HttpContext.User.FindFirst("key_hash")?.Value;
                if (string.IsNullOrEmpty(virtualKeyHash))
                {
                    return Unauthorized(new { error = new { message = "Invalid authentication", type = "authentication_error" } });
                }

                var virtualKey = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKeyHash);
                if (virtualKey == null)
                {
                    return Unauthorized(new { error = new { message = "Virtual key not found", type = "authentication_error" } });
                }

                // Create task ID
                var taskId = Guid.NewGuid().ToString();
                
                // Create async task
                var createdTaskId = await _taskService.CreateTaskAsync("image_generation", new
                {
                    taskId = taskId,
                    request = request,
                    model = modelName,
                    virtualKeyId = virtualKey.Id
                });

                // Create and enqueue the generation request
                var generationRequest = new ImageGenerationRequested
                {
                    TaskId = taskId,
                    VirtualKeyId = virtualKey.Id,
                    VirtualKeyHash = virtualKeyHash,
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
                    CorrelationId = Guid.NewGuid().ToString()
                };

                // Enqueue for processing
                await _imageQueue.EnqueueAsync(generationRequest);

                // Return accepted response with task information
                var response = new
                {
                    taskId = taskId,
                    status = "queued",
                    statusUrl = Url.Action(nameof(GetGenerationStatus), null, new { taskId }, Request.Scheme),
                    created = DateTime.UtcNow
                };

                return Accepted(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating async image generation task");
                return StatusCode(500, new { error = new { message = "An error occurred while creating the task", type = "server_error" } });
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
                // Get task from service
                var task = await _taskService.GetTaskStatusAsync(taskId);
                if (task == null)
                {
                    return NotFound(new { error = new { message = "Task not found", type = "not_found_error" } });
                }

                // Verify user owns this task
                var virtualKeyHash = HttpContext.User.FindFirst("key_hash")?.Value;
                if (task.Metadata != null)
                {
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    var metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadataDict != null && metadataDict.TryGetValue("virtualKeyId", out var keyIdObj))
                    {
                        var taskVirtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(Convert.ToInt32(keyIdObj.ToString()));
                        if (taskVirtualKey == null || taskVirtualKey.KeyHash != virtualKeyHash)
                        {
                            return NotFound(new { error = new { message = "Task not found", type = "not_found_error" } });
                        }
                    }
                }

                // Build response
                var response = new
                {
                    taskId = task.TaskId,
                    status = task.State.ToString().ToLowerInvariant(),
                    created = task.CreatedAt,
                    updated = task.UpdatedAt,
                    progress = task.ProgressPercentage,
                    result = task.State == TaskState.Completed ? task.Result : null,
                    error = task.State == TaskState.Failed ? task.Error : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting task status for {TaskId}", taskId);
                return StatusCode(500, new { error = new { message = "An error occurred while getting task status", type = "server_error" } });
            }
        }

        /// <summary>
        /// Cancels an async image generation task if it hasn't started processing yet.
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
                    return NotFound(new { error = new { message = "Task not found", type = "not_found_error" } });
                }

                // Verify user owns this task
                var virtualKeyHash = HttpContext.User.FindFirst("key_hash")?.Value;
                if (task.Metadata != null)
                {
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    var metadataDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(metadataJson);
                    if (metadataDict != null && metadataDict.TryGetValue("virtualKeyId", out var keyIdObj))
                    {
                        var taskVirtualKey = await _virtualKeyService.GetVirtualKeyInfoForValidationAsync(Convert.ToInt32(keyIdObj.ToString()));
                        if (taskVirtualKey == null || taskVirtualKey.KeyHash != virtualKeyHash)
                        {
                            return NotFound(new { error = new { message = "Task not found", type = "not_found_error" } });
                        }
                    }
                }

                // Check if task can be cancelled
                if (task.State == TaskState.Completed || task.State == TaskState.Failed)
                {
                    return BadRequest(new { error = new { message = "Task has already completed", type = "invalid_request_error" } });
                }

                // Cancel the task
                await _taskService.UpdateTaskStatusAsync(taskId, TaskState.Failed, error: "Cancelled by user");

                return Ok(new { message = "Task cancelled successfully", taskId = taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling task {TaskId}", taskId);
                return StatusCode(500, new { error = new { message = "An error occurred while cancelling the task", type = "server_error" } });
            }
        }

    }
}