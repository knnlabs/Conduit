using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Orchestrates video generation tasks by consuming events and managing the generation lifecycle.
    /// Handles async video generation workflow with progress tracking and error handling.
    /// </summary>
    public class VideoGenerationOrchestrator : 
        IConsumer<VideoGenerationRequested>,
        IConsumer<VideoGenerationCancelled>
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAsyncTaskService _taskService;
        private readonly IMediaStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly ICostCalculationService _costService;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly IWebhookNotificationService _webhookService;
        private readonly VideoGenerationRetryConfiguration _retryConfiguration;
        private readonly ILogger<VideoGenerationOrchestrator> _logger;

        public VideoGenerationOrchestrator(
            ILLMClientFactory clientFactory,
            IAsyncTaskService taskService,
            IMediaStorageService storageService,
            IPublishEndpoint publishEndpoint,
            IModelProviderMappingService modelMappingService,
            IProviderDiscoveryService discoveryService,
            IVirtualKeyService virtualKeyService,
            ICostCalculationService costService,
            ICancellableTaskRegistry taskRegistry,
            IWebhookNotificationService webhookService,
            IOptions<VideoGenerationRetryConfiguration> retryConfiguration,
            ILogger<VideoGenerationOrchestrator> logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _taskRegistry = taskRegistry ?? throw new ArgumentNullException(nameof(taskRegistry));
            _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
            _retryConfiguration = retryConfiguration?.Value ?? new VideoGenerationRetryConfiguration();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles video generation requested events.
        /// </summary>
        public async Task Consume(ConsumeContext<VideoGenerationRequested> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            
            // Only process async requests in the orchestrator
            if (!request.IsAsync)
            {
                _logger.LogDebug("Skipping synchronous video generation request {RequestId}", request.RequestId);
                return;
            }

            // NOTE: This method is now called by the background service worker
            // The Task.Run anti-pattern has been removed - video generation now runs
            // synchronously within the worker thread

            try
            {
                _logger.LogInformation("Processing async video generation task {RequestId} for model {Model}", 
                    request.RequestId, request.Model);
                
                // Update task status to processing
                await _taskService.UpdateTaskStatusAsync(request.RequestId, TaskState.Processing, cancellationToken: context.CancellationToken);
                
                // Publish VideoGenerationStarted event
                await _publishEndpoint.Publish(new VideoGenerationStarted
                {
                    RequestId = request.RequestId,
                    Provider = "pending", // Will be updated when provider is determined
                    StartedAt = DateTime.UtcNow,
                    EstimatedSeconds = 60, // Default estimate
                    CorrelationId = request.CorrelationId
                });
                
                // Get provider and model info
                var modelInfo = await GetModelInfoAsync(request.Model, request.VirtualKeyId);
                if (modelInfo == null)
                {
                    throw new InvalidOperationException($"Model {request.Model} not found or not available");
                }
                
                // Get task to retrieve the actual virtual key from metadata
                _logger.LogInformation("Retrieving task {TaskId} to get virtual key from metadata", request.RequestId);
                var task = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (task == null)
                {
                    _logger.LogError("Task {TaskId} not found", request.RequestId);
                    throw new InvalidOperationException($"Task {request.RequestId} not found");
                }
                if (task.Metadata == null)
                {
                    _logger.LogError("Task {TaskId} has no metadata", request.RequestId);
                    throw new InvalidOperationException($"Task {request.RequestId} has no metadata");
                }
                
                // Extract the virtual key from task metadata
                string virtualKey;
                try
                {
                    // Serialize the metadata object to JSON string first, then deserialize to JsonElement
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(task.Metadata);
                    _logger.LogDebug("Task metadata JSON: {MetadataJson}", metadataJson);
                    
                    var metadata = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(metadataJson);
                    
                    // The InMemoryAsyncTaskService wraps the original metadata
                    // Check if we have the wrapped format first
                    if (metadata.TryGetProperty("originalMetadata", out var originalMetadataElement))
                    {
                        // This is the wrapped format from InMemoryAsyncTaskService
                        _logger.LogDebug("Found originalMetadata wrapper, extracting inner metadata");
                        var originalMetadata = originalMetadataElement;
                        
                        // Log what's inside originalMetadata
                        if (originalMetadata.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            var innerProps = originalMetadata.EnumerateObject().Select(p => p.Name).ToList();
                            _logger.LogDebug("Properties in originalMetadata: {Properties}", string.Join(", ", innerProps));
                        }
                        
                        // Now look for VirtualKey in the original metadata
                        if (originalMetadata.TryGetProperty("VirtualKey", out var vkElement))
                        {
                            virtualKey = vkElement.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                        }
                        else if (originalMetadata.TryGetProperty("virtualKey", out var vkElementLower))
                        {
                            virtualKey = vkElementLower.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                        }
                        else
                        {
                            // Log available properties for debugging
                            var properties = originalMetadata.EnumerateObject().Select(p => p.Name).ToList();
                            _logger.LogError("Virtual key not found in original metadata. Available properties: {Properties}", string.Join(", ", properties));
                            throw new InvalidOperationException("Virtual key not found in task metadata");
                        }
                    }
                    else
                    {
                        // Try direct property access (for database-stored tasks)
                        if (metadata.TryGetProperty("VirtualKey", out var vkElement))
                        {
                            virtualKey = vkElement.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                        }
                        else if (metadata.TryGetProperty("virtualKey", out var vkElementLower))
                        {
                            virtualKey = vkElementLower.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                        }
                        else
                        {
                            // Log available properties for debugging
                            var properties = metadata.EnumerateObject().Select(p => p.Name).ToList();
                            _logger.LogError("Virtual key not found in metadata. Available properties: {Properties}", string.Join(", ", properties));
                            throw new InvalidOperationException("Virtual key not found in task metadata");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract virtual key from task metadata. Metadata type: {MetadataType}", task.Metadata?.GetType().FullName ?? "null");
                    throw new InvalidOperationException("Invalid task metadata format", ex);
                }
                
                // Validate virtual key using the actual key from metadata
                var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey, request.Model);
                if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
                {
                    throw new UnauthorizedAccessException("Invalid or disabled virtual key");
                }
                
                // Check if model supports video generation
                var capabilities = await _discoveryService.DiscoverModelsAsync();
                var modelCapability = capabilities.Values.FirstOrDefault(m => 
                    m.ModelId.Equals(request.Model, StringComparison.OrdinalIgnoreCase));
                
                if (modelCapability?.Capabilities.VideoGeneration != true)
                {
                    throw new NotSupportedException($"Model {request.Model} does not support video generation");
                }
                
                // Perform actual video generation
                await GenerateVideoAsync(request, modelInfo, virtualKeyInfo, stopwatch, context.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video generation failed for request {RequestId}", request.RequestId);
                
                // Get current task status to check retry count
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                var retryCount = taskStatus?.RetryCount ?? 0;
                var maxRetries = _retryConfiguration.EnableRetries ? 
                    (taskStatus?.MaxRetries ?? _retryConfiguration.MaxRetries) : 0;
                var isRetryable = _retryConfiguration.EnableRetries && 
                    IsRetryableError(ex) && retryCount < maxRetries;
                
                // Calculate next retry time with exponential backoff
                DateTime? nextRetryAt = null;
                if (isRetryable)
                {
                    var delaySeconds = _retryConfiguration.CalculateRetryDelay(retryCount);
                    nextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                    
                    // Update task status to pending for retry
                    await _taskService.UpdateTaskStatusAsync(
                        request.RequestId, 
                        TaskState.Pending, 
                        error: $"Retry {retryCount + 1}/{maxRetries} scheduled: {ex.Message}");
                }
                else
                {
                    // Update task status to failed (no more retries)
                    await _taskService.UpdateTaskStatusAsync(
                        request.RequestId, 
                        TaskState.Failed, 
                        error: ex.Message);
                }
                
                // Publish failure event with retry information
                await _publishEndpoint.Publish(new VideoGenerationFailed
                {
                    RequestId = request.RequestId,
                    Error = ex.Message,
                    ErrorCode = ex.GetType().Name,
                    IsRetryable = isRetryable,
                    RetryCount = retryCount,
                    MaxRetries = maxRetries,
                    NextRetryAt = nextRetryAt,
                    FailedAt = DateTime.UtcNow,
                    CorrelationId = request.CorrelationId
                });
            }
        }

        /// <summary>
        /// Handles video generation cancellation events.
        /// </summary>
        public async Task Consume(ConsumeContext<VideoGenerationCancelled> context)
        {
            var cancellation = context.Message;
            
            _logger.LogInformation("Processing video generation cancellation for task {RequestId}", 
                cancellation.RequestId);
            
            try
            {
                // Try to cancel the running task via the registry
                var cancelledViaRegistry = false;
                if (_taskRegistry.TryCancel(cancellation.RequestId))
                {
                    cancelledViaRegistry = true;
                    _logger.LogInformation("Successfully cancelled running task {RequestId} via registry", 
                        cancellation.RequestId);
                }
                else
                {
                    _logger.LogDebug("Task {RequestId} not found in registry, it may have already completed", 
                        cancellation.RequestId);
                }
                
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    cancellation.RequestId, 
                    TaskState.Cancelled, 
                    error: cancellation.Reason ?? "User requested cancellation");
                
                // If we cancelled via registry, the task's cancellation token was triggered
                // and the running operation should stop. The provider implementation
                // needs to respect the cancellation token for this to work properly.
                
                _logger.LogInformation("Successfully updated video generation task {RequestId} status to cancelled (registry cancellation: {CancelledViaRegistry})", 
                    cancellation.RequestId, cancelledViaRegistry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel video generation task {RequestId}", 
                    cancellation.RequestId);
            }
        }

        /// <summary>
        /// Performs actual video generation using the appropriate provider.
        /// </summary>
        private async Task GenerateVideoAsync(
            VideoGenerationRequested request,
            ModelInfo modelInfo,
            ConduitLLM.Configuration.Entities.VirtualKey virtualKeyInfo,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get the task metadata to reconstruct the request
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (taskStatus == null)
                {
                    throw new InvalidOperationException($"Task {request.RequestId} not found");
                }

                // Extract the virtual key and request from metadata
                string virtualKey;
                VideoGenerationRequest videoRequest;
                try
                {
                    // Serialize the metadata object to JSON string first, then deserialize to JsonElement
                    var metadataJsonString = System.Text.Json.JsonSerializer.Serialize(taskStatus.Metadata);
                    var metadataJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(metadataJsonString);
                    
                    // Handle wrapped format from InMemoryAsyncTaskService
                    System.Text.Json.JsonElement workingMetadata;
                    if (metadataJson.TryGetProperty("originalMetadata", out var originalMetadataElement))
                    {
                        workingMetadata = originalMetadataElement;
                    }
                    else
                    {
                        workingMetadata = metadataJson;
                    }
                    
                    // Extract virtual key
                    if (workingMetadata.TryGetProperty("VirtualKey", out var vkElement))
                    {
                        virtualKey = vkElement.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                    }
                    else if (workingMetadata.TryGetProperty("virtualKey", out var vkElementLower))
                    {
                        virtualKey = vkElementLower.GetString() ?? throw new InvalidOperationException("Virtual key value is null");
                    }
                    else
                    {
                        throw new InvalidOperationException("Virtual key not found in task metadata");
                    }
                    
                    // Reconstruct the video generation request from metadata
                    if (workingMetadata.TryGetProperty("Request", out var requestElement))
                    {
                        videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(requestElement.GetRawText()) ??
                            throw new InvalidOperationException("Failed to deserialize request from metadata");
                    }
                    else
                    {
                        // Fallback to constructing from event parameters
                        videoRequest = new VideoGenerationRequest
                        {
                            Model = request.Model,
                            Prompt = request.Prompt,
                            Duration = request.Parameters?.Duration,
                            Size = request.Parameters?.Size,
                            Fps = request.Parameters?.Fps,
                            N = 1
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract data from task metadata");
                    throw new InvalidOperationException("Invalid task metadata format", ex);
                }

                // Get the appropriate client for the model
                var client = _clientFactory.GetClient(request.Model);
                if (client == null)
                {
                    throw new NotSupportedException($"No provider available for model {request.Model}");
                }

                // Check if the client supports video generation using reflection
                VideoGenerationResponse response;
                var clientType = client.GetType();
                
                // Handle decorators by getting inner client
                object clientToCheck = client;
                if (clientType.FullName?.Contains("Decorator") == true || clientType.FullName?.Contains("PerformanceTracking") == true)
                {
                    var innerClientField = clientType.GetField("_innerClient", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (innerClientField != null)
                    {
                        var innerClient = innerClientField.GetValue(client);
                        if (innerClient != null)
                        {
                            clientToCheck = innerClient;
                            clientType = innerClient.GetType();
                        }
                    }
                }
                
                // Find CreateVideoAsync method
                var createVideoMethod = clientType.GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3);
                
                if (createVideoMethod != null)
                {
                    // Create a cancellation token source for this specific task
                    var taskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    
                    // Register the task for cancellation
                    _taskRegistry.RegisterTask(request.RequestId, taskCts);
                    _logger.LogDebug("Registered task {RequestId} for cancellation", request.RequestId);
                    
                    try
                    {
                        // Start progress tracking via event-driven architecture
                        // NOTE: Disabled time-based tracking in favor of real progress from MiniMax
                        // await StartProgressTrackingAsync(request);

                        // Set up progress callback if the client is MiniMax
                        if (clientType.Name == "MiniMaxClient")
                        {
                            // Use reflection to set the progress callback
                            var setCallbackMethod = clientType.GetMethod("SetVideoProgressCallback");
                            if (setCallbackMethod != null)
                            {
                                Func<string, string, int, Task> progressCallback = async (taskId, status, progressPercentage) =>
                                {
                                    _logger.LogInformation("Video generation progress for {TaskId}: {Status} at {Progress}%", 
                                        taskId, status, progressPercentage);
                                    
                                    // Update task progress
                                    await _taskService.UpdateTaskStatusAsync(
                                        request.RequestId,
                                        TaskState.Processing,
                                        progress: progressPercentage);
                                    
                                    // Publish progress event
                                    await _publishEndpoint.Publish(new VideoGenerationProgress
                                    {
                                        RequestId = request.RequestId,
                                        ProgressPercentage = progressPercentage,
                                        Status = status,
                                        Message = $"Video generation {status.ToLowerInvariant()}",
                                        CorrelationId = request.CorrelationId
                                    });
                                };
                                
                                setCallbackMethod.Invoke(clientToCheck, new object[] { progressCallback });
                                _logger.LogDebug("Set video progress callback for MiniMax client");
                            }
                        }

                        _logger.LogInformation("Processing video generation for task {RequestId} with cancellation support", request.RequestId);
                        
                        // Invoke video generation with the virtual key and cancellation token
                        // The client is already configured with the correct API key from the factory
                        var task = createVideoMethod.Invoke(clientToCheck, new object?[] { videoRequest, null, taskCts.Token }) as Task<VideoGenerationResponse>;
                        if (task != null)
                        {
                            response = await task;
                            
                            // Process the response when it completes
                            await ProcessVideoResponseAsync(request, response, modelInfo, virtualKeyInfo, stopwatch);
                        }
                        else
                        {
                            throw new InvalidOperationException($"CreateVideoAsync method on {clientType.Name} did not return expected Task<VideoGenerationResponse>");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Video generation for task {RequestId} was cancelled", request.RequestId);
                        await HandleVideoGenerationCancellationAsync(request, stopwatch);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Video generation failed for task {RequestId}", request.RequestId);
                        await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
                    }
                    finally
                    {
                        // Unregister the task when complete
                        _taskRegistry.UnregisterTask(request.RequestId);
                    }
                    
                    _logger.LogInformation("Video generation task {RequestId} completed processing", request.RequestId);
                    return;
                }
                else
                {
                    throw new NotSupportedException($"Provider for model {request.Model} does not support video generation");
                }
            }
            catch (Exception ex)
            {
                await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
            }
        }

        /// <summary>
        /// Starts progress tracking for a video generation task using event-driven architecture.
        /// </summary>
        private async Task StartProgressTrackingAsync(VideoGenerationRequested request)
        {
            // Publish initial progress check request
            var progressCheck = new VideoProgressCheckRequested
            {
                RequestId = request.RequestId,
                VirtualKeyId = request.VirtualKeyId,
                ScheduledAt = DateTime.UtcNow.AddSeconds(5), // First check in 5 seconds
                IntervalIndex = 0,
                TotalIntervals = 5, // 10%, 30%, 50%, 70%, 90%
                StartTime = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            };
            
            await _publishEndpoint.Publish(progressCheck);
            _logger.LogDebug("Initiated progress tracking for video generation {RequestId}", request.RequestId);
        }

        /// <summary>
        /// Gets model information from mappings or discovery service.
        /// </summary>
        private async Task<ModelInfo?> GetModelInfoAsync(string modelAlias, string virtualKeyHash)
        {
            // First try to get from model mappings
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelAlias);
            
            if (mapping != null)
            {
                return new ModelInfo
                {
                    ModelId = mapping.ProviderModelId,
                    Provider = mapping.ProviderName,
                    ModelAlias = mapping.ModelAlias
                };
            }
            
            // Fall back to discovery service
            var discovered = await _discoveryService.DiscoverModelsAsync();
            var model = discovered.Values.FirstOrDefault(m => 
                m.ModelId.Equals(modelAlias, StringComparison.OrdinalIgnoreCase));
            
            if (model != null)
            {
                return new ModelInfo
                {
                    ModelId = model.ModelId,
                    Provider = model.Provider,
                    ModelAlias = modelAlias
                };
            }
            
            return null;
        }

        /// <summary>
        /// Calculates the cost for video generation.
        /// </summary>
        private async Task<decimal> CalculateVideoCostAsync(VideoGenerationRequested request, ModelInfo modelInfo)
        {
            // Create usage object for video generation
            var usage = new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = request.Parameters?.Duration ?? 5,
                VideoResolution = request.Parameters?.Size ?? "1280x720"
            };

            // Use the cost calculation service
            return await _costService.CalculateCostAsync(modelInfo.ModelId, usage);
        }

        private decimal GetResolutionMultiplier(string? resolution)
        {
            return resolution switch
            {
                "1920x1080" or "1080x1920" => 2.0m,
                "1280x720" or "720x1280" => 1.5m,
                "720x480" or "480x720" => 1.2m,
                _ => 1.0m
            };
        }

        private bool IsRetryableError(Exception ex)
        {
            // Check the exception type
            var isRetryableType = ex switch
            {
                TimeoutException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                System.IO.IOException => true,
                System.Net.Sockets.SocketException => true,
                _ => false
            };

            // Check for specific error messages that indicate transient failures
            if (!isRetryableType && ex.Message != null)
            {
                var lowerMessage = ex.Message.ToLowerInvariant();
                isRetryableType = lowerMessage.Contains("timeout") ||
                                  lowerMessage.Contains("timed out") ||
                                  lowerMessage.Contains("connection") ||
                                  lowerMessage.Contains("network") ||
                                  lowerMessage.Contains("temporarily unavailable") ||
                                  lowerMessage.Contains("service unavailable") ||
                                  lowerMessage.Contains("too many requests") ||
                                  lowerMessage.Contains("rate limit");
            }

            return isRetryableType;
        }

        /// <summary>
        /// Process the video response after generation completes.
        /// </summary>
        private async Task ProcessVideoResponseAsync(
            VideoGenerationRequested request,
            VideoGenerationResponse response,
            ModelInfo modelInfo,
            ConduitLLM.Configuration.Entities.VirtualKey virtualKeyInfo,
            Stopwatch stopwatch)
        {
            try
            {
                // Get the task metadata to reconstruct the request
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (taskStatus == null)
                {
                    throw new InvalidOperationException($"Task {request.RequestId} not found");
                }

                // Reconstruct the video generation request from metadata
                VideoGenerationRequest videoRequest;
                try
                {
                    // Serialize the metadata object to JSON string first, then deserialize to JsonElement
                    var metadataJsonString = System.Text.Json.JsonSerializer.Serialize(taskStatus.Metadata);
                    var metadataJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(metadataJsonString);
                    
                    // Handle wrapped format from InMemoryAsyncTaskService
                    System.Text.Json.JsonElement workingMetadata;
                    if (metadataJson.TryGetProperty("originalMetadata", out var originalMetadataElement))
                    {
                        workingMetadata = originalMetadataElement;
                    }
                    else
                    {
                        workingMetadata = metadataJson;
                    }
                    
                    // Extract the Request from metadata
                    if (workingMetadata.TryGetProperty("Request", out var requestElement))
                    {
                        videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(requestElement.GetRawText()) ??
                            throw new InvalidOperationException("Failed to deserialize request from metadata");
                    }
                    else
                    {
                        // Fallback to constructing from event parameters
                        videoRequest = new VideoGenerationRequest
                        {
                            Model = request.Model,
                            Prompt = request.Prompt,
                            Duration = request.Parameters?.Duration,
                            Size = request.Parameters?.Size,
                            Fps = request.Parameters?.Fps,
                            ResponseFormat = "url"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to extract request from task metadata, using fallback");
                    // Fallback to constructing from event parameters
                    videoRequest = new VideoGenerationRequest
                    {
                        Model = request.Model,
                        Prompt = request.Prompt,
                        Duration = request.Parameters?.Duration ?? 6,
                        Size = request.Parameters?.Size ?? "1280x720",
                        Fps = request.Parameters?.Fps ?? 30,
                        ResponseFormat = "url"
                    };
                }

                // Store video in media storage if base64 data is provided
                var videoUrl = response.Data?.FirstOrDefault()?.Url;
                if (response.Data != null)
                {
                    foreach (var video in response.Data)
                    {
                        if (!string.IsNullOrEmpty(video.B64Json))
                        {
                            var videoBytes = Convert.FromBase64String(video.B64Json);
                            using var videoStream = new System.IO.MemoryStream(videoBytes);
                            
                            var videoMediaMetadata = new VideoMediaMetadata
                            {
                                MediaType = MediaType.Video,
                                ContentType = video.Metadata?.MimeType ?? "video/mp4",
                                FileSizeBytes = videoBytes.Length,
                                FileName = $"video_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.mp4",
                                Width = video.Metadata?.Width ?? 1280,
                                Height = video.Metadata?.Height ?? 720,
                                Duration = video.Metadata?.Duration ?? videoRequest.Duration ?? 6,
                                FrameRate = video.Metadata?.Fps ?? 30,
                                Codec = video.Metadata?.Codec ?? "h264",
                                Bitrate = video.Metadata?.Bitrate,
                                GeneratedByModel = request.Model,
                                GenerationPrompt = request.Prompt,
                                Resolution = videoRequest.Size ?? "1280x720"
                            };
                            
                            var storageResult = await _storageService.StoreVideoAsync(videoStream, videoMediaMetadata);
                            video.Url = storageResult.Url;
                            video.B64Json = null; // Clear base64 data after storing
                            videoUrl = storageResult.Url;
                            
                            // Publish MediaGenerationCompleted event for lifecycle tracking
                            await _publishEndpoint.Publish(new MediaGenerationCompleted
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
                                CorrelationId = request.CorrelationId
                            });
                        }
                    }
                }

                // Calculate cost
                var cost = await CalculateVideoCostAsync(request, modelInfo);
                
                // Update spend
                await _virtualKeyService.UpdateSpendAsync(virtualKeyInfo.Id, cost);
                
                // Update task status to completed
                var result = new 
                {
                    videoUrl = videoUrl,
                    usage = response.Usage,
                    model = response.Model ?? request.Model,
                    created = response.Created,
                    duration = stopwatch.Elapsed.TotalSeconds
                };
                
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Completed,
                    result: result);
                
                // Publish VideoGenerationCompleted event
                await _publishEndpoint.Publish(new VideoGenerationCompleted
                {
                    RequestId = request.RequestId,
                    VideoUrl = videoUrl ?? string.Empty,
                    CompletedAt = DateTime.UtcNow,
                    GenerationDuration = stopwatch.Elapsed,
                    CorrelationId = request.CorrelationId
                });
                
                // Send webhook notification if configured
                if (!string.IsNullOrEmpty(request.WebhookUrl))
                {
                    var webhookPayload = new VideoCompletionWebhookPayload
                    {
                        TaskId = request.RequestId,
                        Status = "completed",
                        VideoUrl = videoUrl,
                        GenerationDurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        Model = request.Model,
                        Prompt = request.Prompt
                    };

                    await _webhookService.SendTaskCompletionWebhookAsync(
                        request.WebhookUrl,
                        webhookPayload,
                        request.WebhookHeaders);
                }
                
                // Cancel progress tracking since task is complete
                // NOTE: Not needed when using real progress from MiniMax
                // await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
                // {
                //     RequestId = request.RequestId,
                //     VirtualKeyId = request.VirtualKeyId,
                //     Reason = "Video generation completed successfully",
                //     CorrelationId = request.CorrelationId
                // });
                
                _logger.LogInformation("Successfully completed video generation task {RequestId} in {Duration}ms",
                    request.RequestId, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video response for task {RequestId}", request.RequestId);
                await HandleVideoGenerationFailureAsync(request, ex.Message, stopwatch);
            }
        }

        /// <summary>
        /// Handle video generation failure.
        /// </summary>
        private async Task HandleVideoGenerationFailureAsync(
            VideoGenerationRequested request,
            string errorMessage,
            Stopwatch stopwatch)
        {
            _logger.LogError("Video generation failed for task {RequestId}: {Error}", request.RequestId, errorMessage);
            
            // Get current task status to check retry count
            var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
            var retryCount = taskStatus?.RetryCount ?? 0;
            var maxRetries = _retryConfiguration.EnableRetries ? 
                (taskStatus?.MaxRetries ?? _retryConfiguration.MaxRetries) : 0;
            var isRetryable = _retryConfiguration.EnableRetries && 
                IsRetryableError(new Exception(errorMessage)) && retryCount < maxRetries;
            
            // Calculate next retry time with exponential backoff
            DateTime? nextRetryAt = null;
            if (isRetryable)
            {
                var delaySeconds = _retryConfiguration.CalculateRetryDelay(retryCount);
                nextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                
                // Update task status to pending for retry
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Pending, 
                    error: $"Retry {retryCount + 1}/{maxRetries} scheduled: {errorMessage}");
                
                _logger.LogInformation("Scheduling retry {RetryCount}/{MaxRetries} for task {RequestId} at {NextRetryAt}", 
                    retryCount + 1, maxRetries, request.RequestId, nextRetryAt);
            }
            else
            {
                // Update task status to failed (no more retries)
                await _taskService.UpdateTaskStatusAsync(
                    request.RequestId, 
                    TaskState.Failed, 
                    error: errorMessage);
            }
            
            // Publish VideoGenerationFailed event with retry information
            await _publishEndpoint.Publish(new VideoGenerationFailed
            {
                RequestId = request.RequestId,
                Error = errorMessage,
                IsRetryable = isRetryable,
                RetryCount = retryCount,
                MaxRetries = maxRetries,
                NextRetryAt = nextRetryAt,
                FailedAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
            
            // Cancel progress tracking since task has failed
            // NOTE: Not needed when using real progress from MiniMax
            // await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
            // {
            //     RequestId = request.RequestId,
            //     VirtualKeyId = request.VirtualKeyId,
            //     Reason = $"Video generation failed: {errorMessage}",
            //     CorrelationId = request.CorrelationId
            // });

            // Send webhook notification if configured
            if (!string.IsNullOrEmpty(request.WebhookUrl))
            {
                var webhookPayload = new VideoCompletionWebhookPayload
                {
                    TaskId = request.RequestId,
                    Status = isRetryable ? "retrying" : "failed",
                    Error = errorMessage,
                    Model = request.Model,
                    Prompt = request.Prompt
                };

                await _webhookService.SendTaskCompletionWebhookAsync(
                    request.WebhookUrl,
                    webhookPayload,
                    request.WebhookHeaders);
            }
        }

        /// <summary>
        /// Handle video generation cancellation.
        /// </summary>
        private async Task HandleVideoGenerationCancellationAsync(
            VideoGenerationRequested request,
            Stopwatch stopwatch)
        {
            _logger.LogInformation("Video generation cancelled for task {RequestId} after {ElapsedMs}ms", 
                request.RequestId, stopwatch.ElapsedMilliseconds);
            
            // Update task status to cancelled
            await _taskService.UpdateTaskStatusAsync(
                request.RequestId, 
                TaskState.Cancelled, 
                error: "Video generation was cancelled by user request");
            
            // Publish VideoGenerationCancelled event
            await _publishEndpoint.Publish(new VideoGenerationCancelled
            {
                RequestId = request.RequestId,
                CancelledAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
            
            // Cancel progress tracking since task is cancelled
            // NOTE: Not needed when using real progress from MiniMax
            // await _publishEndpoint.Publish(new VideoProgressTrackingCancelled
            // {
            //     RequestId = request.RequestId,
            //     VirtualKeyId = request.VirtualKeyId,
            //     Reason = "Video generation cancelled by user",
            //     CorrelationId = request.CorrelationId
            // });

            // Send webhook notification if configured
            if (!string.IsNullOrEmpty(request.WebhookUrl))
            {
                var webhookPayload = new VideoCompletionWebhookPayload
                {
                    TaskId = request.RequestId,
                    Status = "cancelled",
                    Error = "Video generation was cancelled by user request",
                    Model = request.Model,
                    Prompt = request.Prompt
                };

                await _webhookService.SendTaskCompletionWebhookAsync(
                    request.WebhookUrl,
                    webhookPayload,
                    request.WebhookHeaders);
            }
        }

        private class ModelInfo
        {
            public string ModelId { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string ModelAlias { get; set; } = string.Empty;
        }
    }
}