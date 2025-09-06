using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Validation;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IVirtualKeyService = ConduitLLM.Configuration.Interfaces.IVirtualKeyService;
using IModelProviderMappingService = ConduitLLM.Configuration.Interfaces.IModelProviderMappingService;

namespace ConduitLLM.Core.Services.Abstractions
{
    /// <summary>
    /// Abstract base orchestrator for all media generation workflows.
    /// Implements Template Method pattern for consistent processing flow.
    /// </summary>
    /// <typeparam name="TRequest">The generation request type</typeparam>
    /// <typeparam name="TResponse">The generation response type</typeparam>
    /// <typeparam name="TEventRequest">The event request type</typeparam>
    public abstract class MediaGenerationOrchestrator<TRequest, TResponse, TEventRequest> 
        : IConsumer<TEventRequest>
        where TRequest : class
        where TResponse : class
        where TEventRequest : class
    {
        // Abstract methods to get properties from specific event types
        protected abstract string GetRequestId(TEventRequest request);
        protected abstract string GetModel(TEventRequest request);
        protected abstract string GetPrompt(TEventRequest request);
        protected abstract string GetVirtualKeyId(TEventRequest request);
        protected abstract string? GetWebhookUrl(TEventRequest request);
        protected abstract string? GetCorrelationId(TEventRequest request);
        protected abstract bool GetIsAsync(TEventRequest request);
        protected virtual Dictionary<string, string>? GetWebhookHeaders(TEventRequest request) => null;
        // Core dependencies shared across all media orchestrators
        protected readonly ILLMClientFactory _clientFactory;
        protected readonly IAsyncTaskService _taskService;
        protected readonly IMediaStorageService _storageService;
        protected readonly IPublishEndpoint _publishEndpoint;
        protected readonly IModelProviderMappingService _modelMappingService;
        protected readonly IVirtualKeyService _virtualKeyService;
        protected readonly ICostCalculationService _costService;
        protected readonly ICancellableTaskRegistry _taskRegistry;
        protected readonly IWebhookNotificationService _webhookService;
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly MinimalParameterValidator _parameterValidator;
        protected readonly ILogger _logger;

        protected MediaGenerationOrchestrator(
            ILLMClientFactory clientFactory,
            IAsyncTaskService taskService,
            IMediaStorageService storageService,
            IPublishEndpoint publishEndpoint,
            IModelProviderMappingService modelMappingService,
            IVirtualKeyService virtualKeyService,
            ICostCalculationService costService,
            ICancellableTaskRegistry taskRegistry,
            IWebhookNotificationService webhookService,
            IHttpClientFactory httpClientFactory,
            MinimalParameterValidator parameterValidator,
            ILogger logger)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            _virtualKeyService = virtualKeyService ?? throw new ArgumentNullException(nameof(virtualKeyService));
            _costService = costService ?? throw new ArgumentNullException(nameof(costService));
            _taskRegistry = taskRegistry ?? throw new ArgumentNullException(nameof(taskRegistry));
            _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _parameterValidator = parameterValidator ?? throw new ArgumentNullException(nameof(parameterValidator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Template method defining the main processing flow for media generation.
        /// </summary>
        public async Task Consume(ConsumeContext<TEventRequest> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            
            // Check if request should be processed
            if (!ShouldProcessRequest(request))
            {
                _logger.LogDebug("Skipping request {RequestId} - processing criteria not met", GetRequestId(request));
                return;
            }

            // Create linked cancellation token for this task
            using var taskCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            
            // Register task for cancellation support
            _taskRegistry.RegisterTask(GetRequestId(request), taskCts);

            try
            {
                _logger.LogInformation("Processing {MediaType} generation task {RequestId} for model {Model}", 
                    GetMediaType(), GetRequestId(request), GetModel(request));
                
                // 1. Update task status to processing
                await UpdateTaskStatusAsync(GetRequestId(request), TaskState.Processing, taskCts.Token);
                
                // 2. Publish started event
                await PublishStartedEventAsync(request);
                
                // 3. Get and validate model information
                var virtualKeyIdStr = GetVirtualKeyId(request);
                if (!int.TryParse(virtualKeyIdStr, out var virtualKeyId))
                {
                    throw new InvalidOperationException($"Invalid virtual key ID: {virtualKeyIdStr}");
                }
                var modelInfo = await GetModelInfoAsync(GetModel(request), virtualKeyId);
                if (modelInfo == null)
                {
                    throw new InvalidOperationException($"Model {GetModel(request)} not found or not available");
                }
                
                ValidateModelSupport(modelInfo, request);
                
                // 4. Extract and validate virtual key
                var virtualKey = await ExtractAndValidateVirtualKeyAsync(request);
                
                // 5. Build the generation request
                var generationRequest = await BuildGenerationRequestAsync(request, modelInfo);
                
                // 6. Validate parameters
                ValidateParameters(generationRequest);
                
                // 7. Log generation details
                LogGenerationDetails(request, modelInfo, generationRequest);
                
                // 8. Execute the actual generation
                var response = await ExecuteGenerationAsync(generationRequest, modelInfo, virtualKey, taskCts.Token);
                
                // 9. Process and store the generated media
                var processedMedia = await ProcessMediaAsync(response, request, modelInfo, virtualKey, taskCts.Token);
                
                // 10. Calculate cost
                var cost = await CalculateCostAsync(request, modelInfo, processedMedia);
                
                // 11. Update spend
                if (cost > 0)
                {
                    if (int.TryParse(GetVirtualKeyId(request), out var vkId))
                    {
                        await UpdateSpendAsync(vkId, cost, GetRequestId(request), GetCorrelationId(request));
                    }
                }
                
                // 12. Complete the task
                await CompleteTaskAsync(request, processedMedia, cost, modelInfo, stopwatch);
                
                // 13. Send webhook notification if configured
                if (!string.IsNullOrEmpty(GetWebhookUrl(request)))
                {
                    await SendWebhookNotificationAsync(request, processedMedia, stopwatch, "completed");
                }
                
                _logger.LogInformation("Completed {MediaType} generation task {RequestId} in {Duration}s",
                    GetMediaType(), GetRequestId(request), stopwatch.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (taskCts.Token.IsCancellationRequested)
            {
                await HandleCancellationAsync(request, stopwatch);
            }
            catch (Exception ex)
            {
                await HandleFailureAsync(request, ex, stopwatch);
            }
            finally
            {
                // Always unregister the task from the cancellation registry
                _taskRegistry.UnregisterTask(GetRequestId(request));
            }
        }

        // Abstract methods that MUST be implemented by derived classes
        protected abstract bool ShouldProcessRequest(TEventRequest request);
        protected abstract Task<TResponse> ExecuteGenerationAsync(TRequest request, GenerationModelInfo modelInfo, VirtualKey virtualKey, CancellationToken cancellationToken);
        protected abstract Task<ProcessedMedia> ProcessMediaAsync(TResponse response, TEventRequest request, GenerationModelInfo modelInfo, VirtualKey virtualKey, CancellationToken cancellationToken);
        protected abstract void ValidateParameters(TRequest request);
        protected abstract Task<TRequest> BuildGenerationRequestAsync(TEventRequest request, GenerationModelInfo modelInfo);
        protected abstract void ValidateModelSupport(GenerationModelInfo modelInfo, TEventRequest request);
        protected abstract Usage CreateUsageObject(TEventRequest request, ProcessedMedia media);
        protected abstract Task PublishStartedEventAsync(TEventRequest request);
        protected abstract Task PublishCompletedEventAsync(TEventRequest request, ProcessedMedia media, decimal cost, GenerationModelInfo modelInfo, TimeSpan duration);
        protected abstract Task PublishFailedEventAsync(TEventRequest request, Exception ex, bool isRetryable, int retryCount, int maxRetries);
        protected abstract Task PublishProgressEventAsync(TEventRequest request, int current, int total, string status);
        protected abstract object CreateWebhookPayload(TEventRequest request, ProcessedMedia media, TimeSpan duration, string status, string? error = null);
        protected abstract string GetMediaType();
        protected abstract void LogGenerationDetails(TEventRequest request, GenerationModelInfo modelInfo, TRequest generationRequest);

        // Virtual methods with default implementations
        protected virtual async Task<GenerationModelInfo?> GetModelInfoAsync(string modelAlias, int virtualKeyId)
        {
            // Get model mapping
            var mapping = await _modelMappingService.GetMappingByModelAliasAsync(modelAlias);
            if (mapping == null)
            {
                _logger.LogWarning("Model mapping not found for alias {ModelAlias}", modelAlias);
                return null;
            }
            
            return new GenerationModelInfo
            {
                ModelId = mapping.ProviderModelId,
                ModelAlias = mapping.ModelAlias,
                ProviderId = mapping.ProviderId,
                Provider = mapping.Provider // Use the Provider navigation property directly
            };
        }

        protected virtual async Task<VirtualKey> ExtractAndValidateVirtualKeyAsync(TEventRequest request)
        {
            // Get task to retrieve the actual virtual key from metadata
            var task = await _taskService.GetTaskStatusAsync(GetRequestId(request));
            if (task?.Metadata == null)
            {
                throw new InvalidOperationException($"Task {GetRequestId(request)} not found or has no metadata");
            }
            
            // Extract virtual key from metadata
            string? virtualKey = null;
            if (task.Metadata is TaskMetadata taskMetadata && taskMetadata.ExtensionData != null)
            {
                if (taskMetadata.ExtensionData.TryGetValue("VirtualKey", out var virtualKeyObj))
                {
                    virtualKey = virtualKeyObj switch
                    {
                        string vk => vk,
                        System.Text.Json.JsonElement jsonElement when jsonElement.ValueKind == System.Text.Json.JsonValueKind.String 
                            => jsonElement.GetString(),
                        _ => null
                    };
                }
            }
            
            if (string.IsNullOrEmpty(virtualKey))
            {
                throw new InvalidOperationException("Virtual key not found in task metadata");
            }
            
            // Validate and get virtual key info
            var virtualKeyInfo = await _virtualKeyService.GetVirtualKeyByKeyValueAsync(virtualKey);
            if (virtualKeyInfo == null || !virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Invalid or disabled virtual key");
            }
            
            return virtualKeyInfo;
        }

        protected virtual async Task<decimal> CalculateCostAsync(TEventRequest request, GenerationModelInfo modelInfo, ProcessedMedia media)
        {
            var usage = CreateUsageObject(request, media);
            return await _costService.CalculateCostAsync(modelInfo.ModelId, usage);
        }

        protected virtual bool IsRetryableError(Exception ex)
        {
            // Check exception type
            var isRetryableType = ex switch
            {
                TimeoutException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                System.IO.IOException => true,
                System.Net.Sockets.SocketException => true,
                _ => false
            };

            // Check for specific error messages
            if (!isRetryableType && ex.Message != null)
            {
                var lowerMessage = ex.Message.ToLowerInvariant();
                isRetryableType = lowerMessage.Contains("timeout") ||
                                  lowerMessage.Contains("connection") ||
                                  lowerMessage.Contains("temporarily unavailable") ||
                                  lowerMessage.Contains("rate limit");
            }

            return isRetryableType;
        }

        protected virtual async Task UpdateTaskStatusAsync(string taskId, TaskState state, CancellationToken cancellationToken)
        {
            await _taskService.UpdateTaskStatusAsync(taskId, state, cancellationToken: cancellationToken);
        }

        protected virtual async Task CompleteTaskAsync(TEventRequest request, ProcessedMedia media, decimal cost, GenerationModelInfo modelInfo, Stopwatch stopwatch)
        {
            var result = new
            {
                mediaUrl = media.Url ?? media.Items.FirstOrDefault()?.Url,
                mediaCount = media.Count,
                duration = stopwatch.Elapsed.TotalSeconds,
                cost,
                provider = modelInfo.ProviderName,
                model = modelInfo.ModelId
            };
            
            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Completed,
                progress: 100,
                result: result);
            
            await PublishCompletedEventAsync(request, media, cost, modelInfo, stopwatch.Elapsed);
        }

        protected virtual async Task HandleCancellationAsync(TEventRequest request, Stopwatch stopwatch)
        {
            _logger.LogInformation("{MediaType} generation task {RequestId} was cancelled after {Duration}ms",
                GetMediaType(), GetRequestId(request), stopwatch.ElapsedMilliseconds);
            
            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Cancelled,
                error: "Task was cancelled by user request");
            
            if (!string.IsNullOrEmpty(GetWebhookUrl(request)))
            {
                await SendWebhookNotificationAsync(request, null, stopwatch, "cancelled");
            }
        }

        protected virtual async Task HandleFailureAsync(TEventRequest request, Exception ex, Stopwatch stopwatch)
        {
            _logger.LogError(ex, "{MediaType} generation failed for task {RequestId}", 
                GetMediaType(), GetRequestId(request));
            
            // Check if error is retryable
            var isRetryable = IsRetryableError(ex);
            
            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                error: ex.Message);
            
            await PublishFailedEventAsync(request, ex, isRetryable, 0, 0);
            
            if (!string.IsNullOrEmpty(GetWebhookUrl(request)))
            {
                await SendWebhookNotificationAsync(request, null, stopwatch, "failed", ex.Message);
            }
        }

        protected virtual async Task UpdateSpendAsync(int virtualKeyId, decimal amount, string requestId, string? correlationId)
        {
            await _publishEndpoint.Publish(new SpendUpdateRequested
            {
                KeyId = virtualKeyId,
                Amount = amount,
                RequestId = requestId,
                CorrelationId = correlationId ?? string.Empty
            });
        }

        protected virtual async Task SendWebhookNotificationAsync(TEventRequest request, ProcessedMedia? media, Stopwatch stopwatch, string status, string? error = null)
        {
            var payload = CreateWebhookPayload(request, media ?? new ProcessedMedia(), stopwatch.Elapsed, status, error);
            
            var eventType = status switch
            {
                "completed" => WebhookEventType.TaskCompleted,
                "failed" => WebhookEventType.TaskFailed,
                "cancelled" => WebhookEventType.TaskCancelled,
                _ => WebhookEventType.TaskProgress
            };
            
            await _publishEndpoint.Publish(new WebhookDeliveryRequested
            {
                TaskId = GetRequestId(request),
                TaskType = GetMediaType().ToLowerInvariant(),
                WebhookUrl = GetWebhookUrl(request)!,
                EventType = eventType,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(payload),
                Headers = GetWebhookHeaders(request),
                CorrelationId = GetCorrelationId(request) ?? Guid.NewGuid().ToString()
            });
            
            _logger.LogDebug("Published webhook delivery event for {Status} {MediaType} task {RequestId}",
                status, GetMediaType(), GetRequestId(request));
        }
    }
}