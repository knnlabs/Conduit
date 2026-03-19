using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Exceptions;
using ConduitLLM.Core.Validation;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
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
        protected readonly MediaGenerationMetrics _metrics;
        protected readonly IProviderErrorTrackingService _errorTrackingService;
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
            MediaGenerationMetrics metrics,
            IProviderErrorTrackingService errorTrackingService,
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
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
            _errorTrackingService = errorTrackingService ?? throw new ArgumentNullException(nameof(errorTrackingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Template method defining the main processing flow for media generation.
        /// </summary>
        public async Task Consume(ConsumeContext<TEventRequest> context)
        {
            var request = context.Message;
            var stopwatch = Stopwatch.StartNew();
            GenerationModelInfo? modelInfo = null;

            // Check if request should be processed
            if (!ShouldProcessRequest(request))
            {
                _logger.LogDebug("Skipping request {RequestId} - processing criteria not met", GetRequestId(request));
                return;
            }

            // Start distributed tracing span for the entire generation pipeline
            using var activity = MediaGenerationMetrics.StartGenerationActivity(
                $"media.{GetMediaType().ToLowerInvariant()}.generate",
                GetMediaType(),
                GetModel(request),
                "pending"); // Provider not yet known; updated below after model resolution
            activity?.SetTag("media.request_id", GetRequestId(request));
            activity?.SetTag("media.virtual_key_id", GetVirtualKeyId(request));

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
                    throw new ArgumentException($"Virtual key ID must be a valid integer, got: {virtualKeyIdStr}");
                }
                modelInfo = await GetModelInfoAsync(GetModel(request), virtualKeyId);
                if (modelInfo == null)
                {
                    throw new InvalidOperationException($"Model '{GetModel(request)}' is not configured or mapped to a provider. Please check your model configuration.");
                }

                // Update the activity with resolved provider information
                activity?.SetTag("media.provider", modelInfo.ProviderName);
                activity?.SetTag("media.model", modelInfo.ModelId);

                ValidateModelSupport(modelInfo, request);

                // Record generation started metrics
                _metrics.RecordGenerationStarted(
                    GetMediaType(),
                    GetModel(request),
                    modelInfo.ProviderName,
                    virtualKeyIdStr);

                // Update task registry size
                _metrics.UpdateTaskRegistrySize(1);

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

                activity?.SetTag("media.cost", cost);
                activity?.SetTag("media.duration_seconds", stopwatch.Elapsed.TotalSeconds);

                _logger.LogInformation("Completed {MediaType} generation task {RequestId} in {Duration}s",
                    GetMediaType(), GetRequestId(request), stopwatch.Elapsed.TotalSeconds);
            }
            catch (OperationCanceledException) when (taskCts.Token.IsCancellationRequested)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Cancelled");
                activity?.SetTag("media.outcome", "cancelled");
                await HandleCancellationAsync(request, stopwatch, modelInfo);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("media.outcome", "failed");
                activity?.SetTag("media.error_type", ex.GetType().Name);
                await HandleFailureAsync(request, ex, stopwatch, modelInfo);
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
            VirtualKey? virtualKeyInfo = null;
            try
            {
                virtualKeyInfo = await _virtualKeyService.ValidateVirtualKeyAsync(virtualKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate virtual key for task {RequestId}", GetRequestId(request));
                throw new InvalidOperationException($"Virtual key validation failed: {ex.Message}", ex);
            }
            
            if (virtualKeyInfo == null)
            {
                throw new UnauthorizedAccessException("Virtual key validation returned null - key may not exist or service may be unavailable");
            }
            
            if (!virtualKeyInfo.IsEnabled)
            {
                throw new UnauthorizedAccessException("Virtual key is disabled");
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
            // Build data array from processed media items in OpenAI-compatible format
            // This format is expected by SDKs: { created, data: [{ url, metadata }], model, usage }
            var dataItems = new List<object>();

            if (media.Items.Any())
            {
                foreach (var item in media.Items)
                {
                    dataItems.Add(new
                    {
                        url = item.Url,
                        metadata = item.Metadata.Count > 0 ? item.Metadata : null
                    });
                }
            }
            else if (!string.IsNullOrEmpty(media.Url))
            {
                // Single item case - wrap in data array
                dataItems.Add(new
                {
                    url = media.Url,
                    metadata = media.Metadata.Count > 0 ? media.Metadata : null
                });
            }

            // Create result in OpenAI-compatible format that SDKs expect
            // Both ImageGenerationResponse and VideoGenerationResponse share this structure
            var result = new
            {
                created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                data = dataItems,
                model = modelInfo.ModelId,
                usage = new
                {
                    // Generic usage info - specific orchestrators can override if needed
                    count = media.Count,
                    duration_seconds = stopwatch.Elapsed.TotalSeconds
                },
                // Additional metadata for internal use (not part of OpenAI spec but useful)
                _metadata = new
                {
                    cost,
                    provider = modelInfo.ProviderName,
                    generation_duration_seconds = stopwatch.Elapsed.TotalSeconds
                }
            };

            // Record completion metrics
            _metrics.RecordGenerationCompleted(
                GetMediaType(),
                modelInfo.ModelId,
                modelInfo.ProviderName,
                GetVirtualKeyId(request),
                stopwatch.Elapsed.TotalSeconds,
                (double)cost);

            // Update task registry size
            _metrics.UpdateTaskRegistrySize(-1);

            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Completed,
                progress: 100,
                result: result);

            await PublishCompletedEventAsync(request, media, cost, modelInfo, stopwatch.Elapsed);
        }

        protected virtual async Task HandleCancellationAsync(TEventRequest request, Stopwatch stopwatch, GenerationModelInfo? modelInfo)
        {
            _logger.LogInformation("{MediaType} generation task {RequestId} was cancelled after {Duration}ms",
                GetMediaType(), GetRequestId(request), stopwatch.ElapsedMilliseconds);
                
            // Record cancellation metrics if model info is available
            if (modelInfo != null)
            {
                _metrics.RecordGenerationCancelled(
                    GetMediaType(),
                    modelInfo.ModelId,
                    modelInfo.ProviderName,
                    GetVirtualKeyId(request),
                    "user_request",
                    stopwatch.Elapsed.TotalSeconds);
            }
            else
            {
                // Record cancellation with minimal info if model info not available
                _metrics.RecordGenerationCancelled(
                    GetMediaType(),
                    GetModel(request),
                    "unknown",
                    GetVirtualKeyId(request),
                    "early_cancellation",
                    stopwatch.Elapsed.TotalSeconds);
            }
            
            // Update task registry size
            _metrics.UpdateTaskRegistrySize(-1);
            
            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Cancelled,
                error: "Task was cancelled by user request");
            
            if (!string.IsNullOrEmpty(GetWebhookUrl(request)))
            {
                await SendWebhookNotificationAsync(request, null, stopwatch, "cancelled");
            }
        }

        protected virtual async Task HandleFailureAsync(TEventRequest request, Exception ex, Stopwatch stopwatch, GenerationModelInfo? modelInfo)
        {
            _logger.LogError(ex, "{MediaType} generation failed for task {RequestId}", 
                GetMediaType(), GetRequestId(request));
            
            // Check if error is retryable
            var isRetryable = IsRetryableError(ex);
            
            // Categorize error for metrics
            var (errorType, errorCategory) = CategorizeError(ex);
            
            // Record failure metrics
            if (modelInfo != null)
            {
                _metrics.RecordGenerationFailed(
                    GetMediaType(),
                    modelInfo.ModelId,
                    modelInfo.ProviderName,
                    GetVirtualKeyId(request),
                    errorType,
                    errorCategory,
                    stopwatch.Elapsed.TotalSeconds,
                    isRetryable);
            }
            else
            {
                // Record failure with minimal info if model info not available
                _metrics.RecordGenerationFailed(
                    GetMediaType(),
                    GetModel(request),
                    "unknown",
                    GetVirtualKeyId(request),
                    errorType,
                    errorCategory,
                    stopwatch.Elapsed.TotalSeconds,
                    isRetryable);
            }
            
            // Update task registry size
            _metrics.UpdateTaskRegistrySize(-1);
            
            await _taskService.UpdateTaskStatusAsync(
                GetRequestId(request),
                TaskState.Failed,
                error: ex.Message);

            // Track in provider error system for dashboard visibility and auto-disable policies
            await TrackProviderErrorFromExceptionAsync(ex, modelInfo);

            await PublishFailedEventAsync(request, ex, isRetryable, 0, 0);

            if (!string.IsNullOrEmpty(GetWebhookUrl(request)))
            {
                await SendWebhookNotificationAsync(request, null, stopwatch, "failed", ex.Message);
            }
        }

        /// <summary>
        /// Tracks a provider error from an exception using the provider error tracking system.
        /// </summary>
        private async Task TrackProviderErrorFromExceptionAsync(Exception ex, GenerationModelInfo? modelInfo)
        {
            try
            {
                if (modelInfo?.Provider == null)
                {
                    _logger.LogDebug("Cannot track provider error — no provider context available");
                    return;
                }

                var keyCredentialId = modelInfo.Provider.ProviderKeyCredentials?
                    .FirstOrDefault(k => k.IsPrimary)?.Id
                    ?? modelInfo.Provider.ProviderKeyCredentials?.FirstOrDefault()?.Id;

                if (keyCredentialId == null)
                {
                    _logger.LogDebug("Cannot track provider error — no key credential found for provider {ProviderId}",
                        modelInfo.ProviderId);
                    return;
                }

                var errorType = ClassifyExceptionToProviderErrorType(ex);

                var errorInfo = new ProviderErrorInfo
                {
                    KeyCredentialId = keyCredentialId.Value,
                    ProviderId = modelInfo.ProviderId,
                    ErrorType = errorType,
                    ErrorMessage = ex.Message,
                    HttpStatusCode = (ex as LLMCommunicationException)?.StatusCode.HasValue == true
                        ? (int)(ex as LLMCommunicationException)!.StatusCode!.Value
                        : null,
                    ModelName = modelInfo.ModelId,
                    OccurredAt = DateTime.UtcNow
                };

                await _errorTrackingService.TrackErrorAsync(errorInfo);

                _logger.LogInformation("Tracked {MediaType} generation provider error: Type={ErrorType}, Provider={ProviderId}, Key={KeyCredentialId}, Model={Model}",
                    GetMediaType(), errorType, modelInfo.ProviderId, keyCredentialId, modelInfo.ModelId);
            }
            catch (Exception trackEx)
            {
                _logger.LogWarning(trackEx, "Failed to track provider error for {MediaType} generation", GetMediaType());
            }
        }

        /// <summary>
        /// Classifies an exception into a <see cref="ProviderErrorType"/> for error tracking.
        /// </summary>
        private static ProviderErrorType ClassifyExceptionToProviderErrorType(Exception ex)
        {
            return ex switch
            {
                LLMCommunicationException commEx when commEx.StatusCode.HasValue => commEx.StatusCode.Value switch
                {
                    System.Net.HttpStatusCode.Unauthorized => ProviderErrorType.InvalidApiKey,
                    System.Net.HttpStatusCode.PaymentRequired => ProviderErrorType.InsufficientBalance,
                    System.Net.HttpStatusCode.Forbidden => ProviderErrorType.AccessForbidden,
                    System.Net.HttpStatusCode.TooManyRequests => ProviderErrorType.RateLimitExceeded,
                    System.Net.HttpStatusCode.NotFound => ProviderErrorType.ModelNotFound,
                    System.Net.HttpStatusCode.ServiceUnavailable => ProviderErrorType.ServiceUnavailable,
                    System.Net.HttpStatusCode.BadGateway => ProviderErrorType.ServiceUnavailable,
                    System.Net.HttpStatusCode.GatewayTimeout => ProviderErrorType.Timeout,
                    System.Net.HttpStatusCode.RequestTimeout => ProviderErrorType.Timeout,
                    _ => ProviderErrorType.Unknown
                },
                RateLimitExceededException => ProviderErrorType.RateLimitExceeded,
                Exceptions.RequestTimeoutException => ProviderErrorType.Timeout,
                ModelNotFoundException => ProviderErrorType.ModelNotFound,
                ServiceUnavailableException => ProviderErrorType.ServiceUnavailable,
                HttpRequestException => ProviderErrorType.NetworkError,
                _ => ProviderErrorType.Unknown
            };
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

        /// <summary>
        /// Categorizes exceptions into error types and categories for metrics tracking
        /// </summary>
        protected virtual (string ErrorType, string ErrorCategory) CategorizeError(Exception ex)
        {
            return ex switch
            {
                TaskCanceledException => ("timeout", "task_timeout"),
                OperationCanceledException => ("timeout", "cancellation"),
                TimeoutException => ("timeout", "provider_timeout"),
                HttpRequestException => ("network", "http_error"),
                ArgumentException => ("validation", "parameter_error"),
                UnauthorizedAccessException => ("authentication", "auth_error"),
                InvalidOperationException => ("validation", "operation_error"),
                NotImplementedException => ("provider", "not_implemented"),
                System.Net.Sockets.SocketException => ("network", "socket_error"),
                System.IO.IOException => ("storage", "io_error"),
                OutOfMemoryException => ("resource", "memory_error"),
                _ when ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => ("rate_limit", "provider_limit"),
                _ when ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) => ("quota", "provider_quota"),
                _ when ex.Message.Contains("insufficient", StringComparison.OrdinalIgnoreCase) => ("quota", "insufficient_quota"),
                _ when ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) => ("authentication", "invalid_credentials"),
                _ when ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) => ("authentication", "access_denied"),
                _ when ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => ("validation", "resource_not_found"),
                _ when ex.Message.Contains("bad request", StringComparison.OrdinalIgnoreCase) => ("validation", "bad_request"),
                _ when ex.Message.Contains("service unavailable", StringComparison.OrdinalIgnoreCase) => ("provider", "service_unavailable"),
                _ when ex.Message.Contains("internal server", StringComparison.OrdinalIgnoreCase) => ("provider", "internal_error"),
                _ => ("unknown", "unclassified")
            };
        }
    }
}