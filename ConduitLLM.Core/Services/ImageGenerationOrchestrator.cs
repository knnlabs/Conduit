using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using ConduitLLM.Configuration.Interfaces;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
using ConduitLLM.Core.Interfaces;
namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Orchestrates image generation tasks by consuming events and managing the generation lifecycle.
    /// </summary>
    public class ImageGenerationOrchestrator : IConsumer<ImageGenerationRequested>, IConsumer<ImageGenerationCancelled>
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAsyncTaskService _taskService;
        private readonly IMediaStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly ICostCalculationService _costCalculationService;
        private readonly IProviderService _providerService;
        private readonly ImageGenerationPerformanceConfiguration _performanceConfig;
        private readonly ILogger<ImageGenerationOrchestrator> _logger;

        public ImageGenerationOrchestrator(
            ILLMClientFactory clientFactory,
            IAsyncTaskService taskService,
            IMediaStorageService storageService,
            IPublishEndpoint publishEndpoint,
            IModelProviderMappingService modelMappingService,
            IProviderDiscoveryService discoveryService,
            IVirtualKeyService virtualKeyService,
            IHttpClientFactory httpClientFactory,
            ICancellableTaskRegistry taskRegistry,
            ICostCalculationService costCalculationService,
            IProviderService providerService,
            IOptions<ImageGenerationPerformanceConfiguration> performanceOptions,
            ILogger<ImageGenerationOrchestrator> logger)
        {
            _clientFactory = clientFactory;
            _taskService = taskService;
            _storageService = storageService;
            _publishEndpoint = publishEndpoint;
            _modelMappingService = modelMappingService;
            _discoveryService = discoveryService;
            _virtualKeyService = virtualKeyService;
            _httpClientFactory = httpClientFactory;
            _taskRegistry = taskRegistry;
            _costCalculationService = costCalculationService;
            _providerService = providerService;
            _performanceConfig = performanceOptions.Value;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ImageGenerationRequested> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            var downloadStopwatch = new Stopwatch();
            var storageStopwatch = new Stopwatch();
            ModelInfo? modelInfo = null;
            
            // Create a linked cancellation token source for this task
            using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            
            // Register the task for cancellation support
            _taskRegistry.RegisterTask(request.TaskId, taskCts);
            
            try
            {
                _logger.LogInformation("Processing image generation task {TaskId} for prompt: {Prompt}", 
                    request.TaskId, request.Request.Prompt);
                
                // Update task status to processing
                await _taskService.UpdateTaskStatusAsync(request.TaskId, TaskState.Processing, cancellationToken: taskCts.Token);
                
                // Publish progress event
                await _publishEndpoint.Publish(new ImageGenerationProgress
                {
                    TaskId = request.TaskId,
                    Status = "processing",
                    ImagesCompleted = 0,
                    TotalImages = request.Request.N,
                    CorrelationId = request.CorrelationId
                });
                
                // Get provider and model info
                modelInfo = await GetModelInfoAsync(request.Request.Model, request.VirtualKeyHash);
                if (modelInfo == null)
                {
                    throw new InvalidOperationException($"Model {request.Request.Model} not found or not available");
                }
                
                // Create LLM client
                var client = _clientFactory.GetClient(modelInfo.ModelId);
                
                // Prepare generation request
                var generationRequest = new ConduitLLM.Core.Models.ImageGenerationRequest
                {
                    Prompt = request.Request.Prompt,
                    Model = modelInfo.ModelId,
                    N = request.Request.N,
                    Size = request.Request.Size,
                    Quality = request.Request.Quality,
                    Style = request.Request.Style,
                    ResponseFormat = request.Request.ResponseFormat ?? "url",
                    User = request.Request.User
                };
                
                _logger.LogInformation("Generating {Count} images with {Provider} using model {Model}", 
                    request.Request.N, modelInfo.Provider, modelInfo.ModelId);
                
                // Generate images with cancellation support
                var response = await client.CreateImageAsync(generationRequest, cancellationToken: taskCts.Token);
                
                // Process and store images
                var processedImages = new List<ConduitLLM.Core.Events.ImageData>();
                var totalImages = response.Data?.Count ?? 0;
                
                // Determine optimal concurrency for image processing
                var concurrency = GetOptimalConcurrency(modelInfo.ProviderType.ToString(), totalImages);
                var semaphore = new SemaphoreSlim(concurrency);
                _logger.LogInformation("Processing {Count} images in parallel with concurrency limit of {Concurrency}", 
                    totalImages, concurrency);
                
                // Process images in parallel
                var imageTasks = new Task<ConduitLLM.Core.Events.ImageData>[totalImages];
                var progressCounter = 0;
                var downloadTime = 0L;
                var storageTime = 0L;
                
                for (int i = 0; i < totalImages; i++)
                {
                    var index = i; // Capture for closure
                    var imageData = response.Data![i];
                    
                    imageTasks[i] = ProcessSingleImageAsync(
                        imageData, 
                        index, 
                        request, 
                        modelInfo, 
                        semaphore,
                        taskCts.Token,
                        () => Interlocked.Increment(ref progressCounter),
                        (dt, st) => 
                        {
                            Interlocked.Add(ref downloadTime, dt);
                            Interlocked.Add(ref storageTime, st);
                        });
                }
                
                // Start progress reporting task
                var progressTask = ReportProgressAsync(
                    request.TaskId, 
                    request.CorrelationId, 
                    totalImages, 
                    () => progressCounter,
                    request.WebhookUrl,
                    request.WebhookHeaders,
                    taskCts.Token);
                
                // Wait for all images to complete
                var results = await Task.WhenAll(imageTasks);
                processedImages.AddRange(results);
                
                // Cancel progress reporting
                taskCts.Token.ThrowIfCancellationRequested();
                
                stopwatch.Stop();
                
                // Calculate cost using the centralized cost calculation service
                var cost = await CalculateImageGenerationCostAsync(modelInfo.ProviderType, modelInfo.ModelId, totalImages, taskCts.Token);
                
                // Update task with results
                await _taskService.UpdateTaskStatusAsync(
                    request.TaskId, 
                    TaskState.Completed,
                    progress: 100,
                    result: new
                    {
                        images = processedImages,
                        duration = stopwatch.Elapsed.TotalSeconds,
                        cost = cost,
                        provider = modelInfo.ProviderName,
                        model = modelInfo.ModelId
                    });
                
                // Publish completion event
                await _publishEndpoint.Publish(new ImageGenerationCompleted
                {
                    TaskId = request.TaskId,
                    VirtualKeyId = request.VirtualKeyId,
                    Images = processedImages,
                    Duration = stopwatch.Elapsed,
                    Cost = cost,
                    Provider = modelInfo.ProviderName,
                    Model = modelInfo.ModelId,
                    CorrelationId = request.CorrelationId
                });
                
                // Send webhook notification if configured
                if (!string.IsNullOrEmpty(request.WebhookUrl))
                {
                    var imageUrls = processedImages
                        .Where(img => !string.IsNullOrEmpty(img.Url))
                        .Select(img => img.Url!)
                        .ToList();
                    
                    var webhookPayload = new ImageCompletionWebhookPayload
                    {
                        TaskId = request.TaskId,
                        Status = "completed",
                        ImageUrls = imageUrls,
                        ImagesGenerated = processedImages.Count,
                        ImagesRequested = request.Request.N,
                        GenerationDurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        Model = request.Request.Model,
                        Prompt = request.Request.Prompt,
                        Size = request.Request.Size,
                        ResponseFormat = request.Request.ResponseFormat ?? "url"
                    };
                    
                    // Publish webhook delivery event for scalable processing
                    await _publishEndpoint.Publish(new WebhookDeliveryRequested
                    {
                        TaskId = request.TaskId,
                        TaskType = "image",
                        WebhookUrl = request.WebhookUrl,
                        EventType = WebhookEventType.TaskCompleted,
                        PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                        Headers = request.WebhookHeaders,
                        CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                    });
                    
                    _logger.LogDebug("Published webhook delivery event for completed image task {TaskId}", request.TaskId);
                }
                
                // Update spend
                if (cost > 0)
                {
                    await _publishEndpoint.Publish(new SpendUpdateRequested
                    {
                        KeyId = request.VirtualKeyId,
                        Amount = cost,
                        RequestId = request.TaskId,
                        CorrelationId = request.CorrelationId?.ToString() ?? string.Empty
                    });
                }
                
                _logger.LogInformation("Completed image generation task {TaskId} in {Duration}s with {Count} images",
                    request.TaskId, stopwatch.Elapsed.TotalSeconds, processedImages.Count);
            }
            catch (OperationCanceledException) when (taskCts.Token.IsCancellationRequested)
            {
                _logger.LogInformation("Image generation task {TaskId} was cancelled", request.TaskId);
                
                stopwatch.Stop();
                
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    request.TaskId,
                    TaskState.Cancelled,
                    error: "Task was cancelled by user request");
                
                // Send webhook notification if configured
                if (!string.IsNullOrEmpty(request.WebhookUrl))
                {
                    var webhookPayload = new ImageCompletionWebhookPayload
                    {
                        TaskId = request.TaskId,
                        Status = "cancelled",
                        Error = "Task was cancelled by user request",
                        ImagesGenerated = 0,
                        ImagesRequested = request.Request.N,
                        GenerationDurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        Model = request.Request.Model,
                        Prompt = request.Request.Prompt,
                        Size = request.Request.Size,
                        ResponseFormat = request.Request.ResponseFormat ?? "url"
                    };
                    
                    // Publish webhook delivery event for scalable processing
                    await _publishEndpoint.Publish(new WebhookDeliveryRequested
                    {
                        TaskId = request.TaskId,
                        TaskType = "image",
                        WebhookUrl = request.WebhookUrl,
                        EventType = WebhookEventType.TaskCancelled,
                        PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                        Headers = request.WebhookHeaders,
                        CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                    });
                    
                    _logger.LogDebug("Published webhook delivery event for cancelled image task {TaskId}", request.TaskId);
                }
                
                // Don't re-throw - cancellation is a normal completion path
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation task {TaskId}", request.TaskId);
                
                stopwatch.Stop();
                
                // Update task status
                await _taskService.UpdateTaskStatusAsync(
                    request.TaskId,
                    TaskState.Failed,
                    error: ex.Message);
                
                // Publish failure event
                await _publishEndpoint.Publish(new ImageGenerationFailed
                {
                    TaskId = request.TaskId,
                    VirtualKeyId = request.VirtualKeyId,
                    Error = ex.Message,
                    ErrorCode = ex.GetType().Name,
                    Provider = request.Request.Model ?? "unknown",
                    IsRetryable = IsRetryableError(ex),
                    AttemptCount = 1, // Would need to track this properly
                    CorrelationId = request.CorrelationId
                });
                
                // Send webhook notification if configured
                if (!string.IsNullOrEmpty(request.WebhookUrl))
                {
                    var webhookPayload = new ImageCompletionWebhookPayload
                    {
                        TaskId = request.TaskId,
                        Status = "failed",
                        Error = ex.Message,
                        ImagesGenerated = 0,
                        ImagesRequested = request.Request.N,
                        GenerationDurationSeconds = stopwatch.Elapsed.TotalSeconds,
                        Model = request.Request.Model,
                        Prompt = request.Request.Prompt,
                        Size = request.Request.Size,
                        ResponseFormat = request.Request.ResponseFormat ?? "url"
                    };
                    
                    // Publish webhook delivery event for scalable processing
                    await _publishEndpoint.Publish(new WebhookDeliveryRequested
                    {
                        TaskId = request.TaskId,
                        TaskType = "image",
                        WebhookUrl = request.WebhookUrl,
                        EventType = WebhookEventType.TaskFailed,
                        PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                        Headers = request.WebhookHeaders,
                        CorrelationId = request.CorrelationId ?? Guid.NewGuid().ToString()
                    });
                    
                    _logger.LogDebug("Published webhook delivery event for failed image task {TaskId}", request.TaskId);
                }
                
                // Re-throw to let MassTransit handle retry logic
                throw;
            }
            finally
            {
                // Always unregister the task from the cancellation registry
                _taskRegistry.UnregisterTask(request.TaskId);
            }
        }

        private async Task<ModelInfo?> GetModelInfoAsync(string? requestedModel, string virtualKeyHash)
        {
            // Get virtual key to check model access
            var virtualKey = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKeyHash, requestedModel);
            if (virtualKey == null)
            {
                return null;
            }
            
            // If no model specified, use default image model
            if (string.IsNullOrEmpty(requestedModel))
            {
                // Would need to implement default image model selection
                requestedModel = "dall-e-3"; // Default to DALL-E 3
            }
            
            // Get model mapping
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(requestedModel);
            if (mapping == null)
            {
                return null;
            }
            
            // Verify model supports image generation
            if (!mapping.SupportsImageGeneration)
            {
                _logger.LogWarning("Model {Model} does not support image generation", requestedModel);
                return null;
            }
            
            // Get the provider entity
            var provider = await _providerService.GetProviderByIdAsync(mapping.ProviderId);
            if (provider == null)
            {
                _logger.LogWarning("Provider not found for ProviderId {ProviderId}", mapping.ProviderId);
                return null;
            }
            
            return new ModelInfo
            {
                Provider = provider,
                ModelId = mapping.ProviderModelId,
                ProviderId = mapping.ProviderId
            };
        }

        private async Task<decimal> CalculateImageGenerationCostAsync(ProviderType providerType, string model, int imageCount, CancellationToken cancellationToken)
        {
            // Create usage object for cost calculation
            var usage = new Usage
            {
                ImageCount = imageCount
            };
            
            // Use the centralized cost calculation service
            var cost = await _costCalculationService.CalculateCostAsync(model, usage, cancellationToken);
            
            return cost;
        }

        private bool IsRetryableError(Exception ex)
        {
            // Determine if error is retryable
            return ex switch
            {
                TaskCanceledException => true,
                TimeoutException => true,
                _ when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
                _ when ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => true,
                _ when ex.Message.Contains("temporary", StringComparison.OrdinalIgnoreCase) => true,
                _ => false
            };
        }

        /// <summary>
        /// Handles image generation cancellation requests
        /// </summary>
        public async Task Consume(ConsumeContext<ImageGenerationCancelled> context)
        {
            var request = context.Message;
            
            try
            {
                _logger.LogInformation("Processing image generation cancellation for task {TaskId}", request.TaskId);
                
                // Try to cancel via the registry
                var cancelled = _taskRegistry.TryCancel(request.TaskId);
                
                if (cancelled)
                {
                    _logger.LogInformation("Successfully cancelled image generation task {TaskId}", request.TaskId);
                }
                else
                {
                    _logger.LogWarning("Could not cancel image generation task {TaskId} - task may have already completed", 
                        request.TaskId);
                }
                
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    request.TaskId,
                    TaskState.Cancelled,
                    error: request.Reason ?? "Cancelled by user request");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation cancellation for task {TaskId}", request.TaskId);
                // Don't re-throw - cancellation is best effort
            }
        }

        private async Task<ConduitLLM.Core.Events.ImageData> ProcessSingleImageAsync(
            ConduitLLM.Core.Models.ImageData imageData,
            int index,
            ImageGenerationRequested request,
            ModelInfo modelInfo,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken,
            Action onProgress,
            Action<long, long> onTimingUpdate)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                string? finalUrl = imageData.Url;
                
                if (!string.IsNullOrEmpty(imageData.B64Json))
                {
                    // Store base64 image using streaming to avoid loading entire content into memory
                    var metadata = new Dictionary<string, string>
                    {
                        ["prompt"] = request.Request.Prompt,
                        ["model"] = modelInfo.ModelId,
                        ["provider"] = modelInfo.ProviderName
                    };
                    
                    var mediaMetadata = new MediaMetadata
                    {
                        ContentType = "image/png",
                        FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}.png",
                        MediaType = MediaType.Image,
                        CustomMetadata = metadata
                    };
                    
                    // Use streaming to decode base64
                    using var base64Stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(imageData.B64Json));
                    using var decodedStream = new System.Security.Cryptography.CryptoStream(
                        base64Stream, 
                        new System.Security.Cryptography.FromBase64Transform(), 
                        System.Security.Cryptography.CryptoStreamMode.Read);
                    
                    var storageResult = await _storageService.StoreAsync(decodedStream, mediaMetadata);
                    finalUrl = storageResult.Url;
                    
                    // Publish MediaGenerationCompleted event for lifecycle tracking
                    await _publishEndpoint.Publish(new MediaGenerationCompleted
                    {
                        MediaType = MediaType.Image,
                        VirtualKeyId = request.VirtualKeyId,
                        MediaUrl = storageResult.Url,
                        StorageKey = storageResult.StorageKey,
                        FileSizeBytes = storageResult.SizeBytes,
                        ContentType = mediaMetadata.ContentType,
                        GeneratedByModel = modelInfo.ModelId,
                        GenerationPrompt = request.Request.Prompt,
                        GeneratedAt = DateTime.UtcNow,
                        Metadata = new Dictionary<string, object>
                        {
                            ["provider"] = modelInfo.ProviderName,
                            ["model"] = modelInfo.ModelId,
                            ["index"] = index,
                            ["format"] = "b64_json"
                        },
                        CorrelationId = request.CorrelationId?.ToString() ?? string.Empty
                    });
                }
                else if (!string.IsNullOrEmpty(imageData.Url) && 
                        (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                         imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    var (url, downloadMs, storageMs) = await DownloadAndStoreImageAsync(
                        imageData.Url,
                        index,
                        request,
                        modelInfo,
                        cancellationToken);
                    finalUrl = url;
                    onTimingUpdate(downloadMs, storageMs);
                }
                
                // Report progress
                onProgress();
                
                return new ConduitLLM.Core.Events.ImageData
                {
                    Url = finalUrl,
                    B64Json = request.Request.ResponseFormat == "b64_json" ? imageData.B64Json : null,
                    RevisedPrompt = null,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = modelInfo.ProviderName,
                        ["model"] = modelInfo.ModelId,
                        ["index"] = index
                    }
                };
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<(string url, long downloadMs, long storageMs)> DownloadAndStoreImageAsync(
            string imageUrl,
            int index,
            ImageGenerationRequested request,
            ModelInfo modelInfo,
            CancellationToken cancellationToken)
        {
            var downloadStopwatch = Stopwatch.StartNew();
            var storageStopwatch = new Stopwatch();
            
            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = GetProviderTimeout(modelInfo.ProviderType);
                
                // Use streaming for better memory efficiency
                using var response = await httpClient.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                downloadStopwatch.Stop();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                        imageUrl, response.StatusCode);
                    return (imageUrl, downloadStopwatch.ElapsedMilliseconds, 0); // Return original URL as fallback
                }
                
                // Determine content type and extension
                var contentType = "image/png";
                var extension = "png";
                
                if (response.Content.Headers.ContentType != null)
                {
                    contentType = response.Content.Headers.ContentType.MediaType ?? contentType;
                    extension = contentType.Split('/').LastOrDefault() ?? "png";
                    if (extension == "jpeg") extension = "jpg";
                }
                else if (imageUrl.Contains(".jpeg", StringComparison.OrdinalIgnoreCase) || 
                         imageUrl.Contains(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    contentType = "image/jpeg";
                    extension = "jpg";
                }
                
                var metadata = new Dictionary<string, string>
                {
                    ["prompt"] = request.Request.Prompt,
                    ["model"] = modelInfo.ModelId,
                    ["provider"] = modelInfo.ProviderName,
                    ["originalUrl"] = imageUrl
                };
                
                var mediaMetadata = new MediaMetadata
                {
                    ContentType = contentType,
                    FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{index}.{extension}",
                    MediaType = MediaType.Image,
                    CustomMetadata = metadata
                };
                
                // Add CreatedBy if we have virtual key info
                if (request.VirtualKeyId > 0)
                {
                    mediaMetadata.CreatedBy = request.VirtualKeyId.ToString();
                }
                
                // Stream directly to storage
                using var imageStream = await response.Content.ReadAsStreamAsync();
                storageStopwatch.Start();
                var storageResult = await _storageService.StoreAsync(imageStream, mediaMetadata);
                storageStopwatch.Stop();
                
                _logger.LogInformation("Downloaded and stored image from {OriginalUrl} to {StorageUrl} (Download: {DownloadMs}ms, Storage: {StorageMs}ms)", 
                    imageUrl, storageResult.Url, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds);
                
                // Get file size for the event
                var contentLength = response.Content.Headers.ContentLength ?? 0;
                
                // Publish MediaGenerationCompleted event for lifecycle tracking
                await _publishEndpoint.Publish(new MediaGenerationCompleted
                {
                    MediaType = MediaType.Image,
                    VirtualKeyId = request.VirtualKeyId,
                    MediaUrl = storageResult.Url,
                    StorageKey = storageResult.StorageKey,
                    FileSizeBytes = contentLength,
                    ContentType = mediaMetadata.ContentType,
                    GeneratedByModel = modelInfo.ModelId,
                    GenerationPrompt = request.Request.Prompt,
                    GeneratedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["provider"] = modelInfo.ProviderName,
                        ["model"] = modelInfo.ModelId,
                        ["index"] = index
                    },
                    CorrelationId = request.CorrelationId
                });
                
                return (storageResult.Url, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download and store image from URL: {Url}", imageUrl);
                return (imageUrl, downloadStopwatch.ElapsedMilliseconds, storageStopwatch.ElapsedMilliseconds); // Return original URL as fallback
            }
        }

        private async Task ReportProgressAsync(
            string taskId,
            string correlationId,
            int totalImages,
            Func<int> getCompletedCount,
            string? webhookUrl,
            Dictionary<string, string>? webhookHeaders,
            CancellationToken cancellationToken)
        {
            var lastReportedCount = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
                
                var currentCount = getCompletedCount();
                if (currentCount != lastReportedCount)
                {
                    lastReportedCount = currentCount;
                    
                    await _publishEndpoint.Publish(new ImageGenerationProgress
                    {
                        TaskId = taskId,
                        Status = "storing",
                        ImagesCompleted = currentCount,
                        TotalImages = totalImages,
                        Message = $"Processed {currentCount} of {totalImages} images",
                        CorrelationId = correlationId ?? string.Empty
                    });
                    
                    // Send webhook notification if configured
                    if (!string.IsNullOrEmpty(webhookUrl))
                    {
                        var webhookPayload = new ImageProgressWebhookPayload
                        {
                            TaskId = taskId,
                            Status = "processing",
                            ImagesCompleted = currentCount,
                            TotalImages = totalImages,
                            Message = $"Processed {currentCount} of {totalImages} images"
                        };
                        
                        // Publish webhook delivery event for scalable processing
                        await _publishEndpoint.Publish(new WebhookDeliveryRequested
                        {
                            TaskId = taskId,
                            TaskType = "image",
                            WebhookUrl = webhookUrl,
                            EventType = WebhookEventType.TaskProgress,
                            PayloadJson = ConduitLLM.Core.Helpers.WebhookPayloadHelper.SerializePayload(webhookPayload),
                            Headers = webhookHeaders,
                            CorrelationId = correlationId ?? Guid.NewGuid().ToString()
                        });
                    }
                }
                
                if (currentCount >= totalImages)
                {
                    break;
                }
            }
        }

        private int GetOptimalConcurrency(string provider, int imageCount)
        {
            // Use configuration or fallback to defaults
            var maxConcurrency = _performanceConfig.ProviderConcurrencyLimits.TryGetValue(
                provider.ToLowerInvariant(), 
                out var limit) ? limit : _performanceConfig.MaxConcurrentGenerations;
            
            // Don't exceed the number of images
            return Math.Min(maxConcurrency, imageCount);
        }

        private TimeSpan GetProviderTimeout(ProviderType providerType)
        {
            // Use configuration or fallback to defaults
            var providerKey = providerType.ToString().ToLowerInvariant();
            var timeoutSeconds = _performanceConfig.ProviderDownloadTimeouts.TryGetValue(
                providerKey, 
                out var timeout) ? timeout : 30;
            
            return TimeSpan.FromSeconds(timeoutSeconds);
        }

        private class ModelInfo
        {
            public ConduitLLM.Configuration.Entities.Provider? Provider { get; set; }
            public string ModelId { get; set; } = string.Empty;
            public int ProviderId { get; set; }
            
            // Convenience property to get ProviderType from Provider
            public ProviderType ProviderType => Provider?.ProviderType ?? ProviderType.OpenAI;
            
            // Convenience property to get provider name for responses
            public string ProviderName => Provider?.ProviderName ?? Provider?.ProviderType.ToString() ?? "unknown";
        }
    }
}