using System.Diagnostics;
using System.Text;
using System.Text.Json;

using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Gateway.Constants;
using ConduitLLM.Gateway.Metrics;
using ConduitLLM.Gateway.Services;
using GatewayOpsMetrics = ConduitLLM.Gateway.Services.GatewayOperationsMetricsService;

using Microsoft.AspNetCore.Mvc;

namespace ConduitLLM.Gateway.Controllers
{
    public partial class ChatController
    {
        private async Task<IActionResult> HandleNonStreamingRequestAsync(
            ChatCompletionRequest request,
            int? virtualKeyId,
            Stopwatch operationStopwatch,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling non-streaming request.");
            var response = await _conduit.CreateChatCompletionAsync(request, null, virtualKeyId, cancellationToken);
            await CaptureSelectedRouteAsync(request);

            if (response.Usage is not null)
            {
                HttpContext.Items[HttpContextKeys.NonStreamingUsage] = response.Usage;
            }
            HttpContext.Items[HttpContextKeys.PromptCachingEligible] =
                request.PromptCachingIntent is not null ||
                (HttpContext.Items.TryGetValue(HttpContextKeys.PromptCachingEligible, out var eligible) && eligible is true);

            if (response.AgenticMetrics?.FunctionCalls != null && response.AgenticMetrics.FunctionCalls.Count > 0)
            {
                StoreFunctionExecutionResults(response.AgenticMetrics);
            }
            if (response.AgenticMetrics?.ProviderCalls.Count > 0)
            {
                HttpContext.Items[HttpContextKeys.ChatProviderCalls] = response.AgenticMetrics.ProviderCalls;
            }

            // Stash the provider-reported cost via the side channel so the middleware can bill from it.
            // It is intentionally not serialized into the response body (server-only), so the middleware
            // cannot recover it by re-parsing the body the way it does for token counts.
            if (response.Usage?.ProviderReportedCostUsd is decimal providerReportedCost)
            {
                HttpContext.Items[HttpContextKeys.ProviderReportedCost] = providerReportedCost;
            }

            GatewayOpsMetrics.RecordLlmOperation("chat_completion", request.Model, "success", operationStopwatch.Elapsed.TotalSeconds);
            return Ok(response);
        }

        private void StoreFunctionExecutionResults(AgenticExecutionMetrics agenticMetrics)
        {
            var functionExecutionResults = agenticMetrics.FunctionCalls
                .Select(fc => new FunctionExecutionResultForLogging
                {
                    ToolCallId = fc.ToolCallId,
                    FunctionName = fc.FunctionName,
                    Status = fc.Success ? "completed" : "failed",
                    Cost = fc.Cost,
                    ErrorMessage = fc.ErrorMessage,
                    FunctionExecutionId = fc.FunctionExecutionId
                })
                .ToList();

            HttpContext.Items[HttpContextKeys.ChatFunctionCalls] = functionExecutionResults;
            HttpContext.Items[HttpContextKeys.ChatFunctionCost] = agenticMetrics.TotalFunctionCost;
            _logger.LogDebug(
                "Stored {Count} function execution results for non-streaming request logging, total cost: {Cost:C}",
                functionExecutionResults.Count, agenticMetrics.TotalFunctionCost);
        }

        private async Task HandleStreamingRequestAsync(
            ChatCompletionRequest request,
            int? virtualKeyId,
            Stopwatch operationStopwatch,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling streaming request.");
            GatewayOpsMetrics.RecordStreamingRequest(request.Model, "started");

            var bufferingFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
            bufferingFeature?.DisableBuffering();

            var response = HttpContext.Response;
            var sseWriter = response.CreateEnhancedSSEWriter(_jsonSerializerOptions);

            var requestId = Guid.NewGuid().ToString();
            response.Headers["X-Request-ID"] = requestId;

            var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
            var providerId = modelMapping?.ProviderId.ToString() ?? "unknown";

            _logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", LoggingSanitizer.S(request.Model), providerId);
            var metricsCollector = new StreamingMetricsCollector(requestId, request.Model, providerId);
            var state = new StreamingAccumulatorState();
            var firstChunkTime = DateTime.UtcNow;

            try
            {
                await foreach (var chunk in _conduit.StreamChatCompletionAsync(
                    request, null, virtualKeyId,
                    CreateToolExecutionCallback(sseWriter, state),
                    cancellationToken))
                {
                    state.ChunkCount++;
                    if (state.ChunkCount == 1)
                    {
                        _logger.LogInformation("First chunk received at {Time}ms", (DateTime.UtcNow - firstChunkTime).TotalMilliseconds);
                    }

                    AccumulateContent(chunk, state);
                    AccumulateToolCalls(chunk, state);
                    CaptureUsageData(chunk, request, state);
                    await WriteChunkToStream(chunk, sseWriter, metricsCollector);
                    await EmitPeriodicMetrics(chunk, sseWriter, metricsCollector);
                }

                await WriteStreamingCompletionEventsAsync(state, metricsCollector, sseWriter);

                _logger.LogInformation("Streaming completed: {ChunkCount} chunks over {Duration}ms",
                    state.ChunkCount, (DateTime.UtcNow - firstChunkTime).TotalMilliseconds);
                GatewayOpsMetrics.RecordLlmOperation("chat_completion", request.Model, "success", operationStopwatch.Elapsed.TotalSeconds);
                GatewayOpsMetrics.RecordStreamingRequest(request.Model, "completed");
            }
            catch (Exception streamEx)
            {
                _logger.LogError(streamEx, "Error in stream processing");
                await sseWriter.WriteErrorEventAsync(streamEx.Message);
                GatewayOpsMetrics.RecordLlmOperation("chat_completion", request.Model, "error", operationStopwatch.Elapsed.TotalSeconds);
                GatewayOpsMetrics.RecordStreamingRequest(request.Model, "error");
            }
            finally
            {
                await CaptureSelectedRouteAsync(request);
                // Billing data must survive provider failures and client disconnects. Do not use the
                // request token here: it is normally cancelled precisely when this fallback is needed.
                await StoreStreamingResultsAsync(request, state, CancellationToken.None);
            }
        }

        private Func<ToolExecutionEvent, CancellationToken, Task> CreateToolExecutionCallback(
            EnhancedSSEResponseWriter sseWriter,
            StreamingAccumulatorState state)
        {
            return async (toolEvent, ct) =>
            {
                await sseWriter.WriteToolExecutingEventAsync(toolEvent, ct);

                if (toolEvent.Status == "completed" || toolEvent.Status == "failed")
                {
                    state.FunctionExecutionResults.Add(new FunctionExecutionResultForLogging
                    {
                        ToolCallId = toolEvent.ToolCallId,
                        FunctionName = toolEvent.FunctionName,
                        Status = toolEvent.Status,
                        Cost = toolEvent.Cost,
                        ErrorMessage = toolEvent.ErrorMessage,
                        FunctionExecutionId = toolEvent.FunctionExecutionId
                    });
                    state.TotalFunctionCost += toolEvent.Cost ?? 0m;
                }
            };
        }

        private static void AccumulateContent(ChatCompletionChunk chunk, StreamingAccumulatorState state)
        {
            if (chunk.Choices == null) return;
            foreach (var choice in chunk.Choices)
            {
                if (!string.IsNullOrEmpty(choice.Delta?.Content))
                {
                    state.ContentAccumulator.Append(choice.Delta.Content);
                }
                if (!string.IsNullOrEmpty(choice.Delta?.Reasoning))
                {
                    state.ContentAccumulator.Append(choice.Delta.Reasoning);
                }
            }
        }

        private static void AccumulateToolCalls(ChatCompletionChunk chunk, StreamingAccumulatorState state)
        {
            if (chunk.Choices == null) return;

            foreach (var choice in chunk.Choices)
            {
                if (choice.Delta?.ToolCalls is not { } toolCallDeltas) continue;

                foreach (var toolCallChunk in toolCallDeltas)
                {
                    if (!state.AccumulatedToolCalls.ContainsKey(toolCallChunk.Index))
                    {
                        state.AccumulatedToolCalls[toolCallChunk.Index] = new ConduitLLM.Core.Models.ToolCall
                        {
                            Id = toolCallChunk.Id ?? string.Empty,
                            Type = toolCallChunk.Type ?? "function",
                            Function = new ConduitLLM.Core.Models.FunctionCall
                            {
                                Name = toolCallChunk.Function?.Name ?? string.Empty,
                                Arguments = toolCallChunk.Function?.Arguments ?? string.Empty
                            }
                        };
                    }
                    else
                    {
                        var existing = state.AccumulatedToolCalls[toolCallChunk.Index];
                        if (!string.IsNullOrEmpty(toolCallChunk.Function?.Arguments) && existing.Function != null)
                        {
                            existing.Function.Arguments += toolCallChunk.Function.Arguments;
                        }
                        if (!string.IsNullOrEmpty(toolCallChunk.Function?.Name) && existing.Function != null)
                        {
                            existing.Function.Name = toolCallChunk.Function.Name;
                        }
                    }
                }
            }
        }

        private static void CaptureUsageData(ChatCompletionChunk chunk, ChatCompletionRequest request, StreamingAccumulatorState state)
        {
            if (chunk.ProviderCallUsage != null)
            {
                state.ProviderCalls[chunk.ProviderCallUsage.Iteration] = chunk.ProviderCallUsage.Usage;
            }
            if (chunk.Usage != null)
            {
                state.StreamingUsage = chunk.Usage;
                state.StreamingModel = chunk.Model ?? request.Model;
            }
        }

        private async Task WriteChunkToStream(
            ChatCompletionChunk chunk,
            EnhancedSSEResponseWriter sseWriter,
            StreamingMetricsCollector metricsCollector)
        {
            if (chunk.Choices?.Count > 0 && !string.IsNullOrEmpty(chunk.Choices[0].Delta?.Reasoning))
            {
                await sseWriter.WriteReasoningEventAsync(chunk.Choices[0].Delta.Reasoning!);
                await sseWriter.WriteContentEventAsync(chunk);
            }
            else
            {
                await sseWriter.WriteContentEventAsync(chunk);
            }
        }

        private async Task EmitPeriodicMetrics(
            ChatCompletionChunk chunk,
            EnhancedSSEResponseWriter sseWriter,
            StreamingMetricsCollector metricsCollector)
        {
            if (chunk?.Choices?.Count > 0)
            {
                var hasContent = chunk.Choices.Any(c => !string.IsNullOrEmpty(c.Delta?.Content));
                if (hasContent)
                {
                    if (metricsCollector.GetMetrics().TimeToFirstTokenMs == null)
                    {
                        metricsCollector.RecordFirstToken();
                    }
                    else
                    {
                        metricsCollector.RecordToken();
                    }
                }

                if (metricsCollector.ShouldEmitMetrics())
                {
                    _logger.LogDebug("Emitting streaming metrics");
                    await sseWriter.WriteMetricsEventAsync(metricsCollector.GetMetrics());
                }
            }
        }

        private async Task StoreStreamingResultsAsync(
            ChatCompletionRequest request,
            StreamingAccumulatorState state,
            CancellationToken cancellationToken)
        {
            HttpContext.Items[HttpContextKeys.PromptCachingEligible] =
                request.PromptCachingIntent is not null ||
                (HttpContext.Items.TryGetValue(HttpContextKeys.PromptCachingEligible, out var eligible) && eligible is true);

            // Store usage data for middleware
            if (state.StreamingUsage != null)
            {
                HttpContext.Items["StreamingUsage"] = state.StreamingUsage;
                HttpContext.Items["StreamingModel"] = state.StreamingModel;
                HttpContext.Items["UsageIsEstimated"] = false;
            }

            else if (state.ContentAccumulator.Length > 0 || state.AccumulatedToolCalls.Count > 0)
            {
                _logger.LogWarning("No usage data received from provider for streaming response, estimating usage for model {Model}", LoggingSanitizer.S(request.Model));
                await EstimateStreamingUsageAsync(request, state, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No output accumulated from streaming response, cannot estimate usage");
            }

            if (state.ProviderCalls.Count > 0)
            {
                HttpContext.Items[HttpContextKeys.ChatProviderCalls] = state.ProviderCalls
                    .OrderBy(entry => entry.Key)
                    .Select(entry => new ProviderCallUsage { Iteration = entry.Key, Usage = entry.Value })
                    .ToList();
            }

            // Store tool calls for request logging
            if (state.AccumulatedToolCalls.Count > 0)
            {
                HttpContext.Items["StreamingChatToolCalls"] = state.AccumulatedToolCalls
                    .OrderBy(kv => kv.Key)
                    .Select(kv => kv.Value)
                    .ToList();
                _logger.LogDebug("Stored {Count} accumulated tool calls for request logging", state.AccumulatedToolCalls.Count);
            }

            // Store function execution results
            if (state.FunctionExecutionResults.Count > 0)
            {
                HttpContext.Items[HttpContextKeys.ChatFunctionCalls] = state.FunctionExecutionResults;
                HttpContext.Items[HttpContextKeys.ChatFunctionCost] = state.TotalFunctionCost;
                _logger.LogDebug(
                    "Stored {Count} function execution results for request logging, total cost: {Cost:C}",
                    state.FunctionExecutionResults.Count, state.TotalFunctionCost);
            }

        }

        private async Task WriteStreamingCompletionEventsAsync(
            StreamingAccumulatorState state,
            StreamingMetricsCollector metricsCollector,
            EnhancedSSEResponseWriter sseWriter)
        {
            _logger.LogInformation("StreamingUsage before GetFinalMetrics: {Usage}",
                state.StreamingUsage != null ?
                $"Prompt={state.StreamingUsage.PromptTokens}, Completion={state.StreamingUsage.CompletionTokens}, Total={state.StreamingUsage.TotalTokens}" :
                "null");

            var finalMetrics = metricsCollector.GetFinalMetrics(state.StreamingUsage);

            _logger.LogInformation("FinalMetrics after GetFinalMetrics: PromptTokens={Prompt}, CompletionTokens={Completion}, TotalTokens={Total}",
                finalMetrics.PromptTokens, finalMetrics.CompletionTokens, finalMetrics.TotalTokens);

            await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);
            await sseWriter.WriteDoneEventAsync();
        }

        private async Task EstimateStreamingUsageAsync(
            ChatCompletionRequest request,
            StreamingAccumulatorState state,
            CancellationToken cancellationToken)
        {
            try
            {
                var completionOutput = state.ContentAccumulator.ToString();
                if (state.AccumulatedToolCalls.Count > 0)
                {
                    completionOutput += JsonSerializer.Serialize(
                        state.AccumulatedToolCalls.OrderBy(entry => entry.Key).Select(entry => entry.Value),
                        _jsonSerializerOptions);
                }

                var estimatedUsage = await _usageEstimationService.EstimateUsageFromStreamingResponseAsync(
                    state.StreamingModel ?? request.Model,
                    request.Messages,
                    completionOutput,
                    cancellationToken);

                HttpContext.Items["StreamingUsage"] = estimatedUsage;
                HttpContext.Items["StreamingModel"] = state.StreamingModel ?? request.Model;
                HttpContext.Items["UsageIsEstimated"] = true;

                _logger.LogInformation(
                    "Successfully estimated usage for streaming response: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                    estimatedUsage.PromptTokens, estimatedUsage.CompletionTokens, estimatedUsage.TotalTokens);
            }
            catch (Exception estEx)
            {
                _logger.LogError(estEx, "Failed to estimate usage for streaming response");
            }
        }

        /// <summary>
        /// Mutable state accumulated during streaming chunk processing.
        /// </summary>
        private sealed class StreamingAccumulatorState
        {
            public int ChunkCount { get; set; }
            public Usage? StreamingUsage { get; set; }
            public string? StreamingModel { get; set; }
            public StringBuilder ContentAccumulator { get; } = new();
            public Dictionary<int, ConduitLLM.Core.Models.ToolCall> AccumulatedToolCalls { get; } = new();
            public List<FunctionExecutionResultForLogging> FunctionExecutionResults { get; } = new();
            public decimal TotalFunctionCost { get; set; }
            public Dictionary<int, Usage> ProviderCalls { get; } = new();
        }
    }
}
