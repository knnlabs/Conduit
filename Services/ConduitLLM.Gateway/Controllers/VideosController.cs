using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Gateway.Authorization;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Filters;
using ConduitLLM.Gateway.UsageTracking;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Events;
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
    [Tags("Videos")]
    [ServiceFilter(typeof(OperationLoggingFilter))]
    public class VideosController : GatewayControllerBase
    {
        private readonly IAsyncTaskService _taskService;
        private readonly IOperationTimeoutProvider _timeoutProvider;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideosController"/> class.
        /// </summary>
        public VideosController(
            IAsyncTaskService taskService,
            IOperationTimeoutProvider timeoutProvider,
            ICancellableTaskRegistry taskRegistry,
            ILogger<VideosController> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService,
            IEventBus eventBus)
            : base(eventBus, logger)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _timeoutProvider = timeoutProvider ?? throw new ArgumentNullException(nameof(timeoutProvider));
            _taskRegistry = taskRegistry ?? throw new ArgumentNullException(nameof(taskRegistry));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
        }

        /// <summary>
        /// Starts an asynchronous video generation task.
        /// </summary>
        [HttpPost("generations/async")]
        [ProducesResponseType(typeof(VideoGenerationTaskResponse), 202)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 400)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 401)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 403)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 429)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 500)]
        public async Task<IActionResult> GenerateVideoAsync(
            [FromBody][Required] VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            var virtualKey = CurrentVirtualKey;
            if (string.IsNullOrEmpty(virtualKey) || CurrentVirtualKeyId == null)
            {
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized");
            }
            var virtualKeyId = CurrentVirtualKeyId.Value;

            // Validate the request
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return OpenAIError(400, "Prompt is required", "missing_parameter");
            }
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return OpenAIError(400, "Model is required", "missing_parameter");
            }
            if (request.Duration.HasValue && (request.Duration.Value < 1 || request.Duration.Value > 60))
            {
                return OpenAIError(400, "Duration must be between 1 and 60 seconds", "invalid_value");
            }
            if (request.Fps.HasValue && (request.Fps.Value < 1 || request.Fps.Value > 120))
            {
                return OpenAIError(400, "FPS must be between 1 and 120", "invalid_value");
            }

            // Store video request parameters for usage tracking and pricing
            StoreVideoRequestParameters(request);
            var accounting = HttpContext.GetOrCreateRequestAccountingContext();
            accounting.SetOperation(RequestOperation.Video, virtualKeyId, request.Model);
            var submissionUsage = new Usage
            {
                VideoDurationSeconds = request.Duration,
                VideoResolution = request.Size,
                PricingParameters = (HttpContext.GetUsageContext() as VideoUsageContext)?.PricingParameters
            };
            accounting.RecordProviderUsage(
                submissionUsage,
                request.Model,
                UsageEvidenceSource.Estimated);

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
                Logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
            }

            // Create a linked cancellation token that can be controlled independently
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Build task metadata. The orchestrator reads ExtensionData["VirtualKey"] for re-validation
            // and ExtensionData["Request"] to reconstruct the original request when consuming the event.
            var taskMetadata = new TaskMetadata(virtualKeyId)
            {
                Model = request.Model,
                Prompt = request.Prompt,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = virtualKey,
                    ["Request"] = request
                }
            };

            var taskId = await _taskService.CreateTaskAsync("video_generation", virtualKeyId, taskMetadata, cts.Token);

            // Register the task for cancellation
            _taskRegistry.RegisterTask(taskId, cts);

            // Convert ExtensionData to provider options for the event payload
            Dictionary<string, object>? providerOptions = null;
            if (request.ExtensionData != null && request.ExtensionData.Count > 0)
            {
                providerOptions = new Dictionary<string, object>();
                foreach (var kvp in request.ExtensionData)
                {
                    providerOptions[kvp.Key] = kvp.Value.ToString();
                }
            }

            PublishEventFireAndForget(new VideoGenerationRequested
            {
                RequestId = taskId,
                Model = request.Model,
                Prompt = request.Prompt,
                VirtualKeyId = virtualKeyId.ToString(),
                IsAsync = true,
                RequestedAt = DateTime.UtcNow,
                CorrelationId = taskId,
                WebhookUrl = request.WebhookUrl,
                WebhookHeaders = request.WebhookHeaders,
                Parameters = new VideoGenerationParameters
                {
                    Size = request.Size,
                    Duration = request.Duration,
                    Fps = request.Fps,
                    Style = request.Style,
                    ResponseFormat = request.ResponseFormat,
                    ProviderOptions = providerOptions
                }
            }, "create async video generation", new { TaskId = taskId, Model = request.Model });

            var taskResponse = new VideoGenerationTaskResponse
            {
                TaskId = taskId,
                Status = TaskStateConstants.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                EstimatedCompletionTime = DateTimeOffset.UtcNow.AddSeconds(60),
                CheckStatusUrl = $"/v1/videos/generations/tasks/{taskId}"
            };
            accounting.RecordMetadata(JsonSerializer.Serialize(new
            {
                type = "video",
                taskId,
                status = TaskStateConstants.Pending,
                durationSeconds = request.Duration,
                resolution = request.Size,
                fps = request.Fps,
                style = request.Style
            }));

            return Accepted(taskResponse);
        }

        /// <summary>
        /// Gets the status of a video generation task.
        /// </summary>
        [HttpGet("generations/tasks/{taskId}")]
        [ProducesResponseType(typeof(VideoGenerationTaskStatus), 200)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 401)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 404)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 500)]
        public async Task<IActionResult> GetTaskStatus(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            if (CurrentVirtualKeyId == null)
            {
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized");
            }
            var virtualKeyId = CurrentVirtualKeyId.Value;

            var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                return OpenAIError(404, "The requested task was not found", "not_found");
            }

            // Validate task ownership for security
            if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
            {
                // Return 404 instead of 403 to prevent information disclosure
                Logger.LogWarning("Virtual key {VirtualKeyId} attempted to access task {TaskId} owned by {OwnerKeyId}",
                    virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                return OpenAIError(404, "The requested task was not found", "not_found");
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
                ResultRaw = taskStatus.Result?.ToString()
            };

            // If completed, deserialize the stored result into a typed VideoGenerationResponse.
            if (taskStatus.State == TaskState.Completed && taskStatus.Result != null)
            {
                response.Result = DeserializeVideoResult(taskStatus.Result, taskId);
            }

            return Ok(response);
        }

        /// <summary>
        /// Manually retries a failed video generation task.
        /// </summary>
        [HttpPost("generations/tasks/{taskId}/retry")]
        [ProducesResponseType(typeof(VideoGenerationTaskStatus), 200)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 400)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 401)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 404)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 500)]
        public async Task<IActionResult> RetryTask(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            if (CurrentVirtualKeyId == null)
            {
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized");
            }
            var virtualKeyId = CurrentVirtualKeyId.Value;

            var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                return OpenAIError(404, "The requested task was not found", "not_found");
            }

            // Validate task ownership for security
            if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
            {
                Logger.LogWarning("Virtual key {VirtualKeyId} attempted to retry task {TaskId} owned by {OwnerKeyId}",
                    virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                return OpenAIError(404, "The requested task was not found", "not_found");
            }

            // Validate task can be retried
            if (taskStatus.State != TaskState.Failed)
            {
                return OpenAIError(400, $"Only failed tasks can be retried. Current state: {taskStatus.State}", "invalid_operation");
            }

            if (!taskStatus.IsRetryable)
            {
                return OpenAIError(400, "This task has been marked as non-retryable", "invalid_operation");
            }

            if (taskStatus.RetryCount >= taskStatus.MaxRetries)
            {
                return OpenAIError(400, $"Task has already been retried {taskStatus.RetryCount} times (max: {taskStatus.MaxRetries})", "invalid_operation");
            }

            // Reset task for retry
            await _taskService.UpdateTaskStatusAsync(
                taskId,
                TaskState.Pending,
                error: $"Manual retry requested (attempt {taskStatus.RetryCount + 1}/{taskStatus.MaxRetries})",
                cancellationToken: cancellationToken);

            Logger.LogInformation("Manual retry requested for task {TaskId} by virtual key {VirtualKeyId}",
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

        /// <summary>
        /// Cancels a video generation task.
        /// </summary>
        [HttpDelete("generations/{taskId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 401)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 404)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 409)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), 500)]
        public async Task<IActionResult> CancelTask(
            [FromRoute][Required] string taskId,
            CancellationToken cancellationToken = default)
        {
            if (CurrentVirtualKeyId == null)
            {
                return OpenAIError(401, "Virtual key not found in request context", "unauthorized");
            }
            var virtualKeyId = CurrentVirtualKeyId.Value;

            var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
            if (taskStatus == null)
            {
                return OpenAIError(404, "The requested task was not found", "not_found");
            }

            // Validate task ownership for security
            if (taskStatus.Metadata?.VirtualKeyId != virtualKeyId)
            {
                Logger.LogWarning("Virtual key {VirtualKeyId} attempted to cancel task {TaskId} owned by {OwnerKeyId}",
                    virtualKeyId, taskId, taskStatus.Metadata?.VirtualKeyId);
                return OpenAIError(404, "The requested task was not found", "not_found");
            }

            // Check if task can be cancelled
            if (taskStatus.State == TaskState.Completed || taskStatus.State == TaskState.Failed)
            {
                return OpenAIError(409, $"Task is already {taskStatus.State.ToString().ToLowerInvariant()} and cannot be cancelled", "invalid_operation");
            }

            // Try to cancel via the registry first (signals any in-flight provider call)
            var registryCancelled = _taskRegistry.TryCancel(taskId);
            if (registryCancelled)
            {
                Logger.LogInformation("Cancelled task {TaskId} via registry", taskId);
            }

            // Mark the task as cancelled and notify consumers via event
            await _taskService.CancelTaskAsync(taskId, cancellationToken);

            PublishEventFireAndForget(new VideoGenerationCancelled
            {
                RequestId = taskId,
                CancelledAt = DateTime.UtcNow,
                CorrelationId = taskId,
                Reason = "User requested cancellation"
            }, "cancel video generation", new { TaskId = taskId });

            return NoContent();
        }

        /// <summary>
        /// Deserializes the stored task result into a typed <see cref="VideoGenerationResponse"/>.
        /// The result may already be a typed object (in-memory task store) or a JsonElement (Redis/DB).
        /// Returns null if deserialization fails — caller falls back to <c>ResultRaw</c>.
        /// </summary>
        private VideoGenerationResponse? DeserializeVideoResult(object result, string taskId)
        {
            try
            {
                if (result is VideoGenerationResponse typed)
                {
                    return typed;
                }
                var json = result is JsonElement element
                    ? element.GetRawText()
                    : JsonSerializer.Serialize(result);
                return JsonSerializer.Deserialize<VideoGenerationResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to deserialize video task result for {TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// Stores video request parameters in a typed <see cref="VideoUsageContext"/> for
        /// the usage tracking middleware (cost calculation, request logging, pricing rules).
        /// </summary>
        private void StoreVideoRequestParameters(VideoGenerationRequest request)
        {
            // Build pricing parameters dictionary for rules-based pricing
            var pricingParameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(request.Size))
            {
                pricingParameters["resolution"] = NormalizeResolution(request.Size);
            }
            if (request.Duration.HasValue)
            {
                pricingParameters["duration"] = request.Duration.Value;
            }
            if (request.Fps.HasValue)
            {
                pricingParameters["fps"] = request.Fps.Value;
            }
            if (!string.IsNullOrEmpty(request.Style))
            {
                pricingParameters["style"] = request.Style;
            }

            if (request.ExtensionData != null)
            {
                ExtractExtensionParameter(request.ExtensionData, "with_audio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "audio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "aspect_ratio", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "quality", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "num_inference_steps", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "guidance_scale", pricingParameters);
                ExtractExtensionParameter(request.ExtensionData, "motion_bucket_id", pricingParameters);
            }

            HttpContext.SetUsageContext(new VideoUsageContext
            {
                Model = request.Model,
                Size = string.IsNullOrEmpty(request.Size) ? null : request.Size,
                Duration = request.Duration,
                Fps = request.Fps,
                Style = string.IsNullOrEmpty(request.Style) ? null : request.Style,
                N = request.N,
                PricingParameters = pricingParameters.Count > 0 ? pricingParameters : null
            });

            Logger.LogDebug(
                "Stored video usage context: Model={Model}, Size={Size}, Duration={Duration}, N={N}, PricingParams={PricingParamsCount}",
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
        /// Normalizes video resolution to standard format (e.g., "1920x1080" → "1080p").
        /// </summary>
        private static string NormalizeResolution(string resolution)
        {
            if (string.IsNullOrEmpty(resolution))
                return resolution;

            if (resolution.EndsWith("p", StringComparison.OrdinalIgnoreCase))
                return resolution.ToLowerInvariant();

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
        public string TaskId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? EstimatedCompletionTime { get; set; }
        public string CheckStatusUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Status information for a video generation task.
    /// </summary>
    public class VideoGenerationTaskStatus
    {
        public string TaskId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? Progress { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? Error { get; set; }
        public string? ResultRaw { get; set; }
        public VideoGenerationResponse? Result { get; set; }
    }
}
