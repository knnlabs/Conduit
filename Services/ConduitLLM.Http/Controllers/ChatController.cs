using System.Text;
using System.Text.Json;

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Constants;
using ConduitLLM.Http.Services;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.Authorization;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles chat completion requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/chat")]
    [Authorize]
    [RequireBalance]
    [Tags("Chat")]
    public class ChatController : EventPublishingControllerBase
    {
        private readonly Conduit _conduit;
        private readonly ILogger<ChatController> _logger;
        private readonly ConduitLLM.Configuration.Interfaces.IModelProviderMappingService _modelMappingService;
        private readonly IOptions<ConduitSettings> _settings;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        private readonly IUsageEstimationService? _usageEstimationService;
        private readonly ConduitLLM.Functions.Interfaces.IFunctionConfigurationRepository? _functionConfigRepository;
        private readonly ConduitLLM.Configuration.Interfaces.IGlobalSettingsCacheService _globalSettingsCacheService;

        public ChatController(
            Conduit conduit,
            ILogger<ChatController> logger,
            ConduitLLM.Configuration.Interfaces.IModelProviderMappingService modelMappingService,
            IOptions<ConduitSettings> settings,
            JsonSerializerOptions jsonSerializerOptions,
            IPublishEndpoint publishEndpoint,
            ConduitLLM.Configuration.Interfaces.IGlobalSettingsCacheService globalSettingsCacheService,
            IUsageEstimationService? usageEstimationService = null,
            ConduitLLM.Functions.Interfaces.IFunctionConfigurationRepository? functionConfigRepository = null) : base(publishEndpoint, logger)
        {
            _conduit = conduit ?? throw new ArgumentNullException(nameof(conduit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _jsonSerializerOptions = jsonSerializerOptions ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
            _globalSettingsCacheService = globalSettingsCacheService ?? throw new ArgumentNullException(nameof(globalSettingsCacheService));
            _usageEstimationService = usageEstimationService;
            _functionConfigRepository = functionConfigRepository;
        }

        /// <summary>
        /// Creates a chat completion.
        /// </summary>
        /// <param name="request">The chat completion request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A chat completion response or a stream of server-sent events.</returns>
        [HttpPost("completions")]
        [ProducesResponseType(typeof(ChatCompletionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(OpenAIErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CreateChatCompletion(
            [FromBody] ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

            // Store streaming flag for middleware
            HttpContext.Items["IsStreamingRequest"] = request.Stream == true;
            
            // Get provider info for usage tracking
            try
            {
                var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping != null)
                {
                    HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                    HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
            }

            // Validate function calling parameters if provided
            if (request.FunctionConfigurationIds != null && request.FunctionConfigurationIds.Count > 0)
            {
                var validationError = await ValidateFunctionCallRequestAsync(request, cancellationToken);
                if (validationError != null)
                {
                    return validationError;
                }
            }

            // Apply defaults from GlobalSettings for agentic mode if not explicitly set
            if (!request.MaxAgenticIterations.HasValue)
            {
                request.MaxAgenticIterations = await _globalSettingsCacheService.GetMaxAgenticIterationsAsync();
            }
            if (!request.EnableAgenticMode.HasValue)
            {
                request.EnableAgenticMode = await _globalSettingsCacheService.GetDefaultAgenticModeEnabledAsync();
            }

            try
            {
                // Extract virtual key ID for function execution billing
                int? virtualKeyId = null;
                var virtualKeyIdClaim = User.FindFirst("VirtualKeyId")?.Value;
                if (!string.IsNullOrEmpty(virtualKeyIdClaim) && int.TryParse(virtualKeyIdClaim, out var keyId))
                {
                    virtualKeyId = keyId;
                }

                // Non-streaming path
                if (request.Stream != true)
                {
                    _logger.LogInformation("Handling non-streaming request.");
                    var response = await _conduit.CreateChatCompletionAsync(request, null, virtualKeyId, cancellationToken);

                    // Capture function execution results for request logging metadata (non-streaming)
                    if (response.AgenticMetrics?.FunctionCalls != null && response.AgenticMetrics.FunctionCalls.Count > 0)
                    {
                        var functionExecutionResults = response.AgenticMetrics.FunctionCalls
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
                        HttpContext.Items[HttpContextKeys.ChatFunctionCost] = response.AgenticMetrics.TotalFunctionCost;
                        _logger.LogDebug(
                            "Stored {Count} function execution results for non-streaming request logging, total cost: {Cost:C}",
                            functionExecutionResults.Count, response.AgenticMetrics.TotalFunctionCost);
                    }

                    return Ok(response);
                }
                else
                {
                    _logger.LogInformation("Handling streaming request.");
                    
                    // Disable response buffering for true streaming
                    var bufferingFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>();
                    bufferingFeature?.DisableBuffering();
                    
                    // Use enhanced SSE writer for performance metrics support
                    var response = HttpContext.Response;
                    var sseWriter = response.CreateEnhancedSSEWriter(_jsonSerializerOptions);
                    
                    // Always create metrics collector for token usage tracking
                    // Token usage is critical for billing and UI display, not just performance metrics
                    var requestId = Guid.NewGuid().ToString();
                    response.Headers["X-Request-ID"] = requestId;
                    
                    // Get provider info for metrics from model mapping service
                    var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                    // Use provider ID for metrics since it's the stable identifier
                    var providerId = modelMapping?.ProviderId.ToString() ?? "unknown";
                    
                    _logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", request.Model, providerId);
                    var metricsCollector = new StreamingMetricsCollector(
                        requestId,
                        request.Model,
                        providerId);

                    try
                    {
                        ConduitLLM.Core.Models.Usage? streamingUsage = null;
                        string? streamingModel = null;

                        // Accumulate content for usage estimation if needed
                        var contentAccumulator = new StringBuilder();

                        // Accumulate tool calls for request logging
                        var accumulatedToolCalls = new Dictionary<int, ConduitLLM.Core.Models.ToolCall>();

                        // Accumulate function execution results for request logging metadata
                        var functionExecutionResults = new List<FunctionExecutionResultForLogging>();
                        decimal totalFunctionCost = 0m;

                        var chunkCount = 0;
                        var firstChunkTime = DateTime.UtcNow;

                        await foreach (var chunk in _conduit.StreamChatCompletionAsync(
                            request,
                            null,
                            virtualKeyId,
                            async (eventData, ct) =>
                            {
                                // Write to SSE stream
                                await sseWriter.WriteToolExecutingEventAsync(eventData, ct);

                                // Capture completed/failed events for request logging
                                // Use reflection to safely extract properties from anonymous object
                                var eventType = eventData.GetType();
                                var statusProp = eventType.GetProperty("status");
                                if (statusProp != null)
                                {
                                    var status = statusProp.GetValue(eventData)?.ToString();
                                    if (status == "completed" || status == "failed")
                                    {
                                        var result = new FunctionExecutionResultForLogging
                                        {
                                            ToolCallId = eventType.GetProperty("tool_call_id")?.GetValue(eventData)?.ToString(),
                                            FunctionName = eventType.GetProperty("function_name")?.GetValue(eventData)?.ToString(),
                                            Status = status,
                                            Cost = eventType.GetProperty("cost")?.GetValue(eventData) as decimal?,
                                            ErrorMessage = eventType.GetProperty("error_message")?.GetValue(eventData)?.ToString(),
                                            FunctionExecutionId = eventType.GetProperty("function_execution_id")?.GetValue(eventData) as Guid?
                                        };
                                        functionExecutionResults.Add(result);
                                        totalFunctionCost += result.Cost ?? 0m;
                                    }
                                }
                            },
                            cancellationToken))
                        {
                            chunkCount++;
                            if (chunkCount == 1)
                            {
                                _logger.LogInformation("First chunk received at {Time}ms", (DateTime.UtcNow - firstChunkTime).TotalMilliseconds);
                            }
                            
                            // Accumulate content from chunks for potential usage estimation
                            if (chunk.Choices != null)
                            {
                                foreach (var choice in chunk.Choices)
                                {
                                    if (!string.IsNullOrEmpty(choice.Delta?.Content))
                                    {
                                        contentAccumulator.Append(choice.Delta.Content);
                                    }
                                }
                            }

                            // Accumulate tool calls from streaming chunks for request logging
                            if (chunk.Choices?.Count > 0 && chunk.Choices[0].Delta?.ToolCalls is { } toolCallDeltas)
                            {
                                foreach (var toolCallChunk in toolCallDeltas)
                                {
                                    if (!accumulatedToolCalls.ContainsKey(toolCallChunk.Index))
                                    {
                                        // New tool call
                                        accumulatedToolCalls[toolCallChunk.Index] = new ConduitLLM.Core.Models.ToolCall
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
                                        // Append to existing tool call (arguments come in chunks)
                                        var existing = accumulatedToolCalls[toolCallChunk.Index];
                                        if (!string.IsNullOrEmpty(toolCallChunk.Function?.Arguments))
                                        {
                                            if (existing.Function != null)
                                            {
                                                existing.Function.Arguments += toolCallChunk.Function.Arguments;
                                            }
                                        }
                                        if (!string.IsNullOrEmpty(toolCallChunk.Function?.Name) && existing.Function != null)
                                        {
                                            existing.Function.Name = toolCallChunk.Function.Name;
                                        }
                                    }
                                }
                            }

                            // Check for usage data in chunk (comes in final chunk for OpenAI-compatible APIs)
                            if (chunk.Usage != null)
                            {
                                streamingUsage = chunk.Usage;
                                streamingModel = chunk.Model ?? request.Model;
                                _logger.LogDebug("Captured streaming usage data: {Usage}", JsonSerializer.Serialize(streamingUsage));
                            }

                            // Route chunks to appropriate event types
                            // 1. Check for reasoning content (Conduit extension)
                            if (chunk.Choices?.Count > 0 && !string.IsNullOrEmpty(chunk.Choices[0].Delta?.Reasoning))
                            {
                                // Send reasoning as typed "event: reasoning"
                                // Null-forgiving operator is safe here because we checked IsNullOrEmpty above
                                await sseWriter.WriteReasoningEventAsync(chunk.Choices[0].Delta.Reasoning!);

                                // Also send the chunk as content (for full compatibility)
                                // Some clients may want the raw chunk with reasoning field
                                await sseWriter.WriteContentEventAsync(chunk);
                            }
                            // 2. Regular content chunk (OpenAI standard)
                            else
                            {
                                await sseWriter.WriteContentEventAsync(chunk);
                            }

                            // ⚠️ LEGACY FALLBACK TOKEN COUNTING (INACCURATE)
                            // =================================================
                            // This code attempts to count tokens by tracking chunks, but this is fundamentally flawed:
                            // - Each chunk typically contains MULTIPLE tokens, not one token
                            // - RecordToken() is called once per chunk, severely undercounting actual tokens
                            // - This causes wildly inaccurate tokensPerSecond metrics (e.g., 1.1 when actual is 20+)
                            //
                            // WHY THIS EXISTS:
                            // - Historically, providers didn't return usage data in streaming responses
                            // - This fallback was meant to provide "some" metrics when usage data was unavailable
                            //
                            // CURRENT STATUS:
                            // - Now using stream_options.include_usage=true to request usage from providers
                            // - Most OpenAI-compatible providers (OpenAI, Groq, SambaNova, Cerebras) support this
                            // - When usage data is available, GetFinalMetrics() uses actual token counts (line 240)
                            // - This fallback only runs when providers don't return usage data
                            //
                            // OPTIONS FOR IMPROVEMENT:
                            // 1. Remove this entirely and rely on provider usage data (requires all providers support it)
                            // 2. Implement proper tokenization using tiktoken or similar (adds dependency + latency)
                            // 3. Keep as-is but log warnings when usage data is missing and fallback is used
                            // 4. Use content length estimation (chars / 4 ≈ tokens) as a better fallback
                            //
                            // RECOMMENDATION: Option 3 - Keep for backward compatibility but warn when used
                            // =================================================
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

                                // Emit metrics periodically
                                if (metricsCollector.ShouldEmitMetrics())
                                {
                                    _logger.LogDebug("Emitting streaming metrics");
                                    await sseWriter.WriteMetricsEventAsync(metricsCollector.GetMetrics());
                                }
                            }
                        }
                        
                        // Store usage data for middleware to process
                        if (streamingUsage != null)
                        {
                            HttpContext.Items["StreamingUsage"] = streamingUsage;
                            HttpContext.Items["StreamingModel"] = streamingModel;
                            HttpContext.Items["UsageIsEstimated"] = false;
                        }
                        else if (_usageEstimationService != null && contentAccumulator.Length > 0)
                        {
                            // No usage data from provider, estimate it to prevent revenue loss
                            _logger.LogWarning("No usage data received from provider for streaming response, estimating usage for model {Model}", request.Model);
                            
                            try
                            {
                                var estimatedUsage = await _usageEstimationService.EstimateUsageFromStreamingResponseAsync(
                                    streamingModel ?? request.Model,
                                    request.Messages,
                                    contentAccumulator.ToString(),
                                    cancellationToken);
                                
                                HttpContext.Items["StreamingUsage"] = estimatedUsage;
                                HttpContext.Items["StreamingModel"] = streamingModel ?? request.Model;
                                HttpContext.Items["UsageIsEstimated"] = true;
                                
                                _logger.LogInformation(
                                    "Successfully estimated usage for streaming response: Prompt={PromptTokens}, Completion={CompletionTokens}, Total={TotalTokens}",
                                    estimatedUsage.PromptTokens, estimatedUsage.CompletionTokens, estimatedUsage.TotalTokens);
                            }
                            catch (Exception estEx)
                            {
                                _logger.LogError(estEx, "Failed to estimate usage for streaming response");
                                // Don't throw - we've already sent the response to the user
                                // The middleware will log this as a billing failure
                            }
                        }
                        else if (contentAccumulator.Length == 0)
                        {
                            _logger.LogWarning("No content accumulated from streaming response, cannot estimate usage");
                        }

                        // Store accumulated tool calls for request logging
                        if (accumulatedToolCalls.Count > 0)
                        {
                            HttpContext.Items["StreamingChatToolCalls"] = accumulatedToolCalls
                                .OrderBy(kv => kv.Key)
                                .Select(kv => kv.Value)
                                .ToList();
                            _logger.LogDebug("Stored {Count} accumulated tool calls for request logging", accumulatedToolCalls.Count);
                        }

                        // Store function execution results for request logging metadata
                        if (functionExecutionResults.Count > 0)
                        {
                            HttpContext.Items[HttpContextKeys.ChatFunctionCalls] = functionExecutionResults;
                            HttpContext.Items[HttpContextKeys.ChatFunctionCost] = totalFunctionCost;
                            _logger.LogDebug(
                                "Stored {Count} function execution results for request logging, total cost: {Cost:C}",
                                functionExecutionResults.Count, totalFunctionCost);
                        }

                        // Always write final metrics with token usage data
                        _logger.LogInformation("StreamingUsage before GetFinalMetrics: {Usage}", 
                            streamingUsage != null ? 
                            $"Prompt={streamingUsage.PromptTokens}, Completion={streamingUsage.CompletionTokens}, Total={streamingUsage.TotalTokens}" : 
                            "null");
                        
                        // Pass usage data to GetFinalMetrics which will include it in the metrics
                        var finalMetrics = metricsCollector.GetFinalMetrics(streamingUsage);
                        
                        _logger.LogInformation("FinalMetrics after GetFinalMetrics: PromptTokens={Prompt}, CompletionTokens={Completion}, TotalTokens={Total}",
                            finalMetrics.PromptTokens, finalMetrics.CompletionTokens, finalMetrics.TotalTokens);
                        
                        await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);

                        // Write [DONE] to signal the end of the stream
                        await sseWriter.WriteDoneEventAsync();
                        
                        _logger.LogInformation("Streaming completed: {ChunkCount} chunks over {Duration}ms", 
                            chunkCount, (DateTime.UtcNow - firstChunkTime).TotalMilliseconds);
                    }
                    catch (Exception streamEx)
                    {
                        _logger.LogError(streamEx, "Error in stream processing");
                        await sseWriter.WriteErrorEventAsync(streamEx.Message);
                    }

                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                });
            }
        }

        /// <summary>
        /// Validates function calling request parameters.
        /// </summary>
        /// <param name="request">The chat completion request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Error result if validation fails, null if validation succeeds</returns>
        private async Task<IActionResult?> ValidateFunctionCallRequestAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken)
        {
            // Check if function repository is available
            if (_functionConfigRepository == null)
            {
                _logger.LogError("Function calling requested but IFunctionConfigurationRepository is not available");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Function calling is not configured on this server",
                        Type = "server_error",
                        Code = "function_calling_unavailable"
                    }
                });
            }

            // Validate function configuration IDs exist and are enabled
            try
            {
                var functionConfigs = await _functionConfigRepository.GetByIdsAsync(
                    request.FunctionConfigurationIds!,
                    cancellationToken);

                // Check if all requested IDs were found
                var missingIds = request.FunctionConfigurationIds!
                    .Except(functionConfigs.Select(fc => fc.Id))
                    .ToList();

                if (missingIds.Count > 0)
                {
                    _logger.LogWarning("Function calling request includes non-existent function configuration IDs: {MissingIds}",
                        string.Join(", ", missingIds));
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Function configuration IDs not found: {string.Join(", ", missingIds)}",
                            Type = "invalid_request_error",
                            Code = "invalid_function_configuration_ids"
                        }
                    });
                }

                // Check if all function configurations are enabled
                var disabledConfigs = functionConfigs.Where(fc => !fc.IsEnabled).ToList();
                if (disabledConfigs.Count > 0)
                {
                    var disabledIds = string.Join(", ", disabledConfigs.Select(fc => fc.Id));
                    _logger.LogWarning("Function calling request includes disabled function configurations: {DisabledIds}", disabledIds);
                    return BadRequest(new OpenAIErrorResponse
                    {
                        Error = new OpenAIError
                        {
                            Message = $"Function configurations are disabled: {disabledIds}",
                            Type = "invalid_request_error",
                            Code = "disabled_function_configurations"
                        }
                    });
                }

                // Validate iteration limit if provided (using configurable limits from GlobalSettings)
                if (request.MaxAgenticIterations.HasValue)
                {
                    var minIterations = await _globalSettingsCacheService.GetMinAgenticIterationsAsync();
                    var maxIterations = await _globalSettingsCacheService.GetMaxAgenticIterationsAsync();

                    if (request.MaxAgenticIterations.Value < minIterations || request.MaxAgenticIterations.Value > maxIterations)
                    {
                        _logger.LogWarning("Invalid MaxAgenticIterations value: {Value}, valid range is {Min}-{Max}",
                            request.MaxAgenticIterations.Value, minIterations, maxIterations);
                        return BadRequest(new OpenAIErrorResponse
                        {
                            Error = new OpenAIError
                            {
                                Message = $"MaxAgenticIterations must be between {minIterations} and {maxIterations}",
                                Type = "invalid_request_error",
                                Code = "invalid_max_agentic_iterations"
                            }
                        });
                    }
                }

                _logger.LogDebug("Function calling validation passed for {Count} function configurations",
                    functionConfigs.Count);
                return null; // Validation passed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating function calling request");
                return StatusCode(500, new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = "Error validating function calling request",
                        Type = "server_error",
                        Code = "function_validation_error"
                    }
                });
            }
        }
    }

    /// <summary>
    /// Helper class to capture function execution results for request logging.
    /// This data is stored in HttpContext.Items during streaming and used by
    /// the UsageTrackingMiddleware to populate request log metadata.
    /// </summary>
    public class FunctionExecutionResultForLogging
    {
        /// <summary>
        /// The tool call ID from the LLM response
        /// </summary>
        public string? ToolCallId { get; set; }

        /// <summary>
        /// Name of the function that was executed
        /// </summary>
        public string? FunctionName { get; set; }

        /// <summary>
        /// Execution status: "completed" or "failed"
        /// </summary>
        public string? Status { get; set; }

        /// <summary>
        /// Cost of the function execution
        /// </summary>
        public decimal? Cost { get; set; }

        /// <summary>
        /// Error message if the function failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// ID of the FunctionExecution record for audit/drill-down
        /// </summary>
        public Guid? FunctionExecutionId { get; set; }
    }
}
