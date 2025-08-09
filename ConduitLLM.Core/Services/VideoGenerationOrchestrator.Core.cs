using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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
    public partial class VideoGenerationOrchestrator : 
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
        private readonly IHttpClientFactory _httpClientFactory;
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
            IHttpClientFactory httpClientFactory,
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
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles video generation requested events.
        /// </summary>
        public async Task Consume(ConsumeContext<VideoGenerationRequested> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("VideoGenerationOrchestrator received event for request {RequestId}, IsAsync: {IsAsync}, Model: {Model}", 
                request.RequestId, request.IsAsync, request.Model);
            
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
                
                // Declare originalModelAlias at the beginning for proper scope
                string originalModelAlias = request.Model;
                
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
                string? virtualKey = null;
                try
                {
                    if (task.Metadata is TaskMetadata taskMetadata)
                    {
                        // The VirtualKey is stored in the ExtensionData dictionary
                        // When deserialized from JSON, the values might be JsonElement objects
                        
                        if (taskMetadata.ExtensionData != null)
                        {
                            _logger.LogDebug("ExtensionData has {Count} keys: {Keys}", 
                                taskMetadata.ExtensionData.Count, 
                                string.Join(", ", taskMetadata.ExtensionData.Keys));
                            
                            // Try to get VirtualKey from ExtensionData
                            if (taskMetadata.ExtensionData.TryGetValue("VirtualKey", out var virtualKeyObj))
                            {
                                // Handle different types the value might be
                                if (virtualKeyObj is string vk)
                                {
                                    virtualKey = vk;
                                    _logger.LogInformation("Extracted virtual key as string from ExtensionData");
                                }
                                else if (virtualKeyObj is System.Text.Json.JsonElement jsonElement)
                                {
                                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.String)
                                    {
                                        virtualKey = jsonElement.GetString();
                                        _logger.LogInformation("Extracted virtual key as JsonElement string from ExtensionData");
                                    }
                                    else
                                    {
                                        _logger.LogError("VirtualKey in ExtensionData is JsonElement but not a string. ValueKind: {ValueKind}", jsonElement.ValueKind);
                                    }
                                }
                                else
                                {
                                    _logger.LogError("VirtualKey in ExtensionData is of unexpected type: {Type}", virtualKeyObj?.GetType().FullName ?? "null");
                                }
                            }
                            else
                            {
                                _logger.LogError("VirtualKey not found in ExtensionData. Available keys: {Keys}", 
                                    string.Join(", ", taskMetadata.ExtensionData.Keys));
                            }
                        }
                        else
                        {
                            _logger.LogError("ExtensionData is null for task {TaskId}", request.RequestId);
                        }
                        
                        if (string.IsNullOrEmpty(virtualKey))
                        {
                            _logger.LogError("Failed to extract virtual key from task metadata. VirtualKeyId: {VirtualKeyId}", 
                                taskMetadata.VirtualKeyId);
                            throw new InvalidOperationException($"Virtual key not found in task metadata. VirtualKeyId: {taskMetadata.VirtualKeyId}");
                        }
                    }
                    else
                    {
                        _logger.LogError("Task metadata is not of type TaskMetadata. Actual type: {MetadataType}", 
                            task.Metadata?.GetType().FullName ?? "null");
                        throw new InvalidOperationException($"Invalid task metadata type: {task.Metadata?.GetType().FullName ?? "null"}");
                    }
                }
                catch (Exception ex) when (!(ex is InvalidOperationException))
                {
                    _logger.LogError(ex, "Failed to extract virtual key from task metadata");
                    throw new InvalidOperationException("Failed to extract virtual key from task metadata", ex);
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

        private class ModelInfo
        {
            public string ModelId { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public string ModelAlias { get; set; } = string.Empty;
        }
    }
}