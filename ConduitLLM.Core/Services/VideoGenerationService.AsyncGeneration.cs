using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Partial class containing asynchronous video generation functionality with task management.
    /// </summary>
    public partial class VideoGenerationService
    {
        /// <inheritdoc/>
        public async Task<VideoGenerationResponse> GenerateVideoWithTaskAsync(
            VideoGenerationRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting asynchronous video generation task for model {Model}", request.Model);

            // Validate the request
            if (!await ValidateRequestAsync(request, cancellationToken))
            {
                throw new ArgumentException("Invalid video generation request");
            }

            _logger.LogInformation("Request validated successfully");

            // Validate virtual key
            var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, request.Model);
            if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Invalid or disabled virtual key");
            }

            _logger.LogInformation("Virtual key validated: {VirtualKeyId}", virtualKeyInfo.Id);

            // Create task metadata
            var taskMetadata = new TaskMetadata(virtualKeyInfo.Id)
            {
                Model = request.Model,
                Prompt = request.Prompt,
                ExtensionData = new Dictionary<string, object>
                {
                    ["VirtualKey"] = virtualKey,
                    ["Request"] = request
                }
            };

            _logger.LogInformation("About to create async task");

            // Create async task using new overload with explicit virtualKeyId
            var taskId = await _taskService.CreateTaskAsync("video_generation", virtualKeyInfo.Id, taskMetadata, cancellationToken);

            _logger.LogInformation("Created task {TaskId}, now publishing VideoGenerationRequested event", taskId);

            // Publish VideoGenerationRequested event for async processing
            await PublishEventAsync(
                new VideoGenerationRequested
                {
                    RequestId = taskId,
                    Model = request.Model,
                    Prompt = request.Prompt,
                    VirtualKeyId = virtualKeyInfo.Id.ToString(),
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
                        ResponseFormat = request.ResponseFormat
                    }
                },
                "async video generation request",
                new { Model = request.Model, TaskId = taskId });

            // Return response with task ID
            // Since the existing VideoGenerationResponse doesn't have TaskId/Status fields,
            // we'll need to return a standard response with a video data entry containing the task info
            return new VideoGenerationResponse
            {
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Data = new List<VideoData>
                {
                    new VideoData
                    {
                        Url = $"pending:{taskId}", // Encode task ID in URL for now
                        Metadata = new VideoMetadata
                        {
                            Width = 0,
                            Height = 0,
                            Duration = 0,
                            Fps = 0,
                            FileSizeBytes = 0,
                            MimeType = "application/json"
                        }
                    }
                },
                Model = request.Model
            };
        }

        /// <inheritdoc/>
        public async Task<VideoGenerationResponse> GetVideoGenerationStatusAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Checking status for video generation task {TaskId}", taskId);

            // Get task status from the async task service
            var taskStatus = await _taskService.GetTaskStatusAsync(taskId, cancellationToken);
            
            if (taskStatus == null)
            {
                _logger.LogWarning("Task {TaskId} not found", taskId);
                throw new InvalidOperationException($"Task {taskId} not found");
            }

            // Check if the task belongs to the provided virtual key
            if (taskStatus.Metadata != null)
            {
                // Direct access to virtual key ID from typed metadata
                // Note: This validation is limited without access to the actual virtual key ID
                // In production, you'd want to validate against the actual virtual key ID stored in metadata
                _logger.LogDebug("Task {TaskId} has VirtualKeyId: {VirtualKeyId}", taskId, taskStatus.Metadata.VirtualKeyId);
            }
            else
            {
                _logger.LogDebug("Task {TaskId} has no metadata", taskId);
            }

            // Handle different task states
            switch (taskStatus.State)
            {
                case TaskState.Pending:
                case TaskState.Processing:
                    // Return a response indicating the task is still in progress
                    return new VideoGenerationResponse
                    {
                        Created = ((DateTimeOffset)taskStatus.CreatedAt).ToUnixTimeSeconds(),
                        Data = new List<VideoData>
                        {
                            new VideoData
                            {
                                Url = $"pending:{taskId}",
                                Metadata = new VideoMetadata
                                {
                                    Width = 0,
                                    Height = 0,
                                    Duration = 0,
                                    Fps = 0,
                                    FileSizeBytes = 0,
                                    MimeType = "application/json"
                                },
                                RevisedPrompt = taskStatus.ProgressMessage ?? $"Video generation in progress... {taskStatus.Progress}%"
                            }
                        },
                        Model = taskStatus.TaskType == "video_generation" ? "unknown" : taskStatus.TaskType,
                        Usage = new VideoGenerationUsage
                        {
                            VideosGenerated = 0,
                            TotalDurationSeconds = 0
                        }
                    };

                case TaskState.Completed:
                    // Deserialize the result to VideoGenerationResponse
                    if (taskStatus.Result == null)
                    {
                        throw new InvalidOperationException($"Completed task {taskId} has no result");
                    }

                    try
                    {
                        // If the result is already a VideoGenerationResponse, return it directly
                        if (taskStatus.Result is VideoGenerationResponse response)
                        {
                            return response;
                        }

                        // If the result is a JsonElement, deserialize it
                        if (taskStatus.Result is System.Text.Json.JsonElement resultJson)
                        {
                            var videoResponse = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationResponse>(
                                resultJson.GetRawText(),
                                new System.Text.Json.JsonSerializerOptions 
                                { 
                                    PropertyNameCaseInsensitive = true 
                                });
                            
                            if (videoResponse == null)
                            {
                                throw new InvalidOperationException($"Failed to deserialize task {taskId} result");
                            }

                            return videoResponse;
                        }

                        // Try to convert the result to a VideoGenerationResponse
                        var resultString = System.Text.Json.JsonSerializer.Serialize(taskStatus.Result);
                        var deserializedResponse = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationResponse>(
                            resultString,
                            new System.Text.Json.JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            });

                        if (deserializedResponse == null)
                        {
                            throw new InvalidOperationException($"Failed to deserialize task {taskId} result");
                        }

                        return deserializedResponse;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize video generation result for task {TaskId}", taskId);
                        throw new InvalidOperationException($"Failed to process completed task {taskId} result", ex);
                    }

                case TaskState.Failed:
                    // Return an error response or throw an exception
                    var errorMessage = taskStatus.Error ?? "Video generation failed";
                    _logger.LogError("Video generation task {TaskId} failed: {Error}", taskId, errorMessage);
                    throw new InvalidOperationException($"Video generation failed: {errorMessage}");

                case TaskState.Cancelled:
                    _logger.LogInformation("Video generation task {TaskId} was cancelled", taskId);
                    throw new OperationCanceledException($"Video generation task {taskId} was cancelled");

                case TaskState.TimedOut:
                    _logger.LogError("Video generation task {TaskId} timed out", taskId);
                    throw new TimeoutException($"Video generation task {taskId} timed out");

                default:
                    _logger.LogError("Unknown task state {State} for task {TaskId}", taskStatus.State, taskId);
                    throw new InvalidOperationException($"Unknown task state: {taskStatus.State}");
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CancelVideoGenerationAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cancelling video generation task {TaskId}", taskId);

            var cancelled = false;

            // Try to cancel via the task registry if available
            if (_taskRegistry != null)
            {
                cancelled = _taskRegistry.TryCancel(taskId);
                if (cancelled)
                {
                    _logger.LogInformation("Successfully requested cancellation for task {TaskId} via registry", taskId);
                }
                else
                {
                    _logger.LogWarning("Task {TaskId} not found in cancellable task registry", taskId);
                }
            }

            // Update the task status to cancelled
            try
            {
                await _taskService.UpdateTaskStatusAsync(
                    taskId,
                    TaskState.Cancelled,
                    error: "User requested cancellation",
                    cancellationToken: cancellationToken);
                
                cancelled = true;
                _logger.LogInformation("Updated task {TaskId} status to cancelled", taskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update task {TaskId} status to cancelled", taskId);
            }

            // Publish VideoGenerationCancelled event for distributed systems
            await PublishEventAsync(
                new VideoGenerationCancelled
                {
                    RequestId = taskId,
                    CancelledAt = DateTime.UtcNow,
                    CorrelationId = taskId,
                    Reason = "User requested cancellation"
                },
                "video generation cancellation",
                new { TaskId = taskId });

            return cancelled;
        }
    }
}