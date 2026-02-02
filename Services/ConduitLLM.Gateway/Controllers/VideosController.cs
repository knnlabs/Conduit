using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ConduitLLM.Gateway.Authorization;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Constants;

namespace ConduitLLM.Gateway.Controllers
{
    /// <summary>
    /// Controller for video generation operations following OpenAI-compatible patterns.
    /// </summary>
    [ApiController]
    [Route("v1/videos")]
    [Authorize(AuthenticationSchemes = "VirtualKey")]
    [RequireBalance]
    [EnableRateLimiting("VirtualKeyPolicy")]
    [Tags("Videos")]
    public class VideosController : ControllerBase
    {
        private readonly IVideoGenerationService _videoService;
        private readonly IAsyncTaskService _taskService;
        private readonly IOperationTimeoutProvider _timeoutProvider;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly ILogger<VideosController> _logger;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideosController"/> class.
        /// </summary>
        public VideosController(
            IVideoGenerationService videoService,
            IAsyncTaskService taskService,
            IOperationTimeoutProvider timeoutProvider,
            ICancellableTaskRegistry taskRegistry,
            ILogger<VideosController> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService)
        {
            _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _timeoutProvider = timeoutProvider ?? throw new ArgumentNullException(nameof(timeoutProvider));
            _taskRegistry = taskRegistry ?? throw new ArgumentNullException(nameof(taskRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
        }

        /// <summary>
        /// Starts an asynchronous video generation task.
        /// </summary>
        /// <param name="request">The video generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task information including task ID for status checking.</returns>
        /// <response code="202">Video generation task started.</response>
        /// <response code="400">Invalid request parameters.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="403">Virtual key does not have permission.</response>
        /// <response code="429">Rate limit exceeded.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("generations/async")]
        [ProducesResponseType(typeof(VideoGenerationTaskResponse), 202)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 403)]
        [ProducesResponseType(typeof(ProblemDetails), 429)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> GenerateVideoAsync(
            [FromBody][Required] VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Get virtual key and ID from HttpContext (set by VirtualKeyAuthenticationMiddleware)
                var virtualKey = HttpContext.Items["VirtualKey"]?.ToString();
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                
                if (string.IsNullOrEmpty(virtualKey) || string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                // Store video request parameters for usage tracking and pricing
                StoreVideoRequestParameters(request);

                // Get provider info for usage tracking
                try
                {
                    var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                    if (modelMapping != null)
                    {
                        HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                        HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;

                        // Store ModelCostId for direct cost lookup (preferred over string matching)
                        if (modelMapping.ModelProviderTypeAssociation?.ModelCostId != null)
                        {
                            HttpContext.Items[HttpContextKeys.ModelCostId] = modelMapping.ModelProviderTypeAssociation.ModelCostId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
                }

                // Create a linked cancellation token that can be controlled independently
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                
                var response = await _videoService.GenerateVideoWithTaskAsync(
                    request,
                    virtualKey,
                    cts.Token);

                // Extract task ID from the response
                var taskId = response.Data?.FirstOrDefault()?.Url?.Replace("pending:", "");
                if (string.IsNullOrEmpty(taskId))
                {
                    throw new InvalidOperationException("Failed to create video generation task");
                }
                
                // Register the task for cancellation
                _taskRegistry.RegisterTask(taskId, cts);
                _logger.LogDebug("Registered task {TaskId} for cancellation", taskId);

                // Create task response
                // Note: Client will use ephemeral keys for SignalR authentication
                var taskResponse = new VideoGenerationTaskResponse
                {
                    TaskId = taskId,
                    Status = TaskStateConstants.Pending,
                    CreatedAt = DateTimeOffset.UtcNow,
                    EstimatedCompletionTime = DateTimeOffset.UtcNow.AddSeconds(60), // Default estimate
                    CheckStatusUrl = $"/v1/videos/generations/tasks/{taskId}"
                    // SignalRToken removed - clients will use ephemeral keys
                };

                return Accepted(taskResponse);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid async video generation request");
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Request",
                    Detail = ex.Message
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized async video generation attempt");
                return StatusCode(403, new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = ex.Message
                });
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning(ex, "Unsupported model or feature for async generation");
                return BadRequest(new ProblemDetails
                {
                    Title = "Not Supported",
                    Detail = ex.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting async video generation");

                // Extract useful error information for debugging while avoiding sensitive data exposure
                var errorDetail = ExtractSafeErrorDetail(ex);

                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = errorDetail,
                    Extensions =
                    {
                        ["errorType"] = ex.GetType().Name,
                        ["traceId"] = HttpContext.TraceIdentifier
                    }
                });
            }
        }

        /// <summary>
        /// Gets the status of a video generation task.
        /// </summary>
        /// <param name="taskId">The task ID returned from the async generation endpoint.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Current status of the video generation task.</returns>
        /// <response code="200">Task status retrieved successfully.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="404">Task not found or access denied.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("generations/tasks/{taskId}")]
        [ProducesResponseType(typeof(VideoGenerationTaskStatus), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> GetTaskStatus(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get virtual key ID from claims (set by VirtualKeyAuthenticationMiddleware)
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
                if (taskStatus == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // TODO: Consolidate security validation with ImagesController
                // Video uses simple VirtualKeyId comparison, Images uses hash-based validation
                // Both approaches are secure but inconsistent - should standardize on one approach
                // Validate task ownership for security
                if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
                {
                    // Return 404 instead of 403 to prevent information disclosure
                    _logger.LogWarning("Virtual key {VirtualKeyId} attempted to access task {TaskId} owned by {OwnerKeyId}", 
                        virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // Map internal task status to API response
                var response = new VideoGenerationTaskStatus
                {
                    TaskId = taskId,
                    Status = TaskStateConstants.FromTaskState(taskStatus.State),
                    Progress = taskStatus.Progress,
                    CreatedAt = taskStatus.CreatedAt,
                    UpdatedAt = taskStatus.UpdatedAt,
                    CompletedAt = taskStatus.CompletedAt,
                    Error = taskStatus.Error,
                    Result = taskStatus.Result?.ToString()
                };

                // If completed, try to get the video response
                if (taskStatus.State == TaskState.Completed && !string.IsNullOrEmpty(taskStatus.Result?.ToString()))
                {
                    try
                    {
                        // Get virtual key string for the video service call
                        var virtualKey = HttpContext.Items["VirtualKey"]?.ToString();
                        if (!string.IsNullOrEmpty(virtualKey))
                        {
                            var videoResponse = await _videoService.GetVideoGenerationStatusAsync(
                                taskId,
                                virtualKey,
                                cancellationToken);
                            response.VideoResponse = videoResponse;
                        }
                    }
                    catch (NotImplementedException)
                    {
                        // Status tracking not yet implemented, just return basic status
                    }
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving task status for {TaskId}", taskId);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while retrieving task status"
                });
            }
        }

        /// <summary>
        /// Manually retries a failed video generation task.
        /// </summary>
        /// <param name="taskId">The task ID to retry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated task status.</returns>
        /// <response code="200">Task queued for retry.</response>
        /// <response code="400">Task cannot be retried (not failed or exceeded max retries).</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="404">Task not found or access denied.</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("generations/tasks/{taskId}/retry")]
        [ProducesResponseType(typeof(VideoGenerationTaskStatus), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> RetryTask(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get virtual key ID from claims (set by VirtualKeyAuthenticationMiddleware)
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                // Get current task status
                var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
                if (taskStatus == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // TODO: Consolidate security validation with ImagesController
                // Video uses simple VirtualKeyId comparison, Images uses hash-based validation
                // Both approaches are secure but inconsistent - should standardize on one approach
                // Validate task ownership for security
                if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
                {
                    // Return 404 instead of 403 to prevent information disclosure
                    _logger.LogWarning("Virtual key {VirtualKeyId} attempted to retry task {TaskId} owned by {OwnerKeyId}", 
                        virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // Validate task can be retried
                if (taskStatus.State != TaskState.Failed)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Invalid Task State",
                        Detail = $"Only failed tasks can be retried. Current state: {taskStatus.State}"
                    });
                }

                if (!taskStatus.IsRetryable)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Task Not Retryable",
                        Detail = "This task has been marked as non-retryable"
                    });
                }

                if (taskStatus.RetryCount >= taskStatus.MaxRetries)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Max Retries Exceeded",
                        Detail = $"Task has already been retried {taskStatus.RetryCount} times (max: {taskStatus.MaxRetries})"
                    });
                }

                // Reset task for retry
                await _taskService.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Pending,
                    error: $"Manual retry requested (attempt {taskStatus.RetryCount + 1}/{taskStatus.MaxRetries})",
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Manual retry requested for task {TaskId} by virtual key {VirtualKeyId}", 
                    taskId, virtualKeyId);

                // Return updated status
                var updatedStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
                var response = new VideoGenerationTaskStatus
                {
                    TaskId = taskId,
                    Status = updatedStatus != null ? TaskStateConstants.FromTaskState(updatedStatus.State) : TaskStateConstants.Pending,
                    Progress = updatedStatus?.Progress ?? 0,
                    CreatedAt = updatedStatus?.CreatedAt ?? DateTimeOffset.UtcNow,
                    UpdatedAt = updatedStatus?.UpdatedAt ?? DateTimeOffset.UtcNow,
                    Error = $"Retry {updatedStatus?.RetryCount ?? 0}/{updatedStatus?.MaxRetries ?? 3} scheduled"
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrying task {TaskId}", taskId);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while retrying the task"
                });
            }
        }

        /// <summary>
        /// Cancels a video generation task.
        /// </summary>
        /// <param name="taskId">The task ID to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Cancellation result.</returns>
        /// <response code="204">Task cancelled successfully.</response>
        /// <response code="401">Authentication failed.</response>
        /// <response code="404">Task not found or access denied.</response>
        /// <response code="409">Task cannot be cancelled (already completed or failed).</response>
        /// <response code="500">Internal server error.</response>
        [HttpDelete("generations/{taskId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(ProblemDetails), 401)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> CancelTask(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Get virtual key ID from claims (set by VirtualKeyAuthenticationMiddleware)
                var virtualKeyIdClaim = HttpContext.User.FindFirst("VirtualKeyId")?.Value;
                if (string.IsNullOrEmpty(virtualKeyIdClaim) || !int.TryParse(virtualKeyIdClaim, out int virtualKeyId))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
                }

                // Check if task exists
                var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
                if (taskStatus == null)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // TODO: Consolidate security validation with ImagesController
                // Video uses simple VirtualKeyId comparison, Images uses hash-based validation
                // Both approaches are secure but inconsistent - should standardize on one approach
                // Validate task ownership for security
                if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
                {
                    // Return 404 instead of 403 to prevent information disclosure
                    _logger.LogWarning("Virtual key {VirtualKeyId} attempted to cancel task {TaskId} owned by {OwnerKeyId}", 
                        virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                    return NotFound(new ProblemDetails
                    {
                        Title = "Task Not Found",
                        Detail = "The requested task was not found"
                    });
                }

                // Check if task can be cancelled
                if (taskStatus.State == TaskState.Completed || taskStatus.State == TaskState.Failed)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Cannot Cancel Task",
                        Detail = $"Task is already {taskStatus.State.ToString().ToLowerInvariant()} and cannot be cancelled"
                    });
                }

                // Try to cancel via the registry first
                var registryCancelled = _taskRegistry.TryCancel(taskId);
                if (registryCancelled)
                {
                    _logger.LogInformation("Cancelled task {TaskId} via registry", taskId);
                }
                
                // Also notify the video service
                var virtualKey = HttpContext.Items["VirtualKey"]?.ToString();
                var cancelled = await _videoService.CancelVideoGenerationAsync(
                    taskId,
                    virtualKey ?? string.Empty,
                    cancellationToken);

                if (cancelled || registryCancelled)
                {
                    // Update task status to cancelled
                    await _taskService.CancelTaskAsync(taskId, cancellationToken);
                    return NoContent();
                }
                else
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Cancellation Failed",
                        Detail = "Unable to cancel the video generation task"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling task {TaskId}", taskId);
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while cancelling the task"
                });
            }
        }

        /// <summary>
        /// Stores video request parameters in HttpContext.Items for usage tracking and pricing.
        /// Extracts standard parameters (size, duration, fps) and builds pricing parameters
        /// from ExtensionData for rules-based pricing evaluation.
        /// </summary>
        private void StoreVideoRequestParameters(VideoGenerationRequest request)
        {
            // Store standard request parameters
            HttpContext.Items[HttpContextKeys.VideoRequestModel] = request.Model;
            HttpContext.Items[HttpContextKeys.VideoRequestN] = request.N;

            if (!string.IsNullOrEmpty(request.Size))
            {
                HttpContext.Items[HttpContextKeys.VideoRequestSize] = request.Size;
            }

            if (request.Duration.HasValue)
            {
                HttpContext.Items[HttpContextKeys.VideoRequestDuration] = request.Duration.Value;
            }

            if (request.Fps.HasValue)
            {
                HttpContext.Items[HttpContextKeys.VideoRequestFps] = request.Fps.Value;
            }

            if (!string.IsNullOrEmpty(request.Style))
            {
                HttpContext.Items[HttpContextKeys.VideoRequestStyle] = request.Style;
            }

            // Build pricing parameters dictionary for rules-based pricing
            var pricingParameters = new Dictionary<string, object>();

            // Add resolution (normalized to common format like "1080p")
            if (!string.IsNullOrEmpty(request.Size))
            {
                pricingParameters["resolution"] = NormalizeResolution(request.Size);
            }

            // Add duration if specified
            if (request.Duration.HasValue)
            {
                pricingParameters["duration"] = request.Duration.Value;
            }

            // Add FPS if specified
            if (request.Fps.HasValue)
            {
                pricingParameters["fps"] = request.Fps.Value;
            }

            // Add style if specified
            if (!string.IsNullOrEmpty(request.Style))
            {
                pricingParameters["style"] = request.Style;
            }

            // Extract additional pricing parameters from ExtensionData
            if (request.ExtensionData != null)
            {
                // Common pricing-relevant parameters from various video providers
                ExtractExtensionParameter(request.ExtensionData, "with_audio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "audio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "aspect_ratio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "quality", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "num_inference_steps", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "guidance_scale", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "motion_bucket_id", pricingParameters);
            }

            HttpContext.Items[HttpContextKeys.VideoRequestPricingParameters] = pricingParameters;

            _logger.LogDebug(
                "Stored video request parameters: Model={Model}, Size={Size}, Duration={Duration}, N={N}, PricingParams={PricingParamsCount}",
                request.Model, request.Size, request.Duration, request.N, pricingParameters.Count);
        }

        /// <summary>
        /// Extracts a parameter from ExtensionData and adds it to the pricing parameters dictionary.
        /// </summary>
        private static void ExtractExtensionParameter(
            Dictionary<string, JsonElement> extensionData,
            string parameterName,
            Dictionary<string, object> pricingParameters)
        {
            if (!extensionData.TryGetValue(parameterName, out var element))
                return;

            object? value = element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when element.TryGetInt32(out var intVal) => intVal,
                JsonValueKind.Number when element.TryGetDouble(out var dblVal) => dblVal,
                JsonValueKind.String => element.GetString(),
                _ => null
            };

            if (value != null)
            {
                pricingParameters[parameterName] = value;
            }
        }

        /// <summary>
        /// Extracts a safe error message from an exception for API responses.
        /// Avoids exposing internal details while providing useful debugging information.
        /// </summary>
        private static string ExtractSafeErrorDetail(Exception ex)
        {
            // For database errors, provide a cleaner message
            if (ex.GetType().Name.Contains("DbUpdateException") ||
                ex.GetType().Name.Contains("DbException"))
            {
                return "A database error occurred while processing the request. Please try again or contact support if the issue persists.";
            }

            // For provider-related errors, include more detail
            if (ex is HttpRequestException || ex.GetType().Name.Contains("Provider"))
            {
                return $"Failed to communicate with the video generation provider: {ex.Message}";
            }

            // For InvalidOperationException, the message is usually safe and informative
            if (ex is InvalidOperationException)
            {
                return ex.Message;
            }

            // For other errors, check if it's an internal implementation detail
            var message = ex.Message;

            // Avoid exposing stack traces or internal type names
            if (message.Contains("at ") || message.Contains("Exception:") ||
                message.Contains("System.") || message.Contains("Microsoft."))
            {
                return "An internal error occurred while processing the video generation request. Please try again.";
            }

            // Return the message if it seems safe
            return !string.IsNullOrEmpty(message) && message.Length < 500
                ? message
                : "An error occurred while starting video generation. Please try again.";
        }

        /// <summary>
        /// Normalizes video resolution to standard format (e.g., "1920x1080" → "1080p").
        /// </summary>
        private static string NormalizeResolution(string resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return resolution;

            // Already normalized format
            if (resolution.EndsWith("p", StringComparison.OrdinalIgnoreCase))
                return resolution.ToLowerInvariant();

            // Parse "WIDTHxHEIGHT" format
            var parts = resolution.ToLowerInvariant().Split('x');
            if (parts.Length == 2 && int.TryParse(parts[1], out var height))
            {
                return height switch
                {
                    >= 2160 => "4k",
                    >= 1080 => "1080p",
                    >= 720 => "720p",
                    >= 480 => "480p",
                    _ => $"{height}p"
                };
            }

            return resolution;
        }
    }

    /// <summary>
    /// Response for async video generation task creation.
    /// </summary>
    public class VideoGenerationTaskResponse
    {
        /// <summary>
        /// Unique identifier for the video generation task.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Current status of the task (pending, processing, completed, failed).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// When the task was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Estimated time when the video will be ready.
        /// </summary>
        public DateTimeOffset? EstimatedCompletionTime { get; set; }

        /// <summary>
        /// URL to check the status of this task.
        /// </summary>
        public string CheckStatusUrl { get; set; } = string.Empty;

        // SignalRToken removed - clients will use ephemeral keys for SignalR authentication
    }

    /// <summary>
    /// Status information for a video generation task.
    /// </summary>
    public class VideoGenerationTaskStatus
    {
        /// <summary>
        /// Unique identifier for the task.
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Current status (pending, running, completed, failed, cancelled).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        public int? Progress { get; set; }

        /// <summary>
        /// When the task was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the task was last updated.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the task completed (if applicable).
        /// </summary>
        public DateTimeOffset? CompletedAt { get; set; }

        /// <summary>
        /// Error message if the task failed.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Result data (internal use).
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// The video generation response if completed.
        /// </summary>
        public VideoGenerationResponse? VideoResponse { get; set; }
    }
}
