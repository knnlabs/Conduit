using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Events;
using MassTransit;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Base implementation of the video generation service.
    /// Coordinates video generation across different providers and handles orchestration.
    /// </summary>
    public class VideoGenerationService : EventPublishingServiceBase, IVideoGenerationService
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IModelCapabilityService _capabilityService;
        private readonly ICostCalculationService _costService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IMediaStorageService _mediaStorage;
        private readonly IAsyncTaskService _taskService;
        private readonly ILogger<VideoGenerationService> _logger;

        public VideoGenerationService(
            ILLMClientFactory clientFactory,
            IModelCapabilityService capabilityService,
            ICostCalculationService costService,
            IVirtualKeyService virtualKeyService,
            IMediaStorageService mediaStorage,
            IAsyncTaskService taskService,
            ILogger<VideoGenerationService> logger,
            IPublishEndpoint? publishEndpoint = null)
            : base(publishEndpoint, logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _capabilityService = capabilityService ?? throw new ArgumentNullException(nameof(capabilityService));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _mediaStorage = mediaStorage ?? throw new ArgumentNullException(nameof(mediaStorage));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<VideoGenerationResponse> GenerateVideoAsync(
            VideoGenerationRequest request,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting synchronous video generation for model {Model}", request.Model);

            // Validate the request
            if (!await ValidateRequestAsync(request, cancellationToken))
            {
                throw new ArgumentException("Invalid video generation request");
            }

            // Validate virtual key
            var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, request.Model);
            if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Invalid or disabled virtual key");
            }

            // Get the appropriate client for the model
            var client = _clientFactory.GetClient(request.Model);
            if (client == null)
            {
                throw new NotSupportedException($"No provider available for model {request.Model}");
            }

            // Publish VideoGenerationRequested event
            var requestId = Guid.NewGuid().ToString();
            await PublishEventAsync(
                new VideoGenerationRequested
                {
                    RequestId = requestId,
                    Model = request.Model,
                    Prompt = request.Prompt,
                    VirtualKeyId = virtualKeyInfo.Id.ToString(),
                    RequestedAt = DateTime.UtcNow,
                    CorrelationId = requestId
                },
                "video generation request",
                new { Model = request.Model, VirtualKeyId = virtualKeyInfo.Id });

            try
            {
                // Check if the client supports video generation using reflection
                // This avoids circular dependencies while allowing providers to implement video generation
                VideoGenerationResponse response;
                
                var clientType = client.GetType();
                _logger.LogInformation("Client type for model {Model}: {ClientType}", request.Model, clientType.FullName);
                
                // Check if this is a decorator and try to get the inner client
                object clientToCheck = client;
                if (clientType.FullName?.Contains("Decorator") == true || clientType.FullName?.Contains("PerformanceTracking") == true)
                {
                    // Try to get the inner client via reflection
                    var innerClientField = clientType.GetField("_innerClient", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (innerClientField != null)
                    {
                        var innerClient = innerClientField.GetValue(client);
                        if (innerClient != null)
                        {
                            clientToCheck = innerClient;
                            clientType = innerClient.GetType();
                            _logger.LogInformation("Unwrapped decorator to inner client type: {InnerClientType}", clientType.FullName);
                        }
                    }
                }
                
                // Try to find the method with both nullable and non-nullable string parameter
                var createVideoMethod = clientType.GetMethod("CreateVideoAsync", 
                    new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) })
                    ?? clientType.GetMethod("CreateVideoAsync", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(VideoGenerationRequest), typeof(string), typeof(CancellationToken) },
                        null);
                
                // If not found, try finding any method with the name and check parameters manually
                if (createVideoMethod == null)
                {
                    var methods = clientType.GetMethods()
                        .Where(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3)
                        .ToArray();
                    
                    _logger.LogInformation("Found {Count} CreateVideoAsync methods with 3 parameters", methods.Length);
                    foreach (var method in methods)
                    {
                        var parameters = method.GetParameters();
                        _logger.LogInformation("Method: {Method}, Parameters: {P1} {P2} {P3}", 
                            method.Name,
                            parameters[0].ParameterType.Name,
                            parameters[1].ParameterType.Name,
                            parameters[2].ParameterType.Name);
                    }
                    
                    if (methods.Length > 0)
                    {
                        createVideoMethod = methods[0];
                    }
                }
                
                if (createVideoMethod != null)
                {
                    // The client is already configured with the correct API key
                    var task = createVideoMethod.Invoke(clientToCheck, new object?[] { request, null, cancellationToken }) as Task<VideoGenerationResponse>;
                    if (task != null)
                    {
                        response = await task;
                    }
                    else
                    {
                        throw new InvalidOperationException($"CreateVideoAsync method on {clientType.Name} did not return expected Task<VideoGenerationResponse>");
                    }
                }
                else
                {
                    _logger.LogError("CreateVideoAsync method not found on client type {ClientType} for model {Model}", 
                        clientType.FullName, request.Model);
                    
                    // Log all public methods to help debug
                    var allMethods = clientType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                        .Where(m => m.Name.Contains("Video"))
                        .Select(m => m.Name)
                        .Distinct()
                        .ToList();
                    
                    _logger.LogError("Available video-related methods on {ClientType}: {Methods}", 
                        clientType.FullName, string.Join(", ", allMethods));
                    
                    throw new NotSupportedException($"Provider for model {request.Model} does not support video generation");
                }
                
                // Store video in media storage
                if (response.Data != null)
                {
                    foreach (var video in response.Data)
                    {
                        if (!string.IsNullOrEmpty(video.B64Json))
                        {
                            // Convert base64 to stream
                            var videoBytes = Convert.FromBase64String(video.B64Json);
                            using var videoStream = new MemoryStream(videoBytes);
                            
                            // Create video metadata for storage
                            var videoMediaMetadata = new VideoMediaMetadata
                            {
                                MediaType = MediaType.Video,
                                ContentType = video.Metadata?.MimeType ?? "video/mp4",
                                FileSizeBytes = videoBytes.Length,
                                FileName = $"video_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4",
                                Width = video.Metadata?.Width ?? 1280,
                                Height = video.Metadata?.Height ?? 720,
                                Duration = video.Metadata?.Duration ?? request.Duration ?? 6,
                                FrameRate = video.Metadata?.Fps ?? 30,
                                Codec = video.Metadata?.Codec ?? "h264",
                                Bitrate = video.Metadata?.Bitrate,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                Resolution = request.Size ?? "1280x720"
                            };
                            
                            var storageResult = await _mediaStorage.StoreVideoAsync(videoStream, videoMediaMetadata);
                            video.Url = storageResult.Url;
                            video.B64Json = null; // Clear base64 data after storing
                            
                            // Publish MediaGenerationCompleted event for lifecycle tracking
                            await PublishEventAsync(new MediaGenerationCompleted
                            {
                                MediaType = MediaType.Video,
                                VirtualKeyId = virtualKeyInfo.Id,
                                MediaUrl = storageResult.Url,
                                StorageKey = storageResult.StorageKey,
                                FileSizeBytes = videoMediaMetadata.FileSizeBytes,
                                ContentType = videoMediaMetadata.ContentType,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                GeneratedAt = DateTime.UtcNow,
                                Metadata = new Dictionary<string, object>
                                {
                                    ["width"] = videoMediaMetadata.Width,
                                    ["height"] = videoMediaMetadata.Height,
                                    ["duration"] = videoMediaMetadata.Duration,
                                    ["frameRate"] = videoMediaMetadata.FrameRate,
                                    ["resolution"] = videoMediaMetadata.Resolution
                                },
                                CorrelationId = requestId
                            }, "media generation completed", new { MediaType = "Video", Model = request.Model });
                        }
                    }
                }

                // Update spend
                var cost = await EstimateCostAsync(request, cancellationToken);
                await _virtualKeyService.UpdateSpendAsync(virtualKeyInfo.Id, cost);

                // Publish VideoGenerationCompleted event
                await PublishEventAsync(new VideoGenerationCompleted
                {
                    RequestId = requestId,
                    VideoUrl = response.Data?.FirstOrDefault()?.Url ?? string.Empty,
                    CompletedAt = DateTime.UtcNow,
                    CorrelationId = requestId
                }, "video generation completed", new { Model = request.Model });

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video generation failed for model {Model}", request.Model);

                // Publish VideoGenerationFailed event
                await PublishEventAsync(
                    new VideoGenerationFailed
                    {
                        RequestId = requestId,
                        Error = ex.Message,
                        FailedAt = DateTime.UtcNow,
                        CorrelationId = requestId
                    },
                    "video generation failure",
                    new { Model = request.Model, Error = ex.Message });

                throw;
            }
        }

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

            // Validate virtual key
            var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, request.Model);
            if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Invalid or disabled virtual key");
            }

            // Create task metadata
            var taskMetadata = new
            {
                Request = request,
                VirtualKey = virtualKey,
                Model = request.Model
            };

            // Create async task using new overload with explicit virtualKeyId
            var taskId = await _taskService.CreateTaskAsync("video_generation", virtualKeyInfo.Id, taskMetadata, cancellationToken);

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
        public Task<VideoGenerationResponse> GetVideoGenerationStatusAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Checking status for video generation task {TaskId}", taskId);

            // TODO: Implement task status tracking
            // This would query a task status store or cache
            return Task.FromException<VideoGenerationResponse>(
                new NotImplementedException("Video generation status tracking not yet implemented"));
        }

        /// <inheritdoc/>
        public async Task<bool> CancelVideoGenerationAsync(
            string taskId,
            string virtualKey,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cancelling video generation task {TaskId}", taskId);

            // Publish VideoGenerationCancelled event
            await PublishEventAsync(
                new VideoGenerationCancelled
                {
                    RequestId = taskId,
                    CancelledAt = DateTime.UtcNow,
                    CorrelationId = taskId
                },
                "video generation cancellation",
                new { TaskId = taskId });

            // TODO: Implement actual cancellation logic
            return await Task.FromResult(true);
        }

        /// <inheritdoc/>
        public async Task<bool> ValidateRequestAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                _logger.LogWarning("Video generation request validation failed: empty prompt");
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                _logger.LogWarning("Video generation request validation failed: empty model");
                return false;
            }

            // Check if model supports video generation
            var supportsVideo = await _capabilityService.SupportsVideoGenerationAsync(request.Model);
            if (!supportsVideo)
            {
                _logger.LogWarning("Model {Model} does not support video generation", request.Model);
                return false;
            }

            // Validate duration if specified
            if (request.Duration.HasValue && (request.Duration.Value < 1 || request.Duration.Value > 60))
            {
                _logger.LogWarning("Invalid video duration: {Duration}", request.Duration);
                return false;
            }

            // Validate FPS if specified
            if (request.Fps.HasValue && (request.Fps.Value < 1 || request.Fps.Value > 120))
            {
                _logger.LogWarning("Invalid video FPS: {Fps}", request.Fps);
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public async Task<decimal> EstimateCostAsync(
            VideoGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            // Create usage object for cost calculation
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = request.Duration ?? 6, // Default 6 seconds
                VideoResolution = request.Size ?? "1280x720"
            };

            // Use the cost calculation service
            return await _costService.CalculateCostAsync(request.Model, usage, cancellationToken);
        }
    }
}