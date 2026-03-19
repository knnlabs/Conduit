using System.Text.Json;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.Utilities;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Middleware
{
    public partial class UsageTrackingMiddleware
    {
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
        /// Image responses typically don't have standard usage data in the response,
        /// so we extract details from HttpContext.Items (set by the controller) and the response data array.
        /// </summary>
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
                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Get image request details from HttpContext.Items (set by ImagesController)
                var quality = context.Items.TryGetValue(HttpContextKeys.ImageRequestQuality, out var qualityObj)
                    ? qualityObj?.ToString()
                    : null;
                var size = context.Items.TryGetValue(HttpContextKeys.ImageRequestSize, out var sizeObj)
                    ? sizeObj?.ToString()
                    : null;
                var requestedN = context.Items.TryGetValue(HttpContextKeys.ImageRequestN, out var nObj)
                    ? nObj as int? ?? 1
                    : 1;

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Parse the response to count actual images generated and check for usage/model data
                int actualImageCount = requestedN; // Default to requested count
                Usage? responseUsage = null;
                string? responseModel = null;

                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                // Try to get model from response (some providers may include it)
                if (root.TryGetProperty("model", out var modelElement))
                {
                    responseModel = modelElement.GetString();
                }

                // Count actual images from the data array
                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    actualImageCount = dataArray.GetArrayLength();
                }

                // Check if the response includes usage data (some providers may include it)
                if (root.TryGetProperty("usage", out var usageElement))
                {
                    responseUsage = UsageExtractor.ExtractUsage(usageElement, _logger);
                }

                // Resolve model: prefer HttpContext.Items (original request model alias), then response, then "unknown"
                var model = context.Items.TryGetValue(HttpContextKeys.ImageRequestModel, out var modelObj)
                    ? modelObj?.ToString()
                    : null;
                if (string.IsNullOrEmpty(model))
                {
                    model = responseModel ?? "unknown";
                }

                // Build usage object - prefer response usage if available, otherwise construct from request data
                var usage = responseUsage ?? new Usage
                {
                    ImageCount = actualImageCount,
                    ImageQuality = quality,
                    ImageResolution = size
                };

                // Ensure image count is set even if response usage was used
                if (!usage.ImageCount.HasValue || usage.ImageCount.Value == 0)
                {
                    usage.ImageCount = actualImageCount;
                }
                if (string.IsNullOrEmpty(usage.ImageQuality))
                {
                    usage.ImageQuality = quality;
                }
                if (string.IsNullOrEmpty(usage.ImageResolution))
                {
                    usage.ImageResolution = size;
                }

                // Calculate cost - prefer ID-based lookup if ModelCostId is available
                decimal cost;
                if (context.Items.TryGetValue(HttpContextKeys.ModelCostId, out var modelCostIdObj) &&
                    modelCostIdObj is int modelCostId)
                {
                    cost = await costCalculationService.CalculateCostByIdAsync(modelCostId, usage);
                }
                else
                {
                    cost = await costCalculationService.CalculateCostAsync(model, usage);
                }

                // Build metadata JSON for image generation
                var metadata = JsonSerializer.Serialize(new
                {
                    type = "image",
                    imageCount = actualImageCount,
                    quality = quality ?? "standard",
                    size = size ?? "unknown",
                    style = context.Items.TryGetValue("ImageRequestStyle", out var styleObj) ? styleObj?.ToString() : null
                });

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels("image", "success").Inc();
                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, "image").Inc(Convert.ToDouble(cost));

                // Record business metrics for Grafana dashboards (real-time counters)
                var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
                BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
                BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
                if (cost > 0)
                {
                    BusinessMetricsService.RecordCost(providerType, model, "image", Convert.ToDouble(cost));
                }

                // Update spend if there's a cost
                if (cost > 0)
                {
                    await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);
                    LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService);
                }
                else
                {
                    UsageMetrics.ZeroCostEvents.WithLabels(model, "image_zero").Inc();
                    LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService);
                }

                // Log the request with image metadata
                await LogRequestAsync(context, virtualKeyId, model, usage, cost, requestLogService, metadata);

                _logger.LogInformation(
                    "Tracked image generation for VirtualKey {VirtualKeyId}: Model={Model}, Images={ImageCount}, Quality={Quality}, Size={Size}, Cost={Cost:C}",
                    virtualKeyId, model, actualImageCount, quality ?? "standard", size ?? "default", cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image response for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("image_processing_error", "image").Inc();
            }
        }

        /// <summary>
        /// Process video generation responses and log them with video-specific metadata.
        /// Video responses typically don't have standard usage data in the response,
        /// so we extract details from HttpContext.Items (set by the controller) and the response data.
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
                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Get video request details from HttpContext.Items (set by VideosController)
                var size = context.Items.TryGetValue(HttpContextKeys.VideoRequestSize, out var sizeObj)
                    ? sizeObj?.ToString()
                    : null;
                var requestedDuration = context.Items.TryGetValue(HttpContextKeys.VideoRequestDuration, out var durationObj)
                    ? durationObj as int?
                    : null;
                var requestedN = context.Items.TryGetValue(HttpContextKeys.VideoRequestN, out var nObj)
                    ? nObj as int? ?? 1
                    : 1;
                var fps = context.Items.TryGetValue(HttpContextKeys.VideoRequestFps, out var fpsObj)
                    ? fpsObj as int?
                    : null;
                var style = context.Items.TryGetValue(HttpContextKeys.VideoRequestStyle, out var styleObj)
                    ? styleObj?.ToString()
                    : null;

                // Get pricing parameters for rules-based pricing
                var pricingParameters = context.Items.TryGetValue(HttpContextKeys.VideoRequestPricingParameters, out var paramsObj)
                    ? paramsObj as Dictionary<string, object>
                    : null;

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Parse the response to check for usage/model data and actual video count
                int actualVideoCount = requestedN;
                Usage? responseUsage = null;
                string? responseModel = null;
                double? actualDuration = null;
                string? actualResolution = null;
                string? taskId = null;

                responseBody.Seek(0, SeekOrigin.Begin);
                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                // Try to get task ID from async response (for cost correction later)
                if (root.TryGetProperty("taskId", out var taskIdElement))
                {
                    taskId = taskIdElement.GetString();
                }

                // Try to get model from response
                if (root.TryGetProperty("model", out var modelElement))
                {
                    responseModel = modelElement.GetString();
                }

                // Count actual videos from the data array and extract metadata
                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    actualVideoCount = dataArray.GetArrayLength();

                    // Extract metadata from first video if available
                    if (actualVideoCount > 0)
                    {
                        var firstVideo = dataArray[0];
                        if (firstVideo.TryGetProperty("metadata", out var videoMetadata))
                        {
                            if (videoMetadata.TryGetProperty("duration", out var durationEl))
                            {
                                actualDuration = durationEl.GetDouble();
                            }
                            if (videoMetadata.TryGetProperty("width", out var widthEl) &&
                                videoMetadata.TryGetProperty("height", out var heightEl))
                            {
                                actualResolution = $"{widthEl.GetInt32()}x{heightEl.GetInt32()}";
                            }
                        }
                    }
                }

                // Check if the response includes usage data
                if (root.TryGetProperty("usage", out var usageElement))
                {
                    responseUsage = UsageExtractor.ExtractUsage(usageElement, _logger);
                }

                // Resolve model: prefer HttpContext.Items (original request model alias), then response, then "unknown"
                var model = context.Items.TryGetValue(HttpContextKeys.VideoRequestModel, out var modelObj)
                    ? modelObj?.ToString()
                    : null;
                if (string.IsNullOrEmpty(model))
                {
                    model = responseModel ?? "unknown";
                }

                // Build usage object - prefer response usage if available, otherwise construct from request/response data
                var usage = responseUsage ?? new Usage();

                // Set video duration (prefer actual from response, then requested)
                if (!usage.VideoDurationSeconds.HasValue)
                {
                    usage.VideoDurationSeconds = actualDuration ?? requestedDuration;
                }

                // Set video resolution (prefer actual from response, then requested)
                if (string.IsNullOrEmpty(usage.VideoResolution))
                {
                    usage.VideoResolution = actualResolution ?? size;
                }

                // Set pricing parameters for rules-based pricing
                if (pricingParameters != null && pricingParameters.Count > 0)
                {
                    usage.PricingParameters = pricingParameters;
                }

                // Calculate cost - prefer ID-based lookup if ModelCostId is available
                decimal cost;
                if (context.Items.TryGetValue(HttpContextKeys.ModelCostId, out var modelCostIdObj) &&
                    modelCostIdObj is int modelCostId)
                {
                    cost = await costCalculationService.CalculateCostByIdAsync(modelCostId, usage);
                }
                else
                {
                    cost = await costCalculationService.CalculateCostAsync(model, usage);
                }

                // Build metadata JSON for video generation
                // Include taskId for async requests so we can update cost/duration later
                var metadata = JsonSerializer.Serialize(new
                {
                    type = "video",
                    taskId = taskId,
                    videoCount = actualVideoCount,
                    durationSeconds = usage.VideoDurationSeconds,
                    resolution = usage.VideoResolution ?? "unknown",
                    fps = fps,
                    style = style,
                    pricingParametersUsed = pricingParameters?.Keys.ToArray()
                });

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels("video", "success").Inc();
                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, "video").Inc(Convert.ToDouble(cost));

                // Record business metrics for Grafana dashboards (real-time counters)
                var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
                BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
                BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
                if (cost > 0)
                {
                    BusinessMetricsService.RecordCost(providerType, model, "video", Convert.ToDouble(cost));
                }

                // Update spend if there's a cost
                if (cost > 0)
                {
                    await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);
                    LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService);
                }
                else
                {
                    UsageMetrics.ZeroCostEvents.WithLabels(model, "video_zero").Inc();
                    LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService);
                }

                // Log the request with video metadata
                await LogRequestAsync(context, virtualKeyId, model, usage, cost, requestLogService, metadata);

                _logger.LogInformation(
                    "Tracked video generation for VirtualKey {VirtualKeyId}: Model={Model}, Videos={VideoCount}, Duration={Duration}s, Resolution={Resolution}, Cost={Cost:C}",
                    virtualKeyId, model, actualVideoCount, usage.VideoDurationSeconds ?? 0, usage.VideoResolution ?? "unknown", cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process video response for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("video_processing_error", "video").Inc();
            }
        }
    }
}
