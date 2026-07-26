using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ConduitLLM.Configuration.Entities;
using ConduitLLM.Core.Configuration;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;
using IModelProviderMappingService = ConduitLLM.Configuration.Interfaces.IModelProviderMappingService;
using ConduitLLM.Core.Events;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Metrics;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Messaging;
using ConduitLLM.Core.Services.Abstractions;
using ConduitLLM.Core.Services.Strategies;
using ConduitLLM.Core.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConduitLLM.Core.Services
{
    /// <summary>
    /// V2 implementation of video generation orchestrator using the generic base class.
    /// </summary>
    public class VideoGenerationOrchestrator : MediaGenerationOrchestrator<
        VideoGenerationRequest,
        VideoGenerationResponse,
        VideoGenerationRequested>,
        IEventHandler<VideoGenerationCancelled>
    {
        private readonly VideoGenerationRetryConfiguration _retryConfiguration;
        private readonly IMediaProcessingStrategy<VideoData> _base64Processor;
        private readonly IMediaProcessingStrategy<VideoData> _urlProcessor;

        // Implement abstract property accessors
        protected override string GetRequestId(VideoGenerationRequested request) => request.RequestId;
        protected override string GetModel(VideoGenerationRequested request) => request.Model;
        protected override string GetPrompt(VideoGenerationRequested request) => request.Prompt;
        protected override string GetVirtualKeyId(VideoGenerationRequested request) => request.VirtualKeyId;
        protected override string? GetWebhookUrl(VideoGenerationRequested request) => request.WebhookUrl;
        protected override string? GetCorrelationId(VideoGenerationRequested request) => request.CorrelationId;
        protected override bool GetIsAsync(VideoGenerationRequested request) => request.IsAsync;

        public VideoGenerationOrchestrator(
            ILLMClientFactory clientFactory,
            IAsyncTaskService taskService,
            IMediaStorageService storageService,
            IEventBus eventBus,
            IModelProviderMappingService modelMappingService,
            IVirtualKeyService virtualKeyService,
            ICostCalculationService costService,
            ICancellableTaskRegistry taskRegistry,
            IWebhookNotificationService webhookService,
            IOptions<VideoGenerationRetryConfiguration> retryConfiguration,
            IHttpClientFactory httpClientFactory,
            MinimalParameterValidator parameterValidator,
            MediaGenerationMetrics metrics,
            IProviderErrorTrackingService errorTrackingService,
            ILogger<VideoGenerationOrchestrator> logger,
            ConduitLLM.Configuration.Interfaces.IBatchSpendUpdateService? batchSpendService = null)
            : base(clientFactory, taskService, storageService, eventBus,
                   modelMappingService, virtualKeyService, costService, taskRegistry,
                   webhookService, httpClientFactory, parameterValidator, metrics,
                   errorTrackingService, logger, batchSpendService)
        {
            _retryConfiguration = retryConfiguration?.Value ?? new VideoGenerationRetryConfiguration();

            // Initialize processing strategies
            _base64Processor = new Base64MediaProcessor(storageService, eventBus,
                logger as ILogger<Base64MediaProcessor> ?? new NullLogger<Base64MediaProcessor>());
            _urlProcessor = new UrlMediaProcessor(httpClientFactory, storageService, eventBus,
                logger as ILogger<UrlMediaProcessor> ?? new NullLogger<UrlMediaProcessor>());
        }


        protected override bool ShouldProcessRequest(VideoGenerationRequested request)
        {
            // Only process async video requests
            return request.IsAsync;
        }

        protected override async Task<VideoGenerationResponse> ExecuteGenerationAsync(
            VideoGenerationRequest request,
            GenerationModelInfo modelInfo,
            VirtualKey virtualKey,
            CancellationToken cancellationToken)
        {
            // Get the client via the already-resolved provider instead of re-resolving the alias
            var client = await _clientFactory.GetClientByProviderIdAsync(modelInfo.ProviderId, modelInfo.ModelId, cancellationToken);
            if (client == null)
            {
                throw new NotSupportedException($"No provider available for model {modelInfo.ModelAlias}");
            }

            // Video generation is not part of ILLMClient — only specific provider clients
            // implement CreateVideoAsync. Unwrap the decorator chain to the innermost provider
            // client to check the capability; decorators would otherwise hide it (issue #976).
            var innermostClient = client.UnwrapInnermost();
            var innermostType = innermostClient.GetType();

            var supportsVideo = innermostType.GetMethods()
                .Any(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3);

            if (!supportsVideo)
            {
                throw new NotSupportedException($"Provider for model {modelInfo.ModelAlias} does not support video generation");
            }

            // Set up the progress callback for any provider client that exposes SetProgressCallback
            // (e.g. MiniMax, OpenRouter), not just MiniMax by name.
            if (innermostType.GetMethod("SetProgressCallback") != null)
            {
                SetupProgressCallback(innermostClient, request.Model, cancellationToken);
            }

            // Invoke through the outermost client in the chain that exposes CreateVideoAsync so
            // decorators (e.g. ContextAwareLLMClient's key context and error tracking) still run.
            object invocationTarget = innermostClient;
            MethodInfo? createVideoMethod = null;
            for (ILLMClient? current = client; current != null;
                 current = (current as ILLMClientDecorator)?.InnerClient)
            {
                var method = current.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "CreateVideoAsync" && m.GetParameters().Length == 3);
                if (method != null)
                {
                    invocationTarget = current;
                    createVideoMethod = method;
                    break;
                }
            }

            if (createVideoMethod == null)
            {
                throw new NotSupportedException($"Provider for model {modelInfo.ModelAlias} does not support video generation");
            }

            // Invoke video generation
            var task = createVideoMethod.Invoke(invocationTarget, new object?[] { request, null, cancellationToken })
                as Task<VideoGenerationResponse>;

            if (task == null)
            {
                throw new InvalidOperationException($"CreateVideoAsync method on {invocationTarget.GetType().Name} did not return expected Task<VideoGenerationResponse>");
            }

            return await task;
        }

        private void SetupProgressCallback(object client, string requestId, CancellationToken cancellationToken)
        {
            var clientType = client.GetType();
            var setCallbackMethod = clientType.GetMethod("SetProgressCallback");
            if (setCallbackMethod != null)
            {
                Func<string, string, int, Task> progressCallback = async (taskId, status, progressPercentage) =>
                {
                    _logger.LogInformation("Video generation progress for {TaskId}: {Status} at {Progress}%",
                        taskId, status, progressPercentage);

                    // Update task progress
                    await _taskService.UpdateTaskStatusAsync(
                        requestId,
                        TaskState.Processing,
                        progress: progressPercentage);

                    // Publish progress event
                    await _eventBus.PublishAsync(new VideoGenerationProgress
                    {
                        RequestId = requestId,
                        ProgressPercentage = progressPercentage,
                        Status = status,
                        Message = $"Video generation {status.ToLowerInvariant()}",
                        CorrelationId = requestId
                    });
                };

                setCallbackMethod.Invoke(client, new object[] { progressCallback });
                _logger.LogDebug("Set video progress callback for MiniMax client");
            }
        }

        protected override async Task<ProcessedMedia> ProcessMediaAsync(
            VideoGenerationResponse response,
            VideoGenerationRequested request,
            GenerationModelInfo modelInfo,
            VirtualKey virtualKey,
            CancellationToken cancellationToken)
        {
            var processedMedia = new ProcessedMedia();

            if (response.Data == null || !response.Data.Any())
            {
                return processedMedia;
            }

            // Process each video (usually just one)
            var tasks = response.Data.Select(async (videoData, index) =>
            {
                var context = new MediaProcessingContext
                {
                    MediaType = MediaType.Video,
                    Index = index,
                    ModelInfo = modelInfo,
                    Prompt = request.Prompt,
                    VirtualKeyId = virtualKey.Id,
                    RequestId = request.RequestId,
                    CorrelationId = request.CorrelationId
                };

                // Choose appropriate processor
                IMediaProcessingStrategy<VideoData> processor;
                if (!string.IsNullOrEmpty(videoData.B64Json))
                {
                    processor = _base64Processor;
                }
                else if (!string.IsNullOrEmpty(videoData.Url))
                {
                    processor = _urlProcessor;
                }
                else
                {
                    _logger.LogWarning("Video data has neither B64Json nor Url");
                    return null;
                }

                try
                {
                    // Safe cast since we know the processors handle VideoData
                    var typedProcessor = processor as IMediaProcessingStrategy<object>;
                    if (typedProcessor != null)
                    {
                        return await typedProcessor.ProcessAsync(videoData, context, cancellationToken);
                    }
                    return null;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process video");
                    return null;
                }
            });

            var results = await Task.WhenAll(tasks);
            processedMedia.Items = results.Where(r => r != null).ToList()!;

            // Preserve provider-reported output metadata after media processing. The storage
            // strategies add operational metadata but do not carry VideoData.Metadata forward;
            // billing and completion events need the delivered values rather than request defaults.
            foreach (var item in processedMedia.Items)
            {
                var providerMetadata = response.Data.ElementAtOrDefault(item.Index)?.Metadata;
                if (providerMetadata == null)
                {
                    continue;
                }

                if (providerMetadata.Duration > 0)
                {
                    item.Metadata["duration"] = providerMetadata.Duration;
                }

                if (providerMetadata.Width > 0 && providerMetadata.Height > 0)
                {
                    item.Metadata["resolution"] = $"{providerMetadata.Width}x{providerMetadata.Height}";
                }

                if (providerMetadata.FileSizeBytes > 0)
                {
                    item.Metadata["fileSize"] = providerMetadata.FileSizeBytes;
                }
            }

            // Set primary URL to first video
            if (processedMedia.Items.Any())
            {
                processedMedia.Url = processedMedia.Items.First().Url;
            }

            return processedMedia;
        }

        protected override void ValidateParameters(VideoGenerationRequest request)
        {
            _parameterValidator.ValidateVideoParameters(request);
        }

        protected override async Task<VideoGenerationRequest> BuildGenerationRequestAsync(
            VideoGenerationRequested request,
            GenerationModelInfo modelInfo)
        {
            // Extract request from task metadata if available
            var taskStatus = await _taskService.GetTaskStatusAsync(request.RequestId);
            if (taskStatus?.Metadata is TaskMetadata taskMetadata && taskMetadata.ExtensionData != null)
            {
                // Try to extract the full request from metadata
                if (taskMetadata.ExtensionData.TryGetValue("Request", out var requestObj))
                {
                    if (requestObj is VideoGenerationRequest req)
                    {
                        req.Model = modelInfo.ModelId;
                        return req;
                    }
                    else if (requestObj is System.Text.Json.JsonElement jsonReq)
                    {
                        var videoRequest = System.Text.Json.JsonSerializer.Deserialize<VideoGenerationRequest>(jsonReq.GetRawText());
                        if (videoRequest != null)
                        {
                            videoRequest.Model = modelInfo.ModelId;
                            return videoRequest;
                        }
                    }
                }
            }

            // Fallback to constructing from event parameters
            return new VideoGenerationRequest
            {
                Model = modelInfo.ModelId,
                Prompt = request.Prompt,
                Duration = request.Parameters?.Duration,
                Size = request.Parameters?.Size,
                Fps = request.Parameters?.Fps,
                N = 1
            };
        }

        protected override void ValidateModelSupport(GenerationModelInfo modelInfo, VideoGenerationRequested request)
        {
            // Check if model supports video generation
            // This would be checked via model capabilities in the database
            // For now, we'll pass through
        }

        protected override Usage CreateUsageObject(VideoGenerationRequested request, VideoGenerationResponse response)
        {
            var providerMetadata = response.Data?.FirstOrDefault()?.Metadata;
            var resolution = providerMetadata is { Width: > 0, Height: > 0 }
                ? $"{providerMetadata.Width}x{providerMetadata.Height}"
                : request.Parameters?.Size ?? "1280x720";
            var duration = providerMetadata?.Duration > 0
                ? providerMetadata.Duration
                : response.Usage?.TotalDurationSeconds > 0
                    ? response.Usage.TotalDurationSeconds
                    : request.Parameters?.Duration ?? 5;

            // Build pricing parameters for rules-based pricing
            var pricingParameters = new Dictionary<string, object>
            {
                ["resolution"] = Utilities.VideoUtils.NormalizeResolution(resolution),
                ["duration"] = duration
            };

            // Add optional parameters that may affect pricing
            if (request.Parameters?.Fps.HasValue == true)
            {
                pricingParameters["fps"] = request.Parameters.Fps.Value;
            }

            if (!string.IsNullOrEmpty(request.Parameters?.Style))
            {
                pricingParameters["style"] = request.Parameters.Style;
            }

            // Include provider-specific options that may affect pricing (e.g., audio, aspect_ratio)
            if (request.Parameters?.ProviderOptions != null)
            {
                foreach (var option in request.Parameters.ProviderOptions)
                {
                    // Copy pricing-relevant options
                    var key = option.Key.ToLowerInvariant();
                    if (key is "audio" or "with_audio" or "aspect_ratio" or "quality" or "motion_bucket_id")
                    {
                        pricingParameters[key] = option.Value;
                    }
                }
            }

            return new Usage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                VideoDurationSeconds = duration,
                VideoResolution = resolution,
                PricingParameters = pricingParameters
            };
        }

        protected override Usage CreateEstimatedUsageObject(VideoGenerationRequested request)
        {
            return CreateUsageObject(request, new VideoGenerationResponse());
        }

        protected override async Task PublishStartedEventAsync(VideoGenerationRequested request)
        {
            await _eventBus.PublishAsync(new VideoGenerationStarted
            {
                RequestId = request.RequestId,
                Provider = "pending",
                StartedAt = DateTime.UtcNow,
                EstimatedSeconds = 60,
                CorrelationId = request.CorrelationId
            });
        }

        protected override async Task PublishCompletedEventAsync(
            VideoGenerationRequested request,
            ProcessedMedia media,
            decimal cost,
            GenerationModelInfo modelInfo,
            TimeSpan duration)
        {
            // Fall back to request values only when the provider did not report delivered metadata.
            double videoDuration = request.Parameters?.Duration ?? 5;
            var resolution = request.Parameters?.Size ?? "1280x720";

            // Extract file size from processed media metadata if available
            long fileSize = 0;
            string? previewUrl = null;
            if (media.Items?.Any() == true)
            {
                var firstItem = media.Items.First();
                if (firstItem.Metadata.TryGetValue("duration", out var durationObj) &&
                    durationObj is double actualDuration && actualDuration > 0)
                {
                    videoDuration = actualDuration;
                }
                if (firstItem.Metadata.TryGetValue("resolution", out var resolutionObj) &&
                    resolutionObj is string actualResolution && !string.IsNullOrWhiteSpace(actualResolution))
                {
                    resolution = actualResolution;
                }
                if (firstItem.Metadata.TryGetValue("fileSize", out var fileSizeObj) && fileSizeObj is long fs)
                {
                    fileSize = fs;
                }
                if (firstItem.Metadata.TryGetValue("thumbnailUrl", out var thumbObj) && thumbObj is string thumb)
                {
                    previewUrl = thumb;
                }
            }

            await _eventBus.PublishAsync(new VideoGenerationCompleted
            {
                RequestId = request.RequestId,
                VideoUrl = media.Url ?? string.Empty,
                PreviewUrl = previewUrl,
                Duration = videoDuration,
                Resolution = resolution,
                FileSize = fileSize,
                GenerationDuration = duration,
                Cost = cost,
                Provider = modelInfo.ProviderName,
                Model = request.Model,
                CompletedAt = DateTime.UtcNow,
                CorrelationId = request.CorrelationId
            });
        }

        protected override async Task PublishFailedEventAsync(
            VideoGenerationRequested request,
            Exception ex,
            bool isRetryable,
            int retryCount,
            int maxRetries)
        {
            DateTime? nextRetryAt = null;
            if (isRetryable && _retryConfiguration.EnableRetries && retryCount < maxRetries)
            {
                var delaySeconds = _retryConfiguration.CalculateRetryDelay(retryCount);
                nextRetryAt = DateTime.UtcNow.AddSeconds(delaySeconds);
            }

            await _eventBus.PublishAsync(new VideoGenerationFailed
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

        protected override async Task PublishProgressEventAsync(
            VideoGenerationRequested request,
            int current,
            int total,
            string status)
        {
            await _eventBus.PublishAsync(new VideoGenerationProgress
            {
                RequestId = request.RequestId,
                ProgressPercentage = total > 0 ? (current * 100 / total) : 0,
                Status = status,
                Message = $"Video generation {status}",
                CorrelationId = request.CorrelationId
            });
        }

        protected override object CreateWebhookPayload(
            VideoGenerationRequested request,
            ProcessedMedia media,
            TimeSpan duration,
            string status,
            string? error = null)
        {
            return new VideoCompletionWebhookPayload
            {
                TaskId = request.RequestId,
                Status = status,
                VideoUrl = media.Url,
                GenerationDurationSeconds = duration.TotalSeconds,
                Model = request.Model,
                Prompt = request.Prompt,
                Error = error
            };
        }

        protected override string GetMediaType() => "Video";

        protected override void LogGenerationDetails(
            VideoGenerationRequested request,
            GenerationModelInfo modelInfo,
            VideoGenerationRequest generationRequest)
        {
            _logger.LogInformation(
                "Video generation request prepared: TaskId={TaskId}, Model={Model}, Provider={Provider}, " +
                "Duration={Duration}, Resolution={Resolution}, FPS={FPS}, PromptLength={PromptLength}",
                request.RequestId,
                request.Model,
                modelInfo.ProviderName,
                generationRequest.Duration,
                generationRequest.Size,
                generationRequest.Fps,
                request.Prompt?.Length ?? 0);
        }

        protected override bool IsRetryableError(Exception ex)
        {
            // Use retry configuration settings
            if (!_retryConfiguration.EnableRetries)
            {
                return false;
            }

            return base.IsRetryableError(ex);
        }

        /// <summary>
        /// Handles video generation cancellation events.
        /// </summary>
        public async Task HandleAsync(VideoGenerationCancelled cancellationEvent, IEventContext context)
        {
            _logger.LogInformation("Received cancellation request for video generation task {RequestId}",
                cancellationEvent.RequestId);

            // Cancel the task using the task registry
            var cancelled = _taskRegistry.TryCancel(cancellationEvent.RequestId);
            
            if (cancelled)
            {
                _logger.LogInformation("Successfully cancelled video generation task {RequestId}", 
                    cancellationEvent.RequestId);
                
                // Update task status to cancelled
                await _taskService.UpdateTaskStatusAsync(
                    cancellationEvent.RequestId,
                    TaskState.Cancelled,
                    error: "Task cancelled by user request");
                
                // Publish cancellation completed event
                await _eventBus.PublishAsync(new VideoGenerationProgress
                {
                    RequestId = cancellationEvent.RequestId,
                    Status = "cancelled",
                    Message = "Task cancelled by user request",
                    ProgressPercentage = 0,
                    CorrelationId = cancellationEvent.CorrelationId ?? Guid.NewGuid().ToString()
                });
            }
            else
            {
                _logger.LogWarning("Could not cancel video generation task {RequestId} - task may have already completed or was not found", 
                    cancellationEvent.RequestId);
            }
        }
    }

}
