using System.Text.Json;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.UsageTracking;
using ConduitLLM.Gateway.Utilities;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Middleware
{
    public partial class UsageTrackingMiddleware
    {
        /// <summary>
        /// Holds the type-specific data extracted by image/video adapters
        /// so the shared pipeline can process any media type uniformly.
        /// </summary>
        private sealed class MediaProcessingContext
        {
            public required string MediaType { get; init; }
            public required string Model { get; init; }
            public required Usage Usage { get; init; }
            public required string MetadataJson { get; init; }
            public required string ProviderType { get; init; }
            public required int VirtualKeyId { get; init; }
            public required string LogDetail { get; init; }
        }

        /// <summary>
        /// Resolves the model name: prefer the value stored in HttpContext.Items by the controller,
        /// fall back to the model returned in the provider response, then "unknown".
        /// </summary>
        /// <summary>
        /// Resolves the canonical model name. Prefers the model recorded in the request
        /// usage context (set by the controller before provider mapping); falls back to
        /// the model echoed in the response, then to <c>"unknown"</c>.
        /// </summary>
        private static string ResolveModelFromUsage(string? requestModel, string? responseModel)
        {
            return string.IsNullOrEmpty(requestModel) ? (responseModel ?? "unknown") : requestModel;
        }

        /// <summary>
        /// Shared pipeline for image and video response processing.
        /// Handles cost calculation, metrics, spend updates, billing audit, and request logging.
        /// </summary>
        private async Task ProcessMediaResponseAsync(
            HttpContext context,
            MediaProcessingContext media,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            // Calculate cost - prefer ID-based lookup if ModelCostId is available
            decimal cost;
            if (context.Items.TryGetValue(HttpContextKeys.ModelCostId, out var modelCostIdObj) &&
                modelCostIdObj is int modelCostId)
            {
                cost = await costCalculationService.CalculateCostByIdAsync(modelCostId, media.Usage);
            }
            else
            {
                cost = await costCalculationService.CalculateCostAsync(media.Model, media.Usage);
            }

            // Update Prometheus metrics
            UsageMetrics.UsageTrackingRequests.WithLabels(media.MediaType, "success").Inc();
            UsageMetrics.UsageTrackingCosts.WithLabels(media.Model, media.ProviderType, media.MediaType)
                .Inc(Convert.ToDouble(cost));

            // Record business metrics for Grafana dashboards
            var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300
                ? "success" : "error";
            BusinessMetricsService.RecordModelRequest(media.Model, media.ProviderType, requestStatus);
            BusinessMetricsService.RecordResponseTime(media.Model, media.ProviderType,
                UsageExtractor.GetResponseTime(context) / 1000.0);
            if (cost > 0)
            {
                BusinessMetricsService.RecordCost(media.ProviderType, media.Model, media.MediaType,
                    Convert.ToDouble(cost));
            }

            // Update spend and log billing
            if (cost > 0)
            {
                await SpendUpdateHelper.UpdateSpendAsync(media.VirtualKeyId, cost,
                    batchSpendService, virtualKeyService, _logger);
                LogSuccessfulBilling(context, media.Model, media.Usage, cost,
                    media.ProviderType, billingAuditService);
            }
            else
            {
                UsageMetrics.ZeroCostEvents.WithLabels(media.Model, $"{media.MediaType}_zero").Inc();
                LogZeroCostBilling(context, media.Model, media.Usage, cost,
                    media.ProviderType, billingAuditService);
            }

            // Log the request with media metadata
            await LogRequestAsync(context, media.VirtualKeyId, media.Model, media.Usage, cost,
                requestLogService, media.MetadataJson);

            _logger.LogInformation(
                "Tracked {MediaType} generation for VirtualKey {VirtualKeyId}: Model={Model}, {Detail}, Cost={Cost:C}",
                media.MediaType, media.VirtualKeyId, media.Model, media.LogDetail, cost);
        }

        /// <summary>
        /// Process function execution responses and log them with function-specific metadata.
        /// </summary>
        private async Task ProcessFunctionResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            try
            {
                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Get function configuration info from HttpContext.Items (set by FunctionsController)
                var functionConfigId = context.Items.TryGetValue("FunctionConfigurationId", out var configIdObj)
                    ? configIdObj as int? ?? 0
                    : 0;
                var functionName = context.Items.TryGetValue("FunctionConfigurationName", out var nameObj)
                    ? nameObj?.ToString() ?? "unknown"
                    : "unknown";
                var executionId = context.Items.TryGetValue("FunctionExecutionId", out var execIdObj)
                    ? execIdObj as Guid? ?? Guid.Empty
                    : Guid.Empty;

                // Parse the response to get cost and state
                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                decimal cost = 0;
                string state = "unknown";
                string? errorMessage = null;

                if (root.TryGetProperty("actualCost", out var actualCostElement))
                {
                    cost = actualCostElement.ValueKind == JsonValueKind.Number
                        ? actualCostElement.GetDecimal()
                        : 0;
                }
                else if (root.TryGetProperty("estimatedCost", out var estimatedCostElement))
                {
                    cost = estimatedCostElement.ValueKind == JsonValueKind.Number
                        ? estimatedCostElement.GetDecimal()
                        : 0;
                }

                if (root.TryGetProperty("state", out var stateElement))
                {
                    state = stateElement.GetString() ?? "unknown";
                }

                if (root.TryGetProperty("errorMessage", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
                {
                    errorMessage = errorElement.GetString();
                }

                // Build metadata JSON for function execution
                var metadata = JsonSerializer.Serialize(new
                {
                    type = "function",
                    functionConfigurationId = functionConfigId,
                    functionName,
                    executionId,
                    state,
                    errorMessage
                });

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels("function", "success").Inc();
                UsageMetrics.UsageTrackingCosts.WithLabels(functionName, providerType, "function").Inc(Convert.ToDouble(cost));

                // Record business metrics for Grafana dashboards (real-time counters)
                var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
                BusinessMetricsService.RecordModelRequest(functionName, providerType, requestStatus);
                BusinessMetricsService.RecordResponseTime(functionName, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
                if (cost > 0)
                {
                    BusinessMetricsService.RecordCost(providerType, functionName, "function", Convert.ToDouble(cost));
                }

                // Update spend if there's a cost
                if (cost > 0)
                {
                    await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);
                }

                // Create a Usage object with zero tokens (functions don't use tokens)
                var usage = new Usage
                {
                    PromptTokens = 0,
                    CompletionTokens = 0,
                    TotalTokens = 0
                };

                // Log the request with function metadata
                await LogRequestAsync(context, virtualKeyId, functionName, usage, cost, requestLogService, metadata);

                _logger.LogInformation(
                    "Tracked function execution for VirtualKey {VirtualKeyId}: Function={FunctionName}, ExecutionId={ExecutionId}, Cost={Cost:C}",
                    virtualKeyId, functionName, executionId, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process function response for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("function_processing_error", "function").Inc();
            }
        }

        /// <summary>
        /// Process image generation responses and log them with image-specific metadata.
        /// Extracts image-specific data, then delegates to the shared media pipeline.
        /// </summary>
        /// <summary>
        /// Bills an audio (STT/TTS) request from the typed <see cref="AudioUsageContext"/> set by the
        /// controller — never from the response body, which for TTS is raw binary audio.
        /// </summary>
        private async Task ProcessAudioResponseAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            try
            {
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                var audioContext = context.GetUsageContext() as AudioUsageContext;
                var endpointType = UsageExtractor.DetermineRequestType(context.Request.Path);
                var model = audioContext?.Model ?? "unknown";

                var usage = new Usage
                {
                    AudioDurationSeconds = audioContext?.AudioDurationSeconds,
                    TtsCharacters = audioContext?.TtsCharacters
                };
                ApplyProviderBillingPolicy(context, usage);

                var metadata = JsonSerializer.Serialize(new
                {
                    type = endpointType,
                    audioDurationSeconds = audioContext?.AudioDurationSeconds,
                    ttsCharacters = audioContext?.TtsCharacters
                });

                await ProcessMediaResponseAsync(context, new MediaProcessingContext
                {
                    MediaType = endpointType,
                    Model = model,
                    Usage = usage,
                    MetadataJson = metadata,
                    ProviderType = providerType,
                    VirtualKeyId = virtualKeyId,
                    LogDetail = endpointType == "tts"
                        ? $"TtsCharacters={audioContext?.TtsCharacters ?? 0}"
                        : $"AudioSeconds={audioContext?.AudioDurationSeconds ?? 0}"
                }, costCalculationService, batchSpendService, requestLogService, virtualKeyService, billingAuditService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process audio response for usage tracking");
            }
        }

        private async Task ProcessImageResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            try
            {
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Extract image request details from the typed usage context (set by ImagesController)
                var imageUsage = context.GetUsageContext() as ImageUsageContext;
                var quality = imageUsage?.Quality;
                var size = imageUsage?.Size;
                var requestedN = imageUsage?.N ?? 1;
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Parse the response
                int actualImageCount = requestedN;
                Usage? responseUsage = null;
                string? responseModel = null;

                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                if (root.TryGetProperty("model", out var modelElement))
                    responseModel = modelElement.GetString();

                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                    actualImageCount = dataArray.GetArrayLength();

                if (root.TryGetProperty("usage", out var usageElement))
                    responseUsage = UsageExtractor.ExtractUsage(usageElement, _logger);

                var model = ResolveModelFromUsage(imageUsage?.Model, responseModel);

                // Build Usage object - prefer response usage if available, otherwise construct from request data
                var usage = responseUsage ?? new Usage
                {
                    ImageCount = actualImageCount,
                    ImageQuality = quality,
                    ImageResolution = size
                };
                if (!usage.ImageCount.HasValue || usage.ImageCount.Value == 0)
                    usage.ImageCount = actualImageCount;
                if (string.IsNullOrEmpty(usage.ImageQuality))
                    usage.ImageQuality = quality;
                if (string.IsNullOrEmpty(usage.ImageResolution))
                    usage.ImageResolution = size;

                // Apply provider billing policy + provider-reported cost (side channel) before billing.
                ApplyProviderBillingPolicy(context, usage);

                var metadata = JsonSerializer.Serialize(new
                {
                    type = "image",
                    imageCount = actualImageCount,
                    quality = quality ?? "standard",
                    size = size ?? "unknown",
                    style = imageUsage?.Style
                });

                await ProcessMediaResponseAsync(context, new MediaProcessingContext
                {
                    MediaType = "image",
                    Model = model,
                    Usage = usage,
                    MetadataJson = metadata,
                    ProviderType = providerType,
                    VirtualKeyId = virtualKeyId,
                    LogDetail = $"Images={actualImageCount}, Quality={quality ?? "standard"}, Size={size ?? "default"}"
                }, costCalculationService, batchSpendService, requestLogService, virtualKeyService, billingAuditService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image response for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("image_processing_error", "image").Inc();
            }
        }

        /// <summary>
        /// Process video generation responses and log them with video-specific metadata.
        /// Extracts video-specific data, then delegates to the shared media pipeline.
        /// </summary>
        private async Task ProcessVideoResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            try
            {
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Extract video request details from the typed usage context (set by VideosController)
                var videoUsage = context.GetUsageContext() as VideoUsageContext;
                var size = videoUsage?.Size;
                var requestedDuration = videoUsage?.Duration;
                var requestedN = videoUsage?.N ?? 1;
                var fps = videoUsage?.Fps;
                var style = videoUsage?.Style;
                var pricingParameters = videoUsage?.PricingParameters;
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Parse the response
                int actualVideoCount = requestedN;
                Usage? responseUsage = null;
                string? responseModel = null;
                double? actualDuration = null;
                string? actualResolution = null;
                string? taskId = null;

                responseBody.Seek(0, SeekOrigin.Begin);
                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                if (root.TryGetProperty("taskId", out var taskIdElement))
                    taskId = taskIdElement.GetString();

                if (root.TryGetProperty("model", out var modelElement))
                    responseModel = modelElement.GetString();

                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    actualVideoCount = dataArray.GetArrayLength();

                    if (actualVideoCount > 0)
                    {
                        var firstVideo = dataArray[0];
                        if (firstVideo.TryGetProperty("metadata", out var videoMetadata))
                        {
                            if (videoMetadata.TryGetProperty("duration", out var durationEl))
                                actualDuration = durationEl.GetDouble();
                            if (videoMetadata.TryGetProperty("width", out var widthEl) &&
                                videoMetadata.TryGetProperty("height", out var heightEl))
                                actualResolution = $"{widthEl.GetInt32()}x{heightEl.GetInt32()}";
                        }
                    }
                }

                if (root.TryGetProperty("usage", out var usageElement))
                    responseUsage = UsageExtractor.ExtractUsage(usageElement, _logger);

                var model = ResolveModelFromUsage(videoUsage?.Model, responseModel);

                // Build Usage object
                var usage = responseUsage ?? new Usage();
                if (!usage.VideoDurationSeconds.HasValue)
                    usage.VideoDurationSeconds = actualDuration ?? requestedDuration;
                if (string.IsNullOrEmpty(usage.VideoResolution))
                    usage.VideoResolution = actualResolution ?? size;
                if (pricingParameters != null && pricingParameters.Count > 0)
                    usage.PricingParameters = pricingParameters;

                var metadata = JsonSerializer.Serialize(new
                {
                    type = "video",
                    taskId,
                    videoCount = actualVideoCount,
                    durationSeconds = usage.VideoDurationSeconds,
                    resolution = usage.VideoResolution ?? "unknown",
                    fps,
                    style,
                    pricingParametersUsed = pricingParameters?.Keys.ToArray()
                });

                await ProcessMediaResponseAsync(context, new MediaProcessingContext
                {
                    MediaType = "video",
                    Model = model,
                    Usage = usage,
                    MetadataJson = metadata,
                    ProviderType = providerType,
                    VirtualKeyId = virtualKeyId,
                    LogDetail = $"Videos={actualVideoCount}, Duration={usage.VideoDurationSeconds ?? 0}s, Resolution={usage.VideoResolution ?? "unknown"}"
                }, costCalculationService, batchSpendService, requestLogService, virtualKeyService, billingAuditService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video response for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("video_processing_error", "video").Inc();
            }
        }
    }
}
