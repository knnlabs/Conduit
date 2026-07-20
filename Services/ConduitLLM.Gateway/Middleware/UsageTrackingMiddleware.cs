using System.Diagnostics;
using System.Text.Json;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Controllers;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Options;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.UsageTracking;
using ConduitLLM.Gateway.Utilities;
using Microsoft.Extensions.Options;
using Prometheus;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Middleware
{
    /// <summary>
    /// Middleware that tracks LLM usage by intercepting OpenAI-compatible responses.
    /// Extracts usage data from responses and updates virtual key spending.
    /// </summary>
    public partial class UsageTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<UsageTrackingMiddleware> _logger;
        private readonly long _maximumLegacyResponseCaptureBytes;

        public UsageTrackingMiddleware(
            RequestDelegate next,
            ILogger<UsageTrackingMiddleware> logger,
            IOptions<UsageTrackingOptions>? options = null)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maximumLegacyResponseCaptureBytes =
                options?.Value.MaximumLegacyResponseCaptureBytes ?? new UsageTrackingOptions().MaximumLegacyResponseCaptureBytes;
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
            IBillingAuditService billingAuditService,
            IToolCostCalculationService toolCostCalculationService)
        {
            // Skip if not an API endpoint or no virtual key
            if (!ShouldTrackUsage(context))
            {
                // Log billing decision for error responses if this is a tracked endpoint type
                await LogBillingDecisionAsync(context, billingAuditService);
                await _next(context);
                return;
            }

            using var activity = GatewayRequestMetrics.StartUsageTrackingActivity(
                UsageExtractor.DetermineRequestType(context.Request.Path));

            // Legacy JSON endpoints still derive usage from the response body. The adaptive
            // stream bypasses capture as soon as an SSE response sets its content type and
            // performs its first write/flush.
            var originalBodyStream = context.Response.Body;

            try
            {
                await using var responseBody = new ConditionalResponseCaptureStream(
                    context.Response,
                    originalBodyStream,
                    _maximumLegacyResponseCaptureBytes);
                context.Response.Body = responseBody;

                try
                {
                    await _next(context);
                }
                finally
                {
                    // The downstream stream can fail because the provider errored or the client
                    // disconnected. The controller stores partial usage in its own finally block,
                    // so bill it here before any copy to the possibly-aborted client response.
                    if (context.Response.ContentType?.Contains("text/event-stream") == true)
                    {
                        await TrackStreamingUsageAsync(context, costCalculationService, batchSpendService,
                            requestLogService, virtualKeyService, billingAuditService, toolCostCalculationService);
                    }
                }

                // After the controller has run, check if this is a streaming response
                // by checking the Content-Type that was set by the controller
                if (context.Response.ContentType?.Contains("text/event-stream") == true)
                {
                    _logger.LogDebug("Detected streaming response; response body used direct passthrough");
                    return;
                }

                if (responseBody.CaptureLimitExceeded)
                {
                    _logger.LogError(
                        "Legacy response capture exceeded {MaximumCaptureBytes} bytes for {Path}; " +
                        "the response was passed through and usage accounting is incomplete",
                        _maximumLegacyResponseCaptureBytes,
                        LoggingSanitizer.S(context.Request.Path.ToString()));
                    UsageMetrics.UsageTrackingFailures.WithLabels(
                        "response_capture_limit",
                        UsageExtractor.DetermineRequestType(context.Request.Path)).Inc();
                    LogMissingUsageData(
                        context,
                        billingAuditService,
                        "Response exceeded the legacy accounting capture limit",
                        "response_capture_limit");
                    return;
                }

                // Process non-streaming response
                await ProcessResponseAsync(
                    context,
                    responseBody.CapturedBody,
                    costCalculationService,
                    batchSpendService,
                    requestLogService,
                    virtualKeyService,
                    billingAuditService,
                    toolCostCalculationService);

                // Copy the response body back to the original stream
                await responseBody.CopyCapturedBodyToOriginalAsync();
            }
            finally
            {
                try
                {
                    await FinalizeOpenReservationAsync(context);
                }
                catch (Exception reservationFinalizationEx)
                {
                    _logger.LogCritical(
                        reservationFinalizationEx,
                        "Failed to finalize the request spend reservation for {Path}",
                        LoggingSanitizer.S(context.Request.Path.ToString()));
                }

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

            // Only track completion endpoints and function executions
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Exclude polling/task status endpoints - these are not billable requests
            // The actual generation request is billed, not the status checks
            if (path.Contains("/tasks/") || path.Contains("/status"))
                return false;

            return path.Contains("/completions") ||
                   path.Contains("/embeddings") ||
                   path.Contains("/images/generations") ||
                   path.Contains("/audio/transcriptions") ||
                   path.Contains("/audio/speech") ||
                   path.Contains("/videos/generations") ||
                   path.Contains("/rerank") ||
                   path.Contains("/functions/execute");
        }

        /// <summary>
        /// Stamps the provider billing policy — and, where the controller stashed it, the
        /// provider-reported cost — from HttpContext.Items onto the usage object before cost
        /// calculation. Streaming carries the cost on the usage object itself; non-streaming and
        /// media paths supply it via the ProviderReportedCost item because the cost is never
        /// serialized into the client-facing response body.
        /// </summary>
        private static void ApplyProviderBillingPolicy(HttpContext context, ConduitLLM.Core.Models.Usage usage)
        {
            if (context.Items.TryGetValue(HttpContextKeys.ProviderBillingPolicy, out var policyObj) &&
                policyObj is ConduitLLM.Core.Models.ProviderCostBillingPolicy policy)
            {
                usage.ProviderCostPolicy = policy;
            }

            if (usage.ProviderReportedCostUsd == null &&
                context.Items.TryGetValue(HttpContextKeys.ProviderReportedCost, out var costObj) &&
                costObj is decimal reportedCost)
            {
                usage.ProviderReportedCostUsd = reportedCost;
            }
        }

        private async Task ProcessResponseAsync(
            HttpContext context,
            MemoryStream responseBody,
            ICostCalculationService costCalculationService,
            IBatchSpendUpdateService batchSpendService,
            IRequestLogService requestLogService,
            IVirtualKeyService virtualKeyService,
            IBillingAuditService billingAuditService,
            IToolCostCalculationService toolCostCalculationService)
        {
            var endpointType = UsageExtractor.DetermineRequestType(context.Request.Path);
            using var extractionTimer = UsageMetrics.UsageExtractionTime.WithLabels(endpointType).NewTimer();

            try
            {
                // The response status is normally still 200 when InvokeAsync first decides whether
                // to intercept the request. Enforce the no-charge-on-errors policy again after the
                // downstream pipeline has produced the actual response. This is especially
                // important for media endpoints, whose usage is synthesized from request metadata.
                if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
                {
                    await LogBillingDecisionAsync(context, billingAuditService);
                    return;
                }

                responseBody.Seek(0, SeekOrigin.Begin);

                // Handle function execution requests specially (they don't have standard usage data)
                if (endpointType == "function")
                {
                    await ProcessFunctionResponseAsync(context, responseBody, batchSpendService, requestLogService,
                        virtualKeyService, billingAuditService);
                    return;
                }

                // Handle image generation requests specially (they typically don't have usage data)
                if (endpointType == "image")
                {
                    await ProcessImageResponseAsync(context, responseBody, costCalculationService, batchSpendService,
                        requestLogService, virtualKeyService, billingAuditService);
                    return;
                }

                // Handle video generation requests specially (they typically don't have usage data)
                if (endpointType == "video")
                {
                    await ProcessVideoResponseAsync(context, responseBody, costCalculationService, batchSpendService,
                        requestLogService, virtualKeyService, billingAuditService);
                    return;
                }

                // Handle audio (STT/TTS) — bill from the typed usage context, never the body. The TTS
                // response body is raw binary audio and would fail JSON parsing below.
                if (endpointType == "transcription" || endpointType == "tts")
                {
                    await ProcessAudioResponseAsync(context, costCalculationService, batchSpendService,
                        requestLogService, virtualKeyService, billingAuditService);
                    return;
                }

                // Parse the response JSON
                using var jsonDocument = await JsonDocument.ParseAsync(responseBody);
                var root = jsonDocument.RootElement;

                var accountingSnapshot = context.GetRequestAccountingSnapshot();
                var typedUsageEvidence = accountingSnapshot?.ProviderUsage;

                // Extract usage data if typed controller evidence is not available.
                var hasSerializedUsage = root.TryGetProperty("usage", out var usageElement);
                if (typedUsageEvidence is null && !hasSerializedUsage)
                {
                    _logger.LogDebug("No usage data found in response for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                    LogMissingUsageData(context, billingAuditService);
                    return;
                }

                // Extract model name, preferring typed provider evidence.
                var model = typedUsageEvidence?.Model;
                var hasSerializedModel = root.TryGetProperty("model", out var modelElement);
                if (string.IsNullOrWhiteSpace(model) && !hasSerializedModel)
                {
                    _logger.LogWarning("No model found in response for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                    LogMissingUsageData(context, billingAuditService, "Response did not contain a model", "missing_model");
                    return;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    model = modelElement.GetString();
                }
                if (string.IsNullOrEmpty(model))
                {
                    _logger.LogWarning("Empty model name in response for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                    LogMissingUsageData(context, billingAuditService, "Response contained an empty model", "empty_model");
                    return;
                }

                // Build Usage object
                var usage = typedUsageEvidence?.Usage;
                if (usage is null &&
                    context.Items.TryGetValue(HttpContextKeys.NonStreamingUsage, out var normalizedUsage) &&
                    normalizedUsage is Usage providerUsage)
                {
                    usage = providerUsage;
                }
                usage ??= hasSerializedUsage ? UsageExtractor.ExtractUsage(usageElement, _logger) : null;
                if (usage == null)
                {
                    _logger.LogWarning("Failed to extract usage data for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                    LogMissingUsageData(context, billingAuditService, "Response usage could not be extracted", "unparseable_usage", model);
                    return;
                }

                // Apply the provider billing policy + provider-reported cost from the side channel
                // (the cost is never in the re-parsed body, so it must come from HttpContext.Items).
                ApplyProviderBillingPolicy(context, usage);

                // Get virtual key ID
                var virtualKeyId = (int)context.Items["VirtualKeyId"]!;

                // Get provider type for metrics
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj) 
                    ? providerTypeObj?.ToString() ?? "unknown"
                    : "unknown";

                // Parse provider type enum for tool usage parsing
                var providerTypeEnum = Enum.TryParse<ProviderType>(providerType, true, out var parsedProviderType)
                    ? parsedProviderType
                    : ProviderType.OpenAI; // Default fallback

                // Calculate base cost from token usage - prefer ID-based lookup if ModelCostId is available
                var pricingResult = await CalculateTrackedCostAsync(
                    context, model, usage, costCalculationService, billingAuditService);
                var cost = pricingResult.Cost;

                // Read the response body once for all subsequent extractions
                responseBody.Seek(0, SeekOrigin.Begin);
                string responseText;
                using (var reader = new StreamReader(responseBody, leaveOpen: true))
                {
                    responseText = reader.ReadToEnd();
                }

                // Extract and calculate tool usage costs (provider-hosted tools like Groq code_interpreter)
                var toolUsageData = UsageExtractor.ExtractToolUsage(responseText, providerTypeEnum, _logger);
                decimal? toolCost = null;
                string? toolUsageJson = null;
                List<string>? unconfiguredTools = null;

                if (toolUsageData != null)
                {
                    var toolCostResult = await toolCostCalculationService.CalculateToolCostsAsync(toolUsageData, providerTypeEnum);
                    toolUsageJson = toolCostCalculationService.SerializeToolUsage(toolUsageData);
                    unconfiguredTools = toolCostResult.UnconfiguredToolNames;

                    if (!toolCostResult.Failed)
                    {
                        toolCost = toolCostResult.TotalCost;
                        _logger.LogDebug("Tool usage detected: {ToolUsageJson}, Cost: ${ToolCost}", toolUsageJson, toolCost);
                    }
                    else
                    {
                        // Cost calculation failed — record usage but log error
                        toolCost = 0m;
                        _logger.LogError("Tool cost calculation failed for provider {ProviderType}. " +
                            "Tool usage recorded but cost set to $0. Review provider tool configuration.",
                            providerTypeEnum);
                    }

                    // Log audit event for unconfigured tools only when there's also billable cost from other tools.
                    // When toolCost == 0, BillingPolicyHandler.LogZeroCostBilling already emits ToolUsageMissingCostConfig.
                    if (toolCostResult.HasUnconfiguredTools && toolCost > 0)
                    {
                        var virtualKeyIdForAudit = (int)context.Items["VirtualKeyId"]!;
                        billingAuditService.LogBillingEvent(new Configuration.Entities.BillingAuditEvent
                        {
                            EventType = Configuration.Entities.BillingAuditEventType.ToolUsageMissingCostConfig,
                            VirtualKeyId = virtualKeyIdForAudit,
                            Model = model,
                            RequestId = context.TraceIdentifier,
                            RequestPath = context.Request.Path.ToString(),
                            HttpStatusCode = context.Response.StatusCode,
                            ProviderType = providerType,
                            ToolUsageJson = toolUsageJson,
                            ToolUsageCost = toolCost,
                            FailureReason = $"Unconfigured tools: {string.Join(", ", toolCostResult.UnconfiguredToolNames)}"
                        });
                        UsageMetrics.BillingAuditEvents.WithLabels("ToolUsageMissingCostConfig", providerType).Inc();
                    }
                }

                // Extract chat tool calls (user-defined function/tool calls in the response)
                string? chatToolCallsJson = null;
                if (endpointType == "chat")
                {
                    var chatToolCalls = UsageExtractor.ExtractChatToolCalls(responseText, _logger);
                    chatToolCallsJson = UsageExtractor.SerializeChatToolCalls(chatToolCalls);

                    if (chatToolCallsJson != null)
                    {
                        _logger.LogDebug("Chat tool calls detected: {ChatToolCallsJson}", chatToolCallsJson);
                    }
                }

                // Add agentic function-execution cost (in-chat function/tool calls executed by the
                // orchestrator, e.g. Exa/Tavily search). The controller stores this in HttpContext.Items
                // for both streaming and non-streaming responses; the streaming path already bills it,
                // but this non-streaming path previously dropped it, so those executions were free.
                decimal functionExecutionCost = 0m;
                if (endpointType == "chat"
                    && context.Items.TryGetValue(HttpContextKeys.ChatFunctionCost, out var funcCostObj)
                    && funcCostObj is decimal funcCost)
                {
                    functionExecutionCost = funcCost;
                    if (functionExecutionCost > 0)
                    {
                        _logger.LogDebug("Non-streaming function executions detected, total cost: {Cost:C}",
                            functionExecutionCost);
                    }
                }

                // Add tool cost and function-execution cost to total cost
                var totalCost = cost + (toolCost ?? 0m) + functionExecutionCost;

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels(endpointType, "success").Inc();

                if (usage.PromptTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);

                if (usage.CompletionTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);

                if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "cached_input").Inc(usage.CachedInputTokens.Value);

                if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value > 0)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "cached_write").Inc(usage.CachedWriteTokens.Value);

                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType).Inc(Convert.ToDouble(totalCost));

                // Record business metrics for Grafana dashboards (real-time counters)
                var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
                BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
                BusinessMetricsService.RecordTokens(model, providerType, usage.PromptTokens ?? 0, usage.CompletionTokens ?? 0, usage.CachedInputTokens, usage.CachedWriteTokens);
                BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
                if (totalCost > 0)
                {
                    BusinessMetricsService.RecordCost(providerType, model, endpointType, Convert.ToDouble(totalCost));
                }

                // Record prompt caching metrics
                RecordPromptCachingMetrics(context, usage, model, providerType);
                await RecordPromptCachingSavingsAsync(context, costCalculationService, model, usage);

                // Update spend using batch service only if there's a cost
                if (totalCost > 0)
                {
                    if (await RecordSpendOrSettleReservationAsync(
                            context,
                            virtualKeyId,
                            totalCost,
                            batchSpendService,
                            virtualKeyService))
                    {
                        LogSuccessfulBilling(context, model, usage, totalCost, providerType, billingAuditService, toolUsageJson, toolCost);
                    }
                }
                else if (!pricingResult.Failed)
                {
                    if (context.GetRequestAccountingSnapshot()?.Reservation is not null)
                    {
                        await RecordSpendOrSettleReservationAsync(
                            context,
                            virtualKeyId,
                            0m,
                            batchSpendService,
                            virtualKeyService);
                    }
                    _logger.LogDebug("Zero total cost calculated for {Model} with usage {Usage}, tool cost: ${ToolCost}",
                        model, JsonSerializer.Serialize(usage), toolCost);
                    UsageMetrics.ZeroCostEvents.WithLabels(model, "zero_cost").Inc();
                    LogZeroCostBilling(context, model, usage, totalCost, providerType, billingAuditService, toolUsageJson, toolCost);
                }
                else
                {
                    _logger.LogWarning(
                        "Request {RequestId} retained at zero cost after pricing failure; billing audit requires reconciliation",
                        context.TraceIdentifier);
                }

                // Build metadata: prefer chat tool calls, fall back to provider tool usage
                var metadata = chatToolCallsJson ?? toolUsageJson;

                // Always log the request regardless of cost
                await LogRequestAsync(context, virtualKeyId, model, usage, totalCost, requestLogService, metadata);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse response JSON for usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("json_parse_error", endpointType).Inc();
                LogJsonParseError(context, ex, billingAuditService);
            }
            catch (ToolCostCalculationException)
            {
                // Pricing provider-hosted tools is mandatory. Propagate the failure so the
                // successful provider response is not delivered as an unbilled request.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in usage tracking");
                UsageMetrics.UsageTrackingFailures.WithLabels("unexpected_error", endpointType).Inc();
                LogUnexpectedError(context, ex, billingAuditService);
            }
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
