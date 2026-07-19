using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Interfaces;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Controllers;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Services;
using ConduitLLM.Gateway.Utilities;
using IVirtualKeyService = ConduitLLM.Core.Interfaces.IVirtualKeyService;

namespace ConduitLLM.Gateway.Middleware
{
    public partial class UsageTrackingMiddleware
    {
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
                _logger.LogDebug("No streaming usage data found for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                UsageMetrics.UsageTrackingFailures.WithLabels("no_streaming_usage", endpointType).Inc();
                LogMissingStreamingUsage(context, billingAuditService);
                return;
            }

            if (!context.Items.TryGetValue("StreamingModel", out var modelObj) ||
                modelObj is not string model)
            {
                _logger.LogWarning("No streaming model found for {Path}", LoggingSanitizer.S(context.Request.Path.ToString()));
                UsageMetrics.UsageTrackingFailures.WithLabels("no_streaming_model", endpointType).Inc();
                return;
            }

            // Stamp the provider billing policy onto the streaming usage (which already carries the
            // provider-reported cost captured from the final SSE chunk) before cost calculation.
            ApplyProviderBillingPolicy(context, usage);

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
                var toolCostResult = await toolCostCalculationService.CalculateToolCostsAsync(toolUsageData, providerTypeEnum);
                toolUsageJson = toolCostCalculationService.SerializeToolUsage(toolUsageData);

                if (!toolCostResult.Failed)
                {
                    toolCost = toolCostResult.TotalCost;
                    _logger.LogDebug("Streaming tool usage detected: {ToolUsageJson}, Cost: ${ToolCost}", toolUsageJson, toolCost);
                }
                else
                {
                    toolCost = 0m;
                    _logger.LogError("Streaming tool cost calculation failed for provider {ProviderType}.", providerTypeEnum);
                }

                // Only emit when there's also billable cost — BillingPolicyHandler handles the zero-cost case
                if (toolCostResult.HasUnconfiguredTools && toolCost > 0)
                {
                    billingAuditService.LogBillingEvent(new Configuration.Entities.BillingAuditEvent
                    {
                        EventType = Configuration.Entities.BillingAuditEventType.ToolUsageMissingCostConfig,
                        VirtualKeyId = virtualKeyId,
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

            if (usage.CachedInputTokens.HasValue && usage.CachedInputTokens.Value > 0)
                UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "cached_input").Inc(usage.CachedInputTokens.Value);

            if (usage.CachedWriteTokens.HasValue && usage.CachedWriteTokens.Value > 0)
                UsageMetrics.UsageTrackingTokens.WithLabels(model, providerType, "cached_write").Inc(usage.CachedWriteTokens.Value);

            UsageMetrics.UsageTrackingCosts.WithLabels(model, providerType, endpointType + "_stream").Inc(Convert.ToDouble(cost));

            // Record business metrics for Grafana dashboards (real-time counters)
            var requestStatus = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 ? "success" : "error";
            BusinessMetricsService.RecordModelRequest(model, providerType, requestStatus);
            BusinessMetricsService.RecordTokens(model, providerType, usage.PromptTokens ?? 0, usage.CompletionTokens ?? 0, usage.CachedInputTokens, usage.CachedWriteTokens);
            BusinessMetricsService.RecordResponseTime(model, providerType, UsageExtractor.GetResponseTime(context) / 1000.0);
            if (cost > 0)
            {
                BusinessMetricsService.RecordCost(providerType, model, endpointType, Convert.ToDouble(cost));
            }

            // Record prompt caching metrics
            RecordPromptCachingMetrics(usage, model, providerType);
            await RecordPromptCachingSavingsAsync(context, costCalculationService, model, usage);

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
    }
}
