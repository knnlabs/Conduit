using System.Text.Json;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Controllers;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.Utilities;
using Prometheus;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Middleware
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
                        requestLogService, virtualKeyService, billingAuditService, toolCostCalculationService);
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
                    billingAuditService,
                    toolCostCalculationService);

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
                   path.Contains("/functions/execute");
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

                // Parse provider type enum for tool usage parsing
                var providerTypeEnum = Enum.TryParse<ProviderType>(providerType, true, out var parsedProviderType)
                    ? parsedProviderType
                    : ProviderType.OpenAI; // Default fallback

                // Calculate base cost from token usage - prefer ID-based lookup if ModelCostId is available
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

                // Reset stream position after JsonDocument.ParseAsync for tool usage extraction
                responseBody.Seek(0, SeekOrigin.Begin);

                // Extract and calculate tool usage costs (provider-hosted tools like Groq code_interpreter)
                var toolUsageData = ExtractToolUsageFromResponse(responseBody, providerTypeEnum);
                decimal? toolCost = null;
                string? toolUsageJson = null;

                if (toolUsageData != null)
                {
                    var calculatedToolCost = await toolCostCalculationService.CalculateToolCostsAsync(toolUsageData, providerTypeEnum);
                    toolUsageJson = toolCostCalculationService.SerializeToolUsage(toolUsageData);

                    if (calculatedToolCost >= 0)
                    {
                        toolCost = calculatedToolCost;
                        _logger.LogDebug("Tool usage detected: {ToolUsageJson}, Cost: ${ToolCost}", toolUsageJson, toolCost);
                    }
                    else
                    {
                        // Cost calculation failed (returned -1) — record usage but log error
                        toolCost = 0m;
                        _logger.LogError("Tool cost calculation failed for provider {ProviderType}. " +
                            "Tool usage recorded but cost set to $0. Review provider tool configuration.",
                            providerTypeEnum);
                    }
                }

                // Extract chat tool calls (user-defined function/tool calls in the response)
                string? chatToolCallsJson = null;
                if (endpointType == "chat")
                {
                    responseBody.Seek(0, SeekOrigin.Begin);
                    using var reader = new StreamReader(responseBody, leaveOpen: true);
                    var responseText = reader.ReadToEnd();
                    var chatToolCalls = UsageExtractor.ExtractChatToolCalls(responseText, _logger);
                    chatToolCallsJson = UsageExtractor.SerializeChatToolCalls(chatToolCalls);

                    if (chatToolCallsJson != null)
                    {
                        _logger.LogDebug("Chat tool calls detected: {ChatToolCallsJson}", chatToolCallsJson);
                    }
                }

                // Add tool cost to total cost
                var totalCost = cost + (toolCost ?? 0m);

                // Update metrics
                UsageMetrics.UsageTrackingRequests.WithLabels(endpointType, "success").Inc();

                if (usage.PromptTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);

                if (usage.CompletionTokens.HasValue)
                    UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);

                UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType).Inc(Convert.ToDouble(totalCost));

                // Record business metrics for Grafana dashboards (real-time counters)
                var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
                BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
                BusinessMetricsService.RecordTokens(model, providerType, usage.PromptTokens ?? 0, usage.CompletionTokens ?? 0);
                BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
                if (totalCost > 0)
                {
                    BusinessMetricsService.RecordCost(providerType, model, endpointType, Convert.ToDouble(totalCost));
                }

                // Update spend using batch service only if there's a cost
                if (totalCost > 0)
                {
                    await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, totalCost, batchSpendService, virtualKeyService, _logger);
                    LogSuccessfulBilling(context, model, usage, totalCost, providerType, billingAuditService, toolUsageJson, toolCost);
                }
                else
                {
                    _logger.LogDebug("Zero total cost calculated for {Model} with usage {Usage}, tool cost: ${ToolCost}",
                        model, JsonSerializer.Serialize(usage), toolCost);
                    UsageMetrics.ZeroCostEvents.WithLabels(model, "zero_cost").Inc();
                    LogZeroCostBilling(context, model, usage, totalCost, providerType, billingAuditService, toolUsageJson, toolCost);
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
            IBillingAuditService billingAuditService,
            IToolCostCalculationService toolCostCalculationService)
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
            
            // Parse provider type enum for tool usage
            var providerTypeEnum = Enum.TryParse<ProviderType>(providerType, true, out var parsedProviderType) 
                ? parsedProviderType 
                : ProviderType.OpenAI;
            
            // Extract tool usage from streaming context if available (provider-hosted tools)
            var toolUsageData = context.Items.TryGetValue("StreamingToolUsage", out var toolObj)
                ? toolObj as ToolUsageData
                : null;

            decimal? toolCost = null;
            string? toolUsageJson = null;

            if (toolUsageData != null)
            {
                toolCost = await toolCostCalculationService.CalculateToolCostsAsync(toolUsageData, providerTypeEnum);
                toolUsageJson = toolCostCalculationService.SerializeToolUsage(toolUsageData);
                _logger.LogDebug("Streaming tool usage detected: {ToolUsageJson}, Cost: ${ToolCost}", toolUsageJson, toolCost);
            }

            // Extract function execution results from streaming context (richer data with execution status)
            string? chatToolCallsJson = null;
            decimal functionExecutionCost = 0m;

            if (endpointType == "chat" && context.Items.TryGetValue(HttpContextKeys.ChatFunctionCalls, out var functionResultsObj)
                && functionResultsObj is List<FunctionExecutionResultForLogging> functionResults
                && functionResults.Count > 0)
            {
                // Use richer function execution data (includes status, cost, execution ID)
                chatToolCallsJson = FunctionExecutionSerializer.SerializeFunctionExecutionResults(functionResults);

                // Get total function cost from HttpContext
                if (context.Items.TryGetValue(HttpContextKeys.ChatFunctionCost, out var funcCostObj)
                    && funcCostObj is decimal funcCost)
                {
                    functionExecutionCost = funcCost;
                }

                _logger.LogDebug("Streaming function executions detected: {Count} functions, total cost: {Cost:C}",
                    functionResults.Count, functionExecutionCost);
            }
            // Fallback to basic tool call info if no execution results available
            else if (endpointType == "chat" && context.Items.TryGetValue("StreamingChatToolCalls", out var streamingToolCallsObj)
                && streamingToolCallsObj is List<ConduitLLM.Core.Models.ToolCall> streamingToolCalls
                && streamingToolCalls.Count > 0)
            {
                // Convert to ChatToolCallData format (basic info only - no execution results)
                var chatToolCallData = new ChatToolCallData
                {
                    ToolCalls = streamingToolCalls.Select(tc => new ChatToolCallItem
                    {
                        Id = tc.Id,
                        Type = tc.Type,
                        FunctionName = tc.Function?.Name,
                        HasArguments = !string.IsNullOrEmpty(tc.Function?.Arguments)
                    }).ToList()
                };
                chatToolCallsJson = UsageExtractor.SerializeChatToolCalls(chatToolCallData);
                _logger.LogDebug("Streaming chat tool calls detected (basic): {ChatToolCallsJson}", chatToolCallsJson);
            }

            // Calculate base cost and add tool cost (both provider tools and function executions)
            // Prefer ID-based lookup if ModelCostId is available
            decimal baseCost;
            if (context.Items.TryGetValue(HttpContextKeys.ModelCostId, out var modelCostIdObj) &&
                modelCostIdObj is int modelCostId)
            {
                baseCost = await costCalculationService.CalculateCostByIdAsync(modelCostId, usage);
            }
            else
            {
                baseCost = await costCalculationService.CalculateCostAsync(model, usage);
            }
            var cost = baseCost + (toolCost ?? 0m) + functionExecutionCost;

            // Update metrics
            UsageMetrics.UsageTrackingRequests.WithLabels(endpointType + "_stream", "success").Inc();

            if (usage.PromptTokens.HasValue)
                UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "prompt").Inc(usage.PromptTokens.Value);

            if (usage.CompletionTokens.HasValue)
                UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "completion").Inc(usage.CompletionTokens.Value);

            UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType + "_stream").Inc(Convert.ToDouble(cost));

            // Record business metrics for Grafana dashboards (real-time counters)
            var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
            BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
            BusinessMetricsService.RecordTokens(model, providerType, usage.PromptTokens ?? 0, usage.CompletionTokens ?? 0);
            BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
            if (cost > 0)
            {
                BusinessMetricsService.RecordCost(providerType, model, endpointType, Convert.ToDouble(cost));
            }

            // Update spend only if there's a cost
            if (cost > 0)
            {
                await SpendUpdateHelper.UpdateSpendAsync(virtualKeyId, cost, batchSpendService, virtualKeyService, _logger);
                LogStreamingBilling(context, model, usage, cost, providerType, isEstimated, billingAuditService, toolUsageJson, toolCost);
            }
            else
            {
                UsageMetrics.ZeroCostEvents.WithLabels(model ?? "unknown", "streaming_zero").Inc();
                LogZeroCostBilling(context, model ?? "unknown", usage, cost, providerType, billingAuditService, toolUsageJson, toolCost);
            }

            // Build metadata: prefer chat tool calls, fall back to provider tool usage
            var metadata = chatToolCallsJson ?? toolUsageJson;

            // Always log the request regardless of cost
            await LogRequestAsync(context, virtualKeyId, model ?? "unknown", usage, cost, requestLogService, metadata);
        }

        private async Task LogRequestAsync(
            HttpContext context,
            int virtualKeyId,
            string model,
            Usage usage,
            decimal cost,
            IRequestLogService requestLogService,
            string? metadata = null)
        {
            try
            {
                var requestType = UsageExtractor.DetermineRequestType(context.Request.Path);

                // Extract provider info from HttpContext.Items (set by controllers)
                int? providerId = context.Items.TryGetValue("ProviderId", out var providerIdObj) && providerIdObj is int pid
                    ? pid
                    : null;
                var providerType = context.Items.TryGetValue("ProviderType", out var providerTypeObj)
                    ? providerTypeObj?.ToString()
                    : null;

                var logRequest = new LogRequestDto
                {
                    VirtualKeyId = virtualKeyId,
                    ModelName = model,
                    ProviderId = providerId,
                    ProviderType = providerType,
                    RequestType = requestType,
                    InputTokens = usage.PromptTokens ?? 0,
                    OutputTokens = usage.CompletionTokens ?? 0,
                    Cost = cost,
                    ResponseTimeMs = UsageExtractor.GetResponseTime(context),
                    UserId = context.User?.Identity?.Name,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    RequestPath = context.Request.Path.ToString(),
                    StatusCode = context.Response.StatusCode,
                    Metadata = metadata
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

        #region Billing Audit Logging

        private async Task LogBillingDecisionAsync(HttpContext context, IBillingAuditService billingAuditService)
        {
            await BillingPolicyHandler.LogBillingDecisionAsync(context, billingAuditService, _logger);
        }

        private void LogSuccessfulBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogSuccessfulBilling(context, model, usage, cost, providerType, billingAuditService, _logger, toolUsageJson, toolCost);
        }

        /// <summary>
        /// Extracts tool usage data from the response body.
        /// </summary>
        /// <param name="responseBody">The response body stream</param>
        /// <param name="providerType">The provider type</param>
        /// <returns>Tool usage data or null if no tools were used</returns>
        private ToolUsageData? ExtractToolUsageFromResponse(MemoryStream responseBody, ProviderType providerType)
        {
            try
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(responseBody, leaveOpen: true);
                var responseText = reader.ReadToEnd();
                
                return UsageExtractor.ExtractToolUsage(responseText, providerType, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract tool usage from response");
                return null;
            }
        }

        private void LogZeroCostBilling(HttpContext context, string model, Usage usage, decimal cost,
            string providerType, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogZeroCostBilling(context, model, usage, cost, providerType, billingAuditService, toolUsageJson, toolCost, _logger);
        }

        private void LogMissingUsageData(HttpContext context, IBillingAuditService billingAuditService)
        {
            BillingPolicyHandler.LogMissingUsageData(context, billingAuditService);
        }

        private void LogStreamingBilling(HttpContext context, string model, Usage usage, decimal cost, 
            string providerType, bool isEstimated, IBillingAuditService billingAuditService, string? toolUsageJson = null, decimal? toolCost = null)
        {
            BillingPolicyHandler.LogStreamingBilling(context, model, usage, cost, providerType, isEstimated, billingAuditService, _logger, toolUsageJson, toolCost);
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