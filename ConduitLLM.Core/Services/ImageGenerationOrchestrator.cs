using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// Orchestrates image generation tasks by consuming events and managing the generation lifecycle.
    /// </summary>
    public class ImageGenerationOrchestrator : IConsumer<ImageGenerationRequested>
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAsyncTaskService _taskService;
        private readonly IMediaStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IProviderDiscoveryService _discoveryService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IHttpClientFactory _httpClientFactory;
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
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<ImageGenerationRequested> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Processing image generation task {TaskId} for prompt: {Prompt}", 
                    request.TaskId, request.Request.Prompt);
                
                // Update task status to processing
                await _taskService.UpdateTaskStatusAsync(request.TaskId, TaskState.Processing);
                
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
                var modelInfo = await GetModelInfoAsync(request.Request.Model, request.VirtualKeyHash);
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
                
                // Generate images
                var response = await client.CreateImageAsync(generationRequest);
                
                // Process and store images
                var processedImages = new List<ConduitLLM.Core.Events.ImageData>();
                var totalImages = response.Data?.Count ?? 0;
                
                for (int i = 0; i < totalImages; i++)
                {
                    var imageData = response.Data![i];
                    
                    // Publish progress
                    await _publishEndpoint.Publish(new ImageGenerationProgress
                    {
                        TaskId = request.TaskId,
                        Status = "storing",
                        ImagesCompleted = i,
                        TotalImages = totalImages,
                        Message = $"Processing image {i + 1} of {totalImages}",
                        CorrelationId = request.CorrelationId
                    });
                    
                    // Store image if needed
                    string? finalUrl = imageData.Url;
                    
                    if (!string.IsNullOrEmpty(imageData.B64Json))
                    {
                        // Store base64 image
                        var imageBytes = Convert.FromBase64String(imageData.B64Json);
                        var metadata = new Dictionary<string, string>
                        {
                            ["prompt"] = request.Request.Prompt,
                            ["model"] = modelInfo.ModelId,
                            ["provider"] = modelInfo.Provider
                        };
                        
                        var mediaMetadata = new MediaMetadata
                        {
                            ContentType = "image/png",
                            FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.png",
                            MediaType = MediaType.Image,
                            CustomMetadata = metadata
                        };
                        
                        using var imageStream = new System.IO.MemoryStream(imageBytes);
                        var storageResult = await _storageService.StoreAsync(imageStream, mediaMetadata);
                        finalUrl = storageResult.Url;
                        
                        // Publish MediaGenerationCompleted event for lifecycle tracking
                        await _publishEndpoint.Publish(new MediaGenerationCompleted
                        {
                            MediaType = MediaType.Image,
                            VirtualKeyId = request.VirtualKeyId,
                            MediaUrl = storageResult.Url,
                            StorageKey = storageResult.StorageKey,
                            FileSizeBytes = imageBytes.Length,
                            ContentType = mediaMetadata.ContentType,
                            GeneratedByModel = modelInfo.ModelId,
                            GenerationPrompt = request.Request.Prompt,
                            GeneratedAt = DateTime.UtcNow,
                            Metadata = new Dictionary<string, object>
                            {
                                ["provider"] = modelInfo.Provider,
                                ["model"] = modelInfo.ModelId,
                                ["index"] = i,
                                ["format"] = "b64_json"
                            },
                            CorrelationId = request.CorrelationId
                        });
                    }
                    else if (!string.IsNullOrEmpty(imageData.Url) && 
                            (imageData.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                             imageData.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Download and store URL-based images
                        try
                        {
                            using var httpClient = _httpClientFactory.CreateClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(30);
                            
                            var imageResponse = await httpClient.GetAsync(imageData.Url);
                            if (imageResponse.IsSuccessStatusCode)
                            {
                                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
                                
                                // Determine content type and extension
                                var contentType = "image/png";
                                var extension = "png";
                                
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
                                
                                var metadata = new Dictionary<string, string>
                                {
                                    ["prompt"] = request.Request.Prompt,
                                    ["model"] = modelInfo.ModelId,
                                    ["provider"] = modelInfo.Provider,
                                    ["originalUrl"] = imageData.Url
                                };
                                
                                var mediaMetadata = new MediaMetadata
                                {
                                    ContentType = contentType,
                                    FileName = $"generated_{DateTime.UtcNow:yyyyMMddHHmmss}_{i}.{extension}",
                                    MediaType = MediaType.Image,
                                    CustomMetadata = metadata
                                };
                                
                                // Add CreatedBy if we have virtual key info
                                if (request.VirtualKeyId > 0)
                                {
                                    mediaMetadata.CreatedBy = request.VirtualKeyId.ToString();
                                }
                                
                                using var imageStream = new System.IO.MemoryStream(imageBytes);
                                var storageResult = await _storageService.StoreAsync(imageStream, mediaMetadata);
                                finalUrl = storageResult.Url;
                                
                                _logger.LogInformation("Downloaded and stored image from {OriginalUrl} to {StorageUrl}", 
                                    imageData.Url, finalUrl);
                                
                                // Publish MediaGenerationCompleted event for lifecycle tracking
                                await _publishEndpoint.Publish(new MediaGenerationCompleted
                                {
                                    MediaType = MediaType.Image,
                                    VirtualKeyId = request.VirtualKeyId,
                                    MediaUrl = storageResult.Url,
                                    StorageKey = storageResult.StorageKey,
                                    FileSizeBytes = imageBytes.Length,
                                    ContentType = mediaMetadata.ContentType,
                                    GeneratedByModel = modelInfo.ModelId,
                                    GenerationPrompt = request.Request.Prompt,
                                    GeneratedAt = DateTime.UtcNow,
                                    Metadata = new Dictionary<string, object>
                                    {
                                        ["provider"] = modelInfo.Provider,
                                        ["model"] = modelInfo.ModelId,
                                        ["index"] = i
                                    },
                                    CorrelationId = request.CorrelationId
                                });
                            }
                            else
                            {
                                _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", 
                                    imageData.Url, imageResponse.StatusCode);
                                // Keep original URL as fallback
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to download and store image from URL: {Url}", imageData.Url);
                            // Keep original URL as fallback
                        }
                    }
                    
                    processedImages.Add(new ConduitLLM.Core.Events.ImageData
                    {
                        Url = finalUrl,
                        B64Json = request.Request.ResponseFormat == "b64_json" ? imageData.B64Json : null,
                        RevisedPrompt = null, // Core.Models.ImageData doesn't have this property
                        Metadata = new Dictionary<string, object>
                        {
                            ["provider"] = modelInfo.Provider,
                            ["model"] = modelInfo.ModelId,
                            ["index"] = i
                        }
                    });
                }
                
                stopwatch.Stop();
                
                // Calculate cost (simplified - would need provider-specific pricing)
                var cost = CalculateImageGenerationCost(modelInfo.Provider, modelInfo.ModelId, totalImages);
                
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
                        provider = modelInfo.Provider,
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
                    Provider = modelInfo.Provider,
                    Model = modelInfo.ModelId,
                    CorrelationId = request.CorrelationId
                });
                
                // Update spend
                if (cost > 0)
                {
                    await _publishEndpoint.Publish(new SpendUpdateRequested
                    {
                        KeyId = request.VirtualKeyId,
                        Amount = cost,
                        RequestId = request.TaskId,
                        CorrelationId = request.CorrelationId
                    });
                }
                
                _logger.LogInformation("Completed image generation task {TaskId} in {Duration}s with {Count} images",
                    request.TaskId, stopwatch.Elapsed.TotalSeconds, processedImages.Count);
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
                
                // Re-throw to let MassTransit handle retry logic
                throw;
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
            
            return new ModelInfo
            {
                Provider = mapping.ProviderName,
                ModelId = mapping.ProviderModelId,
                ProviderCredentialId = 0 // We don't have this in the mapping
            };
        }

        private decimal CalculateImageGenerationCost(string provider, string model, int imageCount)
        {
            // Simplified cost calculation - in production would use provider-specific pricing
            return provider.ToLowerInvariant() switch
            {
                "openai" => model switch
                {
                    "dall-e-3" => 0.040m * imageCount, // $0.040 per image for standard
                    "dall-e-2" => 0.020m * imageCount, // $0.020 per image
                    _ => 0.030m * imageCount
                },
                "minimax" => 0.010m * imageCount, // Estimated
                "replicate" => 0.025m * imageCount, // Varies by model
                _ => 0.020m * imageCount // Default estimate
            };
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

        private class ModelInfo
        {
            public string Provider { get; set; } = string.Empty;
            public string ModelId { get; set; } = string.Empty;
            public int ProviderCredentialId { get; set; }
        }
    }
}