using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ConduitLLM.Configuration;
using ConduitLLM.Core;
using ConduitLLM.Core.Configuration;
using ConduitLLM.Core.Controllers;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Adapters;
using ConduitLLM.Http.Models;
using ConduitLLM.Http.Services;

using MassTransit;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ConduitLLM.Http.Authorization;

namespace ConduitLLM.Http.Controllers
{
    /// <summary>
    /// Handles chat completion requests following OpenAI's API format.
    /// </summary>
    [ApiController]
    [Route("v1/chat")]
    [Authorize(AuthenticationSchemes = "VirtualKey,EphemeralKey")]
    [RequireBalance]
    [Tags("Chat")]
    public class ChatController : EventPublishingControllerBase
    {
        private readonly Conduit _conduit;
        private readonly ILogger<ChatController> _logger;
        private readonly ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService _modelMappingService;
        private readonly IOptions<ConduitSettings> _settings;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public ChatController(
            Conduit conduit,
            ILogger<ChatController> logger,
            ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService modelMappingService,
            IOptions<ConduitSettings> settings,
            JsonSerializerOptions jsonSerializerOptions,
            IPublishEndpoint publishEndpoint) : base(publishEndpoint, logger)
        {
            _conduit = conduit ?? throw new ArgumentNullException(nameof(conduit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelMappingService = modelMappingService ?? throw new ArgumentNullException(nameof(modelMappingService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _jsonSerializerOptions = jsonSerializerOptions ?? throw new ArgumentNullException(nameof(jsonSerializerOptions));
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

            try
            {
                // Non-streaming path
                if (request.Stream != true)
                {
                    _logger.LogInformation("Handling non-streaming request.");
                    var response = await _conduit.CreateChatCompletionAsync(request, null, cancellationToken);
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
                    
                    // Create metrics collector if performance tracking is enabled
                    StreamingMetricsCollector? metricsCollector = null;
                    
                    if (_settings.Value.PerformanceTracking?.Enabled == true && _settings.Value.PerformanceTracking.TrackStreamingMetrics)
                    {
                        _logger.LogInformation("Performance tracking enabled for streaming request");
                        var requestId = Guid.NewGuid().ToString();
                        response.Headers["X-Request-ID"] = requestId;
                        
                        // Get provider info for metrics from model mapping service
                        var modelMapping = await _modelMappingService.GetMappingByModelAliasAsync(request.Model);
                        // Use provider ID for metrics since it's the stable identifier
                        var providerId = modelMapping?.ProviderId.ToString() ?? "unknown";
                        
                        _logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", request.Model, providerId);
                        metricsCollector = new StreamingMetricsCollector(
                            requestId,
                            request.Model,
                            providerId);
                    }
                    else
                    {
                        _logger.LogInformation("Performance tracking disabled for streaming request. Enabled: {Enabled}, TrackStreaming: {TrackStreaming}", 
                            _settings.Value.PerformanceTracking?.Enabled, 
                            _settings.Value.PerformanceTracking?.TrackStreamingMetrics);
                    }

                    try
                    {
                        ConduitLLM.Core.Models.Usage? streamingUsage = null;
                        string? streamingModel = null;
                        
                        var chunkCount = 0;
                        var firstChunkTime = DateTime.UtcNow;
                        
                        await foreach (var chunk in _conduit.StreamChatCompletionAsync(request, null, cancellationToken))
                        {
                            chunkCount++;
                            if (chunkCount == 1)
                            {
                                _logger.LogInformation("First chunk received at {Time}ms", (DateTime.UtcNow - firstChunkTime).TotalMilliseconds);
                            }
                            
                            // Check for usage data in chunk (comes in final chunk for OpenAI-compatible APIs)
                            if (chunk.Usage != null)
                            {
                                streamingUsage = chunk.Usage;
                                streamingModel = chunk.Model ?? request.Model;
                                _logger.LogDebug("Captured streaming usage data: {Usage}", JsonSerializer.Serialize(streamingUsage));
                            }
                            
                            // Write content event
                            await sseWriter.WriteContentEventAsync(chunk);
                            
                            // Track metrics if enabled
                            if (metricsCollector != null && chunk?.Choices?.Count > 0)
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
                        }

                        // Write final metrics if tracking is enabled
                        if (metricsCollector != null)
                        {
                            var finalMetrics = metricsCollector.GetFinalMetrics();
                            await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);
                        }

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
    }
}