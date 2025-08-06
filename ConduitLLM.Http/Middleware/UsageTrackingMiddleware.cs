using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Services;
using ConduitLLM.Configuration.DTOs;
using Prometheus;
using IVirtualKeyService = ConduitLLM.Configuration.Services.IVirtualKeyService;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware that tracks LLM usage by intercepting OpenAI-compatible responses.
    /// Extracts usage data from responses and updates virtual key spending.
    /// </summary>
    public class UsageTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UsageTrackingMiddleware> _logger;

        // Metrics for usage tracking
        private static readonly Counter UsageTrackingRequests = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_requests_total", "Total usage tracking requests",
                new CounterConfiguration
                {
                    LabelNames = new[] { "endpoint_type", "status" }
                });

        private static readonly Counter UsageTrackingTokens = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_tokens_total", "Total tokens tracked",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider_type", "token_type" }
                });

        private static readonly Counter UsageTrackingCosts = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_cost_dollars", "Total cost tracked in dollars",
                new CounterConfiguration
                {
                    LabelNames = new[] { "model", "provider_type", "endpoint_type" }
                });

        private static readonly Counter UsageTrackingFailures = Prometheus.Metrics
            .CreateCounter("conduit_usage_tracking_failures_total", "Usage tracking failures",
                new CounterConfiguration
                {
                    LabelNames = new[] { "reason", "endpoint_type" }
                });

        private static readonly Histogram UsageExtractionTime = Prometheus.Metrics
            .CreateHistogram("conduit_usage_extraction_time_seconds", "Time to extract usage from response",
                new HistogramConfiguration
                {
                    LabelNames = new[] { "endpoint_type" },
                    Buckets = Histogram.ExponentialBuckets(0.001, 2, 10) // 1ms to ~1s
                });

        public UsageTrackingMiddleware(
            RequestDelegate next,
            ILogger<UsageTrackingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService)
        {
            // Skip if not an API endpoint or no virtual key
            if (!ShouldTrackUsage(context))
            {
                await _next(context);
                return;
            }

            // For non-streaming responses, intercept the response body
            var originalBodyStream = context.Response.Body;

            try
            {
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;

                await _next(context);

                // After the controller has run, check if this is a streaming response
                // by checking the Content-Type that was set by the controller
                if (context.Response.ContentType?.Contains("text/event-stream") == true)
                {
                    _logger.LogDebug("Detected streaming response, skipping JSON parsing");
                    // For streaming, just copy the stream directly without parsing
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalBodyStream);
                    await TrackStreamingUsageAsync(context, costCalculationService, batchSpendService, requestLogService, virtualKeyService);
                    return;
                }

                // Process non-streaming response
                await ProcessResponseAsync(
                    context,
                    responseBody,
                    costCalculationService,
                    batchSpendService,
                    requestLogService,
                    virtualKeyService);

                // Copy the response body back to the original stream
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }

        private bool ShouldTrackUsage(HttpContext context)
        {
            // Check if this is an API request
            if (!context.Request.Path.StartsWithSegments("/v1"))
                return false;

            // Check if we have a virtual key in the context
            if (!context.Items.ContainsKey("VirtualKeyId"))
                return false;

            // Only track successful responses
            if (context.Response.StatusCode >= 400)
                return false;

            // Only track completion endpoints
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            return path.Contains("/completions") || 
                   path.Contains("/embeddings") || 
                   path.Contains("/images/generations") ||
                   path.Contains("/audio/transcriptions") ||
                   path.Contains("/audio/speech") ||
                   path.Contains("/videos/generations");
        }

        private bool IsStreamingRequest(HttpContext context)
        {
            // Check if the request body indicates streaming
            if (context.Items.TryGetValue("IsStreamingRequest", out var isStreaming) && 
                isStreaming is bool streamingBool)
            {
                return streamingBool;
            }

            // Check response content type for SSE
            return context.Response.ContentType?.Contains("text/event-stream") == true;
        }

        private async Task ProcessResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService)
        {
            var endpointType = DetermineRequestType(context.Request.Path);
            var extractionTimer = UsageExtractionTime.WithLabels(endpointType).NewTimer();
            
            try
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                
                // Parse the response JSON
                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                // Extract usage data if present
                if (!root.TryGetProperty("usage", out var usageElement))
                {
                    _logger.LogDebug("No usage data found in response for {Path}", context.Request.Path);
                    return;
                }

                // Extract model name
                if (!root.TryGetProperty("model", out var modelElement))
                {
                    _logger.LogWarning("No model found in response for {Path}", context.Request.Path);
                    return;
                }

                var model = modelElement.GetString();
                if (string.IsNullOrEmpty(model))
                {
                    _logger.LogWarning("Empty model name in response for {Path}", context.Request.Path);
                    return;
                }

                // Build Usage object
                var usage = ExtractUsage(usageElement);
                if (usage == null)
                {
                    _logger.LogWarning("Failed to extract usage data for {Path}", context.Request.Path);
                    return;
                }

                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
                var virtualKey = (string)context.Items["VirtualKey"]!;

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj) 
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Calculate cost
                var cost = await costCalculationService.CalculateCostAsync(model, usage);
                
                if (cost <= 0)
                {
                    _logger.LogDebug("Zero cost calculated for {Model} with usage {Usage}", model, JsonSerializer.Serialize(usage));
                    UsageTrackingFailures.WithLabels("zero_cost", endpointType).Inc();
                    return;
                }

                // Update metrics
                UsageTrackingRequests.WithLabels(endpointType, "success").Inc();
                
                if (usage.PromptTokens.HasValue)
                    UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);
                
                if (usage.CompletionTokens.HasValue)
                    UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);
                
                UsageTrackingCosts.WithLabels(model, providerType, endpointType).Inc(Convert.ToDouble(cost));

                // Update spend using batch service
                await UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService);

                // Log the request
                await LogRequestAsync(
                    context,
                    virtualKeyId,
                    model,
                    usage,
                    cost,
                    requestLogService,
                    batchSpendService);

                _logger.LogInformation(
                    "Tracked usage for VirtualKey {VirtualKeyId}: Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, Cost={Cost:C}",
                    virtualKeyId, model, usage.PromptTokens, usage.CompletionTokens, cost);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse response JSON for usage tracking");
                UsageTrackingFailures.WithLabels("json_parse_error", endpointType).Inc();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in usage tracking");
                UsageTrackingFailures.WithLabels("unexpected_error", endpointType).Inc();
            }
            finally
            {
                extractionTimer.Dispose();
            }
        }

        private Usage? ExtractUsage(JsonElement usageElement)
        {
            try
            {
                var usage = new Usage();

                // Standard OpenAI fields
                if (usageElement.TryGetProperty("prompt_tokens", out var promptTokens))
                    usage.PromptTokens = promptTokens.GetInt32();

                if (usageElement.TryGetProperty("completion_tokens", out var completionTokens))
                    usage.CompletionTokens = completionTokens.GetInt32();

                if (usageElement.TryGetProperty("total_tokens", out var totalTokens))
                    usage.TotalTokens = totalTokens.GetInt32();

                // Anthropic format (uses input_tokens/output_tokens)
                // Note: These will override OpenAI fields if both exist
                if (usageElement.TryGetProperty("input_tokens", out var inputTokens))
                    usage.PromptTokens = inputTokens.GetInt32();

                if (usageElement.TryGetProperty("output_tokens", out var outputTokens))
                    usage.CompletionTokens = outputTokens.GetInt32();

                // Anthropic cached tokens
                if (usageElement.TryGetProperty("cache_creation_input_tokens", out var cacheWriteTokens))
                    usage.CachedWriteTokens = cacheWriteTokens.GetInt32();

                if (usageElement.TryGetProperty("cache_read_input_tokens", out var cacheReadTokens))
                    usage.CachedInputTokens = cacheReadTokens.GetInt32();

                // Image generation
                if (usageElement.TryGetProperty("images", out var imageCount))
                    usage.ImageCount = imageCount.GetInt32();

                // Validate we have at least some usage data
                if (usage.PromptTokens == null && 
                    usage.CompletionTokens == null && 
                    usage.ImageCount == null)
                {
                    return null;
                }

                return usage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract usage data from response");
                return null;
            }
        }

        private async Task UpdateSpendAsync(
            int virtualKeyId,
            decimal cost,
            IBatchSpendUpdateService batchSpendService,
            IVirtualKeyService virtualKeyService)
        {
            try
            {
                // Try batch update first
                if (batchSpendService.IsHealthy)
                {
                    batchSpendService.QueueSpendUpdate(virtualKeyId, cost);
                }
                else
                {
                    // Fallback to direct update
                    _logger.LogWarning("BatchSpendUpdateService unhealthy, using direct update for VirtualKey {VirtualKeyId}", virtualKeyId);
                    await virtualKeyService.UpdateSpendAsync(virtualKeyId, cost);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update spend for VirtualKey {VirtualKeyId}, Cost {Cost:C}", virtualKeyId, cost);
                // Don't throw - we've already sent the response to the user
            }
        }

        private async Task LogRequestAsync(
            HttpContext context,
            int virtualKeyId,
            string model,
            Usage usage,
            decimal cost,
            IRequestLogService requestLogService,
            IBatchSpendUpdateService batchSpendService)
        {
            try
            {
                var requestType = DetermineRequestType(context.Request.Path);
                
                var logRequest = new LogRequestDto
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = model,
                    RequestType = requestType,
                    InputTokens = usage.PromptTokens ?? 0,
                    OutputTokens = usage.CompletionTokens ?? 0,
                    Cost = cost,
                    ResponseTimeMs = GetResponseTime(context),
                    UserId = context.User?.Identity?.Name,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    RequestPath = context.Request.Path.ToString(),
                    StatusCode = context.Response.StatusCode
                };

                // Use the method that doesn't double-charge
                await requestLogService.LogRequestAsync(logRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request for VirtualKey {VirtualKeyId}", virtualKeyId);
                // Don't throw - logging failure shouldn't break the request
            }
        }

        private async Task TrackStreamingUsageAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService)
        {
            var endpointType = DetermineRequestType(context.Request.Path);
            
            // For streaming responses, we need to rely on the SSE writer
            // to have stored the usage data in HttpContext.Items
            if (!context.Items.TryGetValue("StreamingUsage", out var usageObj) || 
                usageObj is not Usage usage)
            {
                _logger.LogDebug("No streaming usage data found for {Path}", context.Request.Path);
                UsageTrackingFailures.WithLabels("no_streaming_usage", endpointType).Inc();
                return;
            }

            if (!context.Items.TryGetValue("StreamingModel", out var modelObj) || 
                modelObj is not string model)
            {
                _logger.LogWarning("No streaming model found for {Path}", context.Request.Path);
                UsageTrackingFailures.WithLabels("no_streaming_model", endpointType).Inc();
                return;
            }

            var virtualKeyId = (int)context.Items["VirtualKeyId"]!;
            
            // Get provider type for metrics
            var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj) 
                ? providerTypeObj?.ToString() ?? "unknown"
                : "unknown";
            
            // Calculate cost and track
            var cost = await costCalculationService.CalculateCostAsync(model, usage);
            if (cost > 0)
            {
                // Update metrics
                UsageTrackingRequests.WithLabels(endpointType + "_stream", "success").Inc();
                
                if (usage.PromptTokens.HasValue)
                    UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);
                
                if (usage.CompletionTokens.HasValue)
                    UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);
                
                UsageTrackingCosts.WithLabels(model, providerType, endpointType + "_stream").Inc(Convert.ToDouble(cost));
                
                await UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService);
                await LogRequestAsync(context, virtualKeyId, model, usage, cost, requestLogService, batchSpendService);
            }
            else
            {
                UsageTrackingFailures.WithLabels("zero_cost_streaming", endpointType).Inc();
            }
        }

        private string DetermineRequestType(PathString path)
        {
            var pathValue = path.Value?.ToLowerInvariant() ?? "";
            
            if (pathValue.Contains("/chat/completions"))
                return "chat";
            if (pathValue.Contains("/completions"))
                return "completion";
            if (pathValue.Contains("/embeddings"))
                return "embedding";
            if (pathValue.Contains("/images/generations"))
                return "image";
            if (pathValue.Contains("/audio/transcriptions"))
                return "transcription";
            if (pathValue.Contains("/audio/speech"))
                return "tts";
            if (pathValue.Contains("/videos/generations"))
                return "video";
            
            return "other";
        }

        private double GetResponseTime(HttpContext context)
        {
            if (context.Items.TryGetValue("RequestStartTime", out var startTimeObj) && 
                startTimeObj is DateTime startTime)
            {
                return (DateTime.UtcNow - startTime).TotalMilliseconds;
            }
            
            return 0;
        }
    }

    /// <summary>
    /// Extension methods for registering the usage tracking middleware
    /// </summary>
    public static class UsageTrackingMiddlewareExtensions
    {
        public static IApplicationBuilder UseUsageTracking(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<UsageTrackingMiddleware>();
        }
    }
}