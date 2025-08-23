using System.Diagnostics;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;
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
    public partial class ImageGenerationOrchestrator : IConsumer<ImageGenerationRequested>, IConsumer<ImageGenerationCancelled>
    {
        private readonly ILLMClientFactory _clientFactory;
        private readonly IAsyncTaskService _taskService;
        private readonly IMediaStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IModelProviderMappingService _modelMappingService;
        private readonly IVirtualKeyService _virtualKeyService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICancellableTaskRegistry _taskRegistry;
        private readonly ICostCalculationService _costCalculationService;
        private readonly IProviderService _providerService;
        private readonly ImageGenerationPerformanceConfiguration _performanceConfig;
        private readonly MinimalParameterValidator _parameterValidator;
        private readonly ILogger<ImageGenerationOrchestrator> _logger;

        public ImageGenerationOrchestrator(
            ILLMClientFactory clientFactory,
            IAsyncTaskService taskService,
            IMediaStorageService storageService,
            IPublishEndpoint publishEndpoint,
            IModelProviderMappingService modelMappingService,
            IVirtualKeyService virtualKeyService,
            IHttpClientFactory httpClientFactory,
            ICancellableTaskRegistry taskRegistry,
            ICostCalculationService costCalculationService,
            IProviderService providerService,
            IOptions<ImageGenerationPerformanceConfiguration> performanceOptions,
            MinimalParameterValidator parameterValidator,
            ILogger<ImageGenerationOrchestrator> logger)
        {
            _clientFactory = clientFactory;
            _taskService = taskService;
            _storageService = storageService;
            _publishEndpoint = publishEndpoint;
            _modelMappingService = modelMappingService;
            _virtualKeyService = virtualKeyService;
            _httpClientFactory = httpClientFactory;
            _taskRegistry = taskRegistry;
            _costCalculationService = costCalculationService;
            _providerService = providerService;
            _performanceConfig = performanceOptions.Value;
            _parameterValidator = parameterValidator;
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
                    User = request.Request.User,
                    ExtensionData = request.Request.ExtensionData // Pass through any additional parameters
                };
                
                // Validate parameters (minimal, provider-agnostic)
                _parameterValidator.ValidateImageParameters(generationRequest);
                
                _logger.LogInformation("Generating {Count} images with {Provider} using model {Model}", 
                    generationRequest.N, modelInfo.Provider, modelInfo.ModelId);
                
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

        public async Task Consume(ConsumeContext<ImageGenerationCancelled> context)
        {
            var request = context.Message;
            
            try
            {
                _logger.LogInformation("Processing image generation cancellation for task {TaskId}", request.TaskId);
                
                // Signal cancellation to the running task if it exists
                _taskRegistry.TryCancel(request.TaskId);
                
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    request.TaskId,
                    TaskState.Cancelled,
                    error: request.Reason ?? "Cancelled by user request");
                
                // Publish cancellation acknowledgement event
                await _publishEndpoint.Publish(new ImageGenerationProgress
                {
                    TaskId = request.TaskId,
                    Status = "cancelled",
                    ImagesCompleted = 0,
                    TotalImages = 0,
                    Message = "Task cancelled",
                    CorrelationId = request.CorrelationId
                });
                
                _logger.LogInformation("Successfully processed cancellation for image generation task {TaskId}", request.TaskId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image generation cancellation for task {TaskId}", request.TaskId);
                // Don't re-throw - cancellation is best effort
            }
        }
    }
}
