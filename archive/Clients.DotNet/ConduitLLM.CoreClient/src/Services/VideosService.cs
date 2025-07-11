using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.CoreClient.Client;
using ConduitLLM.CoreClient.Constants;
using ConduitLLM.CoreClient.Exceptions;
using ConduitLLM.CoreClient.Models;
using ConduitLLM.CoreClient.Utils;

namespace ConduitLLM.CoreClient.Services
{
    /// <summary>
    /// Service for video generation operations using the Conduit Core API.
    /// </summary>
    public class VideosService
    {
        private readonly BaseClient _client;
        private readonly ILogger<VideosService>? _logger;
        // Note: Only async video generation is supported
        private const string AsyncGenerationsEndpoint = ApiEndpoints.V1.Videos.AsyncGenerations;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideosService"/> class.
        /// </summary>
        /// <param name="client">The base client for making API requests.</param>
        /// <param name="logger">Optional logger for debugging and monitoring.</param>
        public VideosService(BaseClient client, ILogger<VideosService>? logger = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger;
        }

        // Note: Synchronous video generation is not supported by the Core API.
        // Only asynchronous video generation is available.
        // Use GenerateAsync(AsyncVideoGenerationRequest) instead.

        /// <summary>
        /// Generates videos asynchronously from a text prompt.
        /// </summary>
        /// <param name="request">The async video generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The async task information.</returns>
        /// <exception cref="ValidationException">Thrown when the request is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task<AsyncVideoGenerationResponse> GenerateAsync(
            AsyncVideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ValidateAsyncGenerationRequest(request);
                
                _logger?.LogDebug("Starting async generation of {Count} video(s) with model {Model} for prompt: {Prompt}", 
                    request.N, request.Model ?? VideoModels.Default, request.Prompt);

                // Convert to API request format
                var apiRequest = ConvertToAsyncApiRequest(request);

                var response = await _client.PostForServiceAsync<AsyncVideoGenerationResponse>(
                    AsyncGenerationsEndpoint,
                    apiRequest,
                    cancellationToken);

                _logger?.LogDebug("Async video generation task created with ID: {TaskId}", response.TaskId);
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the status of an async video generation task.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The current task status and result if completed.</returns>
        /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task<AsyncVideoGenerationResponse> GetTaskStatusAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskId))
                    throw new ValidationException("Task ID is required", "taskId");

                var endpoint = $"/v1/videos/generations/tasks/{Uri.EscapeDataString(taskId)}";
                
                var response = await _client.GetForServiceAsync<AsyncVideoGenerationResponse>(
                    endpoint,
                    cancellationToken);

                _logger?.LogDebug("Retrieved status for task {TaskId}: {Status} ({Progress}%)", 
                    taskId, response.Status, response.Progress);
                
                return response;
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Cancels a pending or running async video generation task.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <exception cref="ValidationException">Thrown when the task ID is invalid.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails.</exception>
        public async Task CancelTaskAsync(
            string taskId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(taskId))
                    throw new ValidationException("Task ID is required", "taskId");

                var endpoint = $"/v1/videos/generations/{Uri.EscapeDataString(taskId)}";
                
                await _client.HttpClientForServices.DeleteAsync(endpoint, cancellationToken);
                
                _logger?.LogDebug("Cancelled task {TaskId}", taskId);
            }
            catch (Exception ex) when (!(ex is ConduitCoreException))
            {
                ErrorHandler.HandleException(ex);
                throw;
            }
        }

        /// <summary>
        /// Polls an async video generation task until completion or timeout.
        /// </summary>
        /// <param name="taskId">The task identifier.</param>
        /// <param name="options">Polling options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The final task result when completed.</returns>
        /// <exception cref="ValidationException">Thrown when parameters are invalid.</exception>
        /// <exception cref="TimeoutException">Thrown when polling times out.</exception>
        /// <exception cref="ConduitCoreException">Thrown when the API request fails or task fails.</exception>
        public async Task<VideoGenerationResponse> PollTaskUntilCompletionAsync(
            string taskId,
            VideoTaskPollingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new VideoTaskPollingOptions();
            
            if (string.IsNullOrWhiteSpace(taskId))
                throw new ValidationException("Task ID is required", "taskId");

            var startTime = DateTime.UtcNow;
            var currentInterval = options.IntervalMs;
            
            _logger?.LogDebug("Starting to poll task {TaskId} with interval {IntervalMs}ms, timeout {TimeoutMs}ms", 
                taskId, options.IntervalMs, options.TimeoutMs);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check timeout
                if ((DateTime.UtcNow - startTime).TotalMilliseconds > options.TimeoutMs)
                {
                    throw new TimeoutException($"Task polling timed out after {options.TimeoutMs}ms");
                }

                var status = await GetTaskStatusAsync(taskId, cancellationToken);

                switch (status.Status)
                {
                    case VideoTaskStatus.Completed:
                        if (status.Result == null)
                            throw new ConduitCoreException("Task completed but no result was provided", null, null, null, null);
                        
                        _logger?.LogDebug("Task {TaskId} completed successfully", taskId);
                        return status.Result;

                    case VideoTaskStatus.Failed:
                        throw new ConduitCoreException($"Task failed: {status.Error ?? "Unknown error"}", null, null, null, null);

                    case VideoTaskStatus.Cancelled:
                        throw new ConduitCoreException("Task was cancelled", null, null, null, null);

                    case VideoTaskStatus.TimedOut:
                        throw new ConduitCoreException("Task timed out", null, null, null, null);

                    case VideoTaskStatus.Pending:
                    case VideoTaskStatus.Running:
                        // Continue polling
                        break;

                    default:
                        throw new ConduitCoreException($"Unknown task status: {status.Status}", null, null, null, null);
                }

                // Wait before next poll
                await Task.Delay(currentInterval, cancellationToken);

                // Apply exponential backoff if enabled
                if (options.UseExponentialBackoff)
                {
                    currentInterval = Math.Min(currentInterval * 2, options.MaxIntervalMs);
                }
            }
        }

        /// <summary>
        /// Gets the capabilities of a video model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>The model capabilities.</returns>
        public VideoModelCapabilities GetModelCapabilities(string model)
        {
            return VideoModelCapabilities.GetCapabilities(model);
        }

        private static void ValidateGenerationRequest(VideoGenerationRequest request)
        {
            if (request == null)
                throw new ValidationException("Request cannot be null", "request");

            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ValidationException("Prompt is required", "prompt");

            if (request.N <= 0 || request.N > 10)
                throw new ValidationException("Number of videos must be between 1 and 10", "n");

            if (request.Duration.HasValue && (request.Duration <= 0 || request.Duration > 60))
                throw new ValidationException("Duration must be between 1 and 60 seconds", "duration");

            if (request.Fps.HasValue && (request.Fps <= 0 || request.Fps > 120))
                throw new ValidationException("FPS must be between 1 and 120", "fps");

            if (!string.IsNullOrEmpty(request.ResponseFormat) && 
                request.ResponseFormat != VideoResponseFormats.Url && 
                request.ResponseFormat != VideoResponseFormats.Base64Json)
            {
                throw new ValidationException($"Response format must be '{VideoResponseFormats.Url}' or '{VideoResponseFormats.Base64Json}'", "responseFormat");
            }
        }

        private static void ValidateAsyncGenerationRequest(AsyncVideoGenerationRequest request)
        {
            // First validate the base video generation request
            ValidateGenerationRequest(request);

            // Additional validation for async-specific fields
            if (request.TimeoutSeconds.HasValue && (request.TimeoutSeconds <= 0 || request.TimeoutSeconds > 3600))
                throw new ValidationException("Timeout must be between 1 and 3600 seconds", "timeoutSeconds");

            if (!string.IsNullOrEmpty(request.WebhookUrl))
            {
                if (!Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    throw new ValidationException("WebhookUrl must be a valid HTTP or HTTPS URL", "webhookUrl");
                }
            }
        }

        private static object ConvertToApiRequest(VideoGenerationRequest request)
        {
            return new
            {
                prompt = request.Prompt,
                model = request.Model ?? VideoModels.Default,
                duration = request.Duration,
                size = request.Size,
                fps = request.Fps,
                style = request.Style,
                response_format = request.ResponseFormat ?? VideoResponseFormats.Url,
                user = request.User,
                seed = request.Seed,
                n = request.N
            };
        }

        private static object ConvertToAsyncApiRequest(AsyncVideoGenerationRequest request)
        {
            var baseRequest = ConvertToApiRequest(request);
            
            // Convert to dynamic object to add async-specific properties
            var asyncRequest = new
            {
                prompt = request.Prompt,
                model = request.Model ?? VideoModels.Default,
                duration = request.Duration,
                size = request.Size,
                fps = request.Fps,
                style = request.Style,
                response_format = request.ResponseFormat ?? VideoResponseFormats.Url,
                user = request.User,
                seed = request.Seed,
                n = request.N,
                webhook_url = request.WebhookUrl,
                webhook_metadata = request.WebhookMetadata,
                webhook_headers = request.WebhookHeaders,
                timeout_seconds = request.TimeoutSeconds
            };

            return asyncRequest;
        }
    }

    /// <summary>
    /// Represents the capabilities of a video generation model.
    /// </summary>
    public class VideoModelCapabilities
    {
        /// <summary>
        /// Gets or sets the maximum duration in seconds.
        /// </summary>
        public int MaxDuration { get; set; }

        /// <summary>
        /// Gets or sets the supported resolutions.
        /// </summary>
        public string[] SupportedResolutions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the supported FPS values.
        /// </summary>
        public int[] SupportedFps { get; set; } = Array.Empty<int>();

        /// <summary>
        /// Gets or sets whether the model supports custom styles.
        /// </summary>
        public bool SupportsCustomStyles { get; set; }

        /// <summary>
        /// Gets or sets whether the model supports seed-based generation.
        /// </summary>
        public bool SupportsSeed { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of videos that can be generated in one request.
        /// </summary>
        public int MaxVideos { get; set; }

        /// <summary>
        /// Gets the capabilities for a specific model.
        /// </summary>
        /// <param name="model">The model name.</param>
        /// <returns>The model capabilities.</returns>
        public static VideoModelCapabilities GetCapabilities(string model)
        {
            return model?.ToLowerInvariant() switch
            {
                "minimax-video" or "minimax-video-01" => new VideoModelCapabilities
                {
                    MaxDuration = 6,
                    SupportedResolutions = new[] 
                    { 
                        VideoResolutions.HD, 
                        VideoResolutions.FullHD, 
                        VideoResolutions.VerticalHD, 
                        VideoResolutions.VerticalFullHD,
                        "720x480"
                    },
                    SupportedFps = new[] { 24, 30 },
                    SupportsCustomStyles = true,
                    SupportsSeed = true,
                    MaxVideos = 1
                },
                _ => new VideoModelCapabilities
                {
                    MaxDuration = 60,
                    SupportedResolutions = new[] 
                    { 
                        VideoResolutions.HD, 
                        VideoResolutions.FullHD, 
                        VideoResolutions.Square 
                    },
                    SupportedFps = new[] { 24, 30, 60 },
                    SupportsCustomStyles = true,
                    SupportsSeed = true,
                    MaxVideos = 10
                }
            };
        }
    }
}