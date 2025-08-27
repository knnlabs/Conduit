using System.Text.Json;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs;
using Prometheus;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

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

        public UsageTrackingMiddleware(
            RequestDelegate next,
            ILogger<UsageTrackingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Processes HTTP requests to track LLM usage and billing.
        /// 
        /// Billing Policy:
        /// - Only successful responses (2xx) are billed to customers
        /// - Client errors (4xx) are NOT billed - protects customers from malformed requests
        /// - Server errors (5xx) are NOT billed - our infrastructure failures shouldn't cost customers
        /// - Rate limiting (429) is NOT billed - capacity management shouldn't penalize customers
        /// 
        /// This follows Anthropic's customer-friendly approach rather than OpenAI's partial billing model.
        /// The policy ensures customers only pay for successfully processed requests that deliver value.
        /// </summary>
        public async Task InvokeAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            // Skip if not an API endpoint or no virtual key
            if (!ShouldTrackUsage(context))
            {
                // Log billing decision for error responses if this is a tracked endpoint type
                await LogBillingDecisionAsync(context, billingAuditService);
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
                    await TrackStreamingUsageAsync(context, costCalculationService, batchSpendService, 
                        requestLogService, virtualKeyService, billingAuditService);
                    return;
                }

                // Process non-streaming response
                await ProcessResponseAsync(
                    context,
                    responseBody,
                    costCalculationService,
                    batchSpendService,
                    requestLogService,
                    virtualKeyService,
                    billingAuditService);

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

            // Only track successful responses - core billing policy enforcement
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

        private async Task ProcessResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            var endpointType = UsageExtractor.DetermineRequestType(context.Request.Path);
            using var extractionTimer = UsageMetrics.UsageExtractionTime.WithLabels(endpointType).NewTimer();
            
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
                    LogMissingUsageData(context, billingAuditService);
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
                var usage = UsageExtractor.ExtractUsage(usageElement, _logger);
                if (usage == null)
                {
                    _logger.LogWarning("Failed to extract usage data for {Path}", context.Request.Path);
                    return;
                }

                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj) 
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Calculate cost
                var cost = await costCalculationService.CalculateCostAsync(model, usage);
                
                if (cost <= 0)
                {
                    _logger.LogDebug("Zero cost calculated for {Model} with usage {Usage}", model, JsonSerializer.Serialize(usage));
                    UsageMetrics.UsageTrackingFailures.WithLabels("zero_cost", endpointType).Inc();
                    LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService);
                    return;
                }

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels(endpointType, "success").Inc();
                
                if (usage.PromptTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);
                
                if (usage.CompletionTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);
                
                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType).Inc(Convert.ToDouble(cost));

                // Update spend using batch service
                await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);

                // Log the request
                await LogRequestAsync(context, virtualKeyId, model, usage, cost, requestLogService);
                
                // Audit log successful billing
                LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse response JSON for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("json_parse_error", endpointType).Inc();
                LogJsonParseError(context, ex, billingAuditService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("unexpected_error", endpointType).Inc();
                LogUnexpectedError(context, ex, billingAuditService);
            }
        }

        private async Task TrackStreamingUsageAsync(
            HttpContext context,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService)
        {
            var endpointType = UsageExtractor.DetermineRequestType(context.Request.Path);
            
            // Check if usage was estimated
            var isEstimated = context.Items.TryGetValue("UsageIsEstimated", out var estimatedObj) && 
                              estimatedObj is bool estimated && estimated;
            
            // For streaming responses, we need to rely on the SSE writer
            // to have stored the usage data in HttpContext.Items
            if (!context.Items.TryGetValue("StreamingUsage", out var usageObj) || 
                usageObj is not Usage usage)
            {
                _logger.LogDebug("No streaming usage data found for {Path}", context.Request.Path);
                UsageMetrics.UsageTrackingFailures.WithLabels("no_streaming_usage", endpointType).Inc();
                LogMissingStreamingUsage(context, billingAuditService);
                return;
            }

            if (!context.Items.TryGetValue("StreamingModel", out var modelObj) || 
                modelObj is not string model)
            {
                _logger.LogWarning("No streaming model found for {Path}", context.Request.Path);
                UsageMetrics.UsageTrackingFailures.WithLabels("no_streaming_model", endpointType).Inc();
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
                UsageMetrics.UsageTrackingRequests.WithLabels(endpointType + "_stream", "success").Inc();
                
                if (usage.PromptTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);
                
                if (usage.CompletionTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);
                
                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType + "_stream").Inc(Convert.ToDouble(cost));
                
                await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);
                await LogRequestAsync(context, virtualKeyId, model, usage, cost, requestLogService);
                
                LogStreamingBilling(context, model, usage, cost, providerType, isEstimated, billingAuditService);
            }
            else
            {
                UsageMetrics.UsageTrackingFailures.WithLabels("zero_cost_streaming", endpointType).Inc();
                LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService);
                UsageMetrics.ZeroCostEvents.WithLabels(model ?? "unknown", "streaming_zero").Inc();
            }
        }

        private async Task LogRequestAsync(
            HttpContext context,
            int virtualKeyId,
            string model,
            Usage usage,
            decimal cost,
            IRequestLogService requestLogService)
        {
            try
            {
                var requestType = UsageExtractor.DetermineRequestType(context.Request.Path);
                
                var logRequest = new LogRequestDto
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = model,
                    RequestType = requestType,
                    InputTokens = usage.PromptTokens ?? 0,
                    OutputTokens = usage.CompletionTokens ?? 0,
                    Cost = cost,
                    ResponseTimeMs = UsageExtractor.GetResponseTime(context),
                    UserId = context.User?.Identity?.Name,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    RequestPath = context.Request.Path.ToString(),
                    StatusCode = context.Response.StatusCode
                };

                await requestLogService.LogRequestAsync(logRequest);
                
                _logger.LogInformation(
                    "Tracked usage for VirtualKey {VirtualKeyId}: Model={Model}, PromptTokens={PromptTokens}, CompletionTokens={CompletionTokens}, Cost={Cost:C}",
                    virtualKeyId, model, usage.PromptTokens, usage.CompletionTokens, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log request for VirtualKey {VirtualKeyId}", virtualKeyId);
                // Don't throw - logging failure shouldn't break the request
            }
        }

        #region Billing Audit Logging

        private async Task LogBillingDecisionAsync(HttpContext context, IBillingAuditService billingAuditService)
        {
            await BillingPolicyHandler.LogBillingDecisionAsync(context, billingAuditService, _logger);
        }

        private void LogSuccessfulBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService, _logger);
        }

        private void LogZeroCostBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService);
        }

        private void LogMissingUsageData(HttpContext context, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogMissingUsageData(context, billingAuditService);
        }

        private void LogStreamingBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, bool isEstimated, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogStreamingBilling(context, model, usage, cost, providerType, isEstimated, billingAuditService, _logger);
        }

        private void LogMissingStreamingUsage(HttpContext context, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogMissingStreamingUsage(context, billingAuditService);
        }

        private void LogJsonParseError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogJsonParseError(context, ex, billingAuditService);
        }

        private void LogUnexpectedError(HttpContext context, Exception ex, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogUnexpectedError(context, ex, billingAuditService);
        }

        #endregion
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