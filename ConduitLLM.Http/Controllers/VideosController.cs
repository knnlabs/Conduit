using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Controller for video generation operations following OpenAI-compatible patterns.
    /// </summary>
    [ApiController]
    [Route("v1/videos")]
    [Authorize]
    [EnableRateLimiting("VirtualKeyPolicy")]
    public class VideosController : ControllerBase
    {
        private readonly IVideoGenerationService _videoService;
        private readonly IAsyncTaskService _taskService;
        private readonly IOperationTimeoutProvider _timeoutProvider;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly ILogger<VideosController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideosController"/> class.
        /// </summary>
        public VideosController(
            IVideoGenerationService videoService,
            IAsyncTaskService taskService,
            IOperationTimeoutProvider timeoutProvider,
            ICancellableTaskRegistry taskRegistry,
            ILogger<VideosController> logger)
        {
            _videoService = videoService ?? throw new ArgumentNullException(nameof(videoService));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _timeoutProvider = timeoutProvider ?? throw new ArgumentNullException(nameof(timeoutProvider));
            _taskRegistry = taskRegistry ?? throw new ArgumentNullException(nameof(taskRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

                // Get virtual key from HttpContext.Items (set by VirtualKeyAuthenticationMiddleware)
                var virtualKey = HttpContext.Items["VirtualKey"] as string;
                if (string.IsNullOrEmpty(virtualKey))
                {
                    return Unauthorized(new ProblemDetails
                    {
                        Title = "Unauthorized",
                        Detail = "Virtual key not found in request context"
                    });
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
                var taskResponse = new VideoGenerationTaskResponse
                {
                    TaskId = taskId,
                    Status = "pending",
                    CreatedAt = DateTimeOffset.UtcNow,
                    EstimatedCompletionTime = DateTimeOffset.UtcNow.AddSeconds(60), // Default estimate
                    CheckStatusUrl = $"/v1/videos/generations/tasks/{taskId}"
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
                return StatusCode(500, new ProblemDetails
                {
                    Title = "Internal Server Error",
                    Detail = "An error occurred while starting video generation"
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
        /// <response code="404">Task not found.</response>
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
                // Get virtual key from HttpContext.Items (set by VirtualKeyAuthenticationMiddleware)
                var virtualKey = HttpContext.Items["VirtualKey"] as string;
                if (string.IsNullOrEmpty(virtualKey))
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
                        Detail = $"No task found with ID: {taskId}"
                    });
                }

                // Map internal task status to API response
                var response = new VideoGenerationTaskStatus
                {
                    TaskId = taskId,
                    Status = taskStatus.State.ToString().ToLowerInvariant(),
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
                        var videoResponse = await _videoService.GetVideoGenerationStatusAsync(
                            taskId,
                            virtualKey,
                            cancellationToken);
                        response.VideoResponse = videoResponse;
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
        /// <response code="404">Task not found.</response>
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
                // Get virtual key from HttpContext.Items (set by VirtualKeyAuthenticationMiddleware)
                var virtualKey = HttpContext.Items["VirtualKey"] as string;
                if (string.IsNullOrEmpty(virtualKey))
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
                        Detail = $"No task found with ID: {taskId}"
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

                _logger.LogInformation("Manual retry requested for task {TaskId} by virtual key {VirtualKey}", 
                    taskId, virtualKey);

                // Return updated status
                var updatedStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
                var response = new VideoGenerationTaskStatus
                {
                    TaskId = taskId,
                    Status = updatedStatus?.State.ToString().ToLowerInvariant() ?? "pending",
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
        /// <response code="404">Task not found.</response>
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
                // Get virtual key from HttpContext.Items (set by VirtualKeyAuthenticationMiddleware)
                var virtualKey = HttpContext.Items["VirtualKey"] as string;
                if (string.IsNullOrEmpty(virtualKey))
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
                        Detail = $"No task found with ID: {taskId}"
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
                var cancelled = await _videoService.CancelVideoGenerationAsync(
                    taskId,
                    virtualKey,
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