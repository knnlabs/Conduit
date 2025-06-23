using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

            try
            {
                _logger.LogInformation("Processing async video generation task {RequestId} for model {Model}", 
                    request.RequestId, request.Model);
                
                // Update task status to running
                await _taskService.UpdateTaskStatusAsync(request.RequestId, TaskState.Running, cancellationToken: context.CancellationToken);
                
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
                
                // Validate virtual key
                var virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(request.VirtualKeyId, request.Model);
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
                
                // TODO: In future phases, implement actual provider-specific video generation
                // For now, we'll simulate the process
                await SimulateVideoGenerationAsync(request, modelInfo, virtualKeyInfo, stopwatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Video generation failed for request {RequestId}", request.RequestId);
                
                // Update task status to failed
                await _taskService.UpdateTaskStatusAsync(request.RequestId, TaskState.Failed, error: ex.Message);
                
                // Publish failure event
                await _publishEndpoint.Publish(new VideoGenerationFailed
                {
                    RequestId = request.RequestId,
                    Error = ex.Message,
                    ErrorCode = ex.GetType().Name,
                    IsRetryable = IsRetryableError(ex),
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
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    cancellation.RequestId, 
                    TaskState.Cancelled, 
                    error: cancellation.Reason ?? "User requested cancellation");
                
                // TODO: Implement actual cancellation logic with providers
                // For now, just mark as cancelled
                
                _logger.LogInformation("Successfully cancelled video generation task {RequestId}", 
                    cancellation.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel video generation task {RequestId}", 
                    cancellation.RequestId);
            }
        }

        /// <summary>
        /// Simulates video generation for demonstration purposes.
        /// This will be replaced with actual provider integration in future phases.
        /// </summary>
        private async Task SimulateVideoGenerationAsync(
            VideoGenerationRequested request,
            ModelInfo modelInfo,
            ConduitLLM.Configuration.Entities.VirtualKey virtualKeyInfo,
            Stopwatch stopwatch)
        {
            // Simulate progress updates
            for (int progress = 10; progress <= 90; progress += 20)
            {
                // Check if cancelled
                var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
                if (taskStatus?.State == TaskState.Cancelled)
                {
                    _logger.LogInformation("Video generation cancelled for request {RequestId}", request.RequestId);
                    return;
                }
                
                await Task.Delay(1000); // Simulate processing time
                
                await _publishEndpoint.Publish(new VideoGenerationProgress
                {
                    RequestId = request.RequestId,
                    ProgressPercentage = progress,
                    Status = GetProgressStatus(progress),
                    Message = GetProgressMessage(progress),
                    CorrelationId = request.CorrelationId
                });
            }
            
            // Simulate successful completion
            var videoUrl = $"https://storage.conduitllm.com/videos/simulated_{request.RequestId}.mp4";
            var previewUrl = $"https://storage.conduitllm.com/previews/simulated_{request.RequestId}.jpg";
            
            // Calculate simulated cost
            var cost = await CalculateVideoCostAsync(request, modelInfo);
            
            // Update spend tracking
            await _virtualKeyService.UpdateSpendAsync(virtualKeyInfo.Id, cost);
            
            // Update task status to completed
            await _taskService.UpdateTaskStatusAsync(request.RequestId, TaskState.Completed, result: videoUrl);
            
            // Publish completion event
            await _publishEndpoint.Publish(new VideoGenerationCompleted
            {
                RequestId = request.RequestId,
                VideoUrl = videoUrl,
                PreviewUrl = previewUrl,
                Duration = request.Parameters?.Duration ?? 5,
                Resolution = request.Parameters?.Size ?? "1280x720",
                FileSize = 10_485_760, // 10MB simulated
                GenerationDuration = stopwatch.Elapsed,
                Cost = cost,
                Provider = modelInfo.Provider,
                Model = request.Model,
                CompletedAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
            
            _logger.LogInformation("Video generation completed for request {RequestId} in {Duration}ms", 
                request.RequestId, stopwatch.ElapsedMilliseconds);
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

        private string GetProgressStatus(int percentage)
        {
            return percentage switch
            {
                <= 20 => "initializing",
                <= 40 => "processing_frames",
                <= 60 => "rendering",
                <= 80 => "encoding",
                _ => "finalizing"
            };
        }

        private string GetProgressMessage(int percentage)
        {
            return percentage switch
            {
                <= 20 => "Initializing video generation pipeline",
                <= 40 => "Processing frames based on prompt",
                <= 60 => "Rendering video content",
                <= 80 => "Encoding video to final format",
                _ => "Finalizing and preparing for delivery"
            };
        }

        private bool IsRetryableError(Exception ex)
        {
            return ex switch
            {
                TimeoutException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                _ => false
            };
        }

        private class ModelInfo
        {
            public string ModelId { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string ModelAlias { get; set; } = string.Empty;
        }
    }
}