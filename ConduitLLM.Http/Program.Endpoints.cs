using System.Text.Json;
using System.Text.Json.Serialization;
using ConduitLLM.Configuration;
using ConduitLLM.Configuration.Extensions;
using ConduitLLM.Core;
using ConduitLLM.Core.Extensions;
using ConduitLLM.Core.Interfaces;
using ConduitLLM.Core.Models;
using ConduitLLM.Core.Routing;
using ConduitLLM.Core.Services;
using ConduitLLM.Http.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public partial class Program
{
    public static void ConfigureEndpoints(WebApplication app)
    {
        // Get JsonSerializerOptions from DI
        var jsonSerializerOptions = app.Services.GetRequiredService<JsonSerializerOptions>();

        // Map SignalR hubs for real-time updates

        // Customer-facing hubs require virtual key authentication
        app.MapHub<ConduitLLM.Http.Hubs.VideoGenerationHub>("/hubs/video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR VideoGenerationHub registered at /hubs/video-generation (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.ImageGenerationHub>("/hubs/image-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR ImageGenerationHub registered at /hubs/image-generation (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.TaskHub>("/hubs/tasks")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR TaskHub registered at /hubs/tasks (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.SystemNotificationHub>("/hubs/notifications")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR SystemNotificationHub registered at /hubs/notifications (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.SpendNotificationHub>("/hubs/spend")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR SpendNotificationHub registered at /hubs/spend (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.WebhookDeliveryHub>("/hubs/webhooks")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR WebhookDeliveryHub registered at /hubs/webhooks (requires authentication)");

        app.MapHub<ConduitLLM.Http.Hubs.ModelDiscoveryHub>("/hubs/model-discovery")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR ModelDiscoveryHub registered at /hubs/model-discovery (requires authentication)");

        // Admin-only hub for metrics dashboard
        app.MapHub<ConduitLLM.Http.Hubs.MetricsHub>("/hubs/metrics")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR MetricsHub registered at /hubs/metrics (requires admin authentication)");

        // Admin-only hub for health monitoring
        app.MapHub<ConduitLLM.Http.Hubs.HealthMonitoringHub>("/hubs/health-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR HealthMonitoringHub registered at /hubs/health-monitoring (requires admin authentication)");

        // Admin-only hub for security monitoring
        app.MapHub<ConduitLLM.Http.Hubs.SecurityMonitoringHub>("/hubs/security-monitoring")
            .RequireAuthorization("AdminOnly");
        Console.WriteLine("[Conduit API] SignalR SecurityMonitoringHub registered at /hubs/security-monitoring (requires admin authentication)");

        // Virtual key management hub for real-time key management updates
        app.MapHub<ConduitLLM.Http.Hubs.VirtualKeyManagementHub>("/hubs/virtual-key-management")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR VirtualKeyManagementHub registered at /hubs/virtual-key-management (requires authentication)");

        // Usage analytics hub for real-time analytics and monitoring
        app.MapHub<ConduitLLM.Http.Hubs.UsageAnalyticsHub>("/hubs/usage-analytics")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR UsageAnalyticsHub registered at /hubs/usage-analytics (requires authentication)");

        // Enhanced video generation hub with acknowledgment support
        app.MapHub<ConduitLLM.Http.SignalR.Hubs.EnhancedVideoGenerationHub>("/hubs/enhanced-video-generation")
            .RequireAuthorization();
        Console.WriteLine("[Conduit API] SignalR EnhancedVideoGenerationHub registered at /hubs/enhanced-video-generation (requires authentication)");

        // Map health check endpoints without authentication requirement
        // Health endpoints should be accessible without authentication for monitoring tools
        app.MapSecureConduitHealthChecks(requireAuthorization: false);

        // Map Prometheus metrics endpoint for scraping
        app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");
        Console.WriteLine("[Conduit API] Prometheus metrics endpoint registered at /metrics");

        Console.WriteLine("[Conduit API] Starting to map API endpoints...");

        // Add completions endpoint (legacy)
        app.MapPost("/v1/completions", ([FromServices] ILogger<Program> logger) =>
        {
            logger.LogInformation("Legacy /completions endpoint called.");
            return Results.Json(
                new
                {
                    error = "The /completions endpoint is not implemented. Please use /chat/completions."
                },
                statusCode: 501,
                options: jsonSerializerOptions
            );
        });

        // Add embeddings endpoint
        app.MapPost("/v1/embeddings", async (
            [FromBody] EmbeddingRequest? request,
            [FromServices] ILLMRouter router,
            [FromServices] ILogger<Program> logger,
            [FromServices] ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService modelMappingService,
            HttpRequest httpRequest,
            CancellationToken cancellationToken) =>
        {
            if (request == null)
            {
                return Results.BadRequest(new { error = "Invalid request body." });
            }

            try
            {
                logger.LogInformation("Processing embeddings request for model: {Model}", request.Model);
                
                // Get provider info for usage tracking
                try
                {
                    var modelMapping = await modelMappingService.GetMappingByModelAliasAsync(request.Model);
                    if (modelMapping != null)
                    {
                        httpRequest.HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                        httpRequest.HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
                }
                
                var response = await router.CreateEmbeddingAsync(request, cancellationToken: cancellationToken);
                return Results.Json(response, options: jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing embeddings request for model: {Model}", request.Model);
                return Results.Json(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                }, statusCode: 500, options: jsonSerializerOptions);
            }
        });

        // Add models endpoint
        app.MapGet("/v1/models", ([FromServices] ILLMRouter router, [FromServices] ILogger<Program> logger) =>
        {
            try
            {
                logger.LogInformation("Getting available models");

                // Get model names from the router
                var modelNames = router.GetAvailableModels();

                // Convert to OpenAI format
                var basicModelData = modelNames.Select(m => new
                {
                    id = m,
                    @object = "model"
                }).ToList();

                // Create the response envelope
                var response = new
                {
                    data = basicModelData,
                    @object = "list"
                };

                return Results.Json(response, options: jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving models list");
                return Results.Json(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                }, statusCode: 500, options: jsonSerializerOptions);
            }
        });

        // Add chat completions endpoint
        app.MapPost("/v1/chat/completions", async (
            [FromBody] ChatCompletionRequest request,
            [FromServices] Conduit conduit,
            [FromServices] ILogger<Program> logger,
            [FromServices] ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService modelMappingService,
            HttpRequest httpRequest) =>
        {
            logger.LogInformation("Received /v1/chat/completions request for model: {Model}", request.Model);

            // Store streaming flag for middleware
            httpRequest.HttpContext.Items["IsStreamingRequest"] = request.Stream == true;
            
            // Get provider info for usage tracking
            try
            {
                var modelMapping = await modelMappingService.GetMappingByModelAliasAsync(request.Model);
                if (modelMapping != null)
                {
                    httpRequest.HttpContext.Items["ProviderId"] = modelMapping.ProviderId;
                    httpRequest.HttpContext.Items["ProviderType"] = modelMapping.Provider?.ProviderType;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get provider info for model {Model}", request.Model);
            }

            try
            {
                // Non-streaming path
                if (request.Stream != true)
                {
                    logger.LogInformation("Handling non-streaming request.");
                    var response = await conduit.CreateChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted);
                    return Results.Json(response, options: jsonSerializerOptions);
                }
                else
                {
                    logger.LogInformation("Handling streaming request.");
                    
                    // Use enhanced SSE writer for performance metrics support
                    var response = httpRequest.HttpContext.Response;
                    var sseWriter = response.CreateEnhancedSSEWriter(jsonSerializerOptions);
                    
                    // Create metrics collector if performance tracking is enabled
                    var settings = httpRequest.HttpContext.RequestServices.GetRequiredService<IOptions<ConduitSettings>>().Value;
                    StreamingMetricsCollector? metricsCollector = null;
                    
                    if (settings.PerformanceTracking?.Enabled == true && settings.PerformanceTracking.TrackStreamingMetrics)
                    {
                        logger.LogInformation("Performance tracking enabled for streaming request");
                        var requestId = Guid.NewGuid().ToString();
                        response.Headers["X-Request-ID"] = requestId;
                        
                        // Get provider info for metrics from model mapping service
                        var mappingService = httpRequest.HttpContext.RequestServices.GetRequiredService<ConduitLLM.Core.Interfaces.Configuration.IModelProviderMappingService>();
                        var modelMapping = await mappingService.GetMappingByModelAliasAsync(request.Model);
                        // Use provider ID for metrics since it's the stable identifier
                        var providerId = modelMapping?.ProviderId.ToString() ?? "unknown";
                        
                        logger.LogInformation("Creating StreamingMetricsCollector for model {Model}, provider {Provider}", request.Model, providerId);
                        metricsCollector = new StreamingMetricsCollector(
                            requestId,
                            request.Model,
                            providerId);
                    }
                    else
                    {
                        logger.LogInformation("Performance tracking disabled for streaming request. Enabled: {Enabled}, TrackStreaming: {TrackStreaming}", 
                            settings.PerformanceTracking?.Enabled, 
                            settings.PerformanceTracking?.TrackStreamingMetrics);
                    }

                    try
                    {
                        ConduitLLM.Core.Models.Usage? streamingUsage = null;
                        string? streamingModel = null;
                        
                        await foreach (var chunk in conduit.StreamChatCompletionAsync(request, null, httpRequest.HttpContext.RequestAborted))
                        {
                            // Check for usage data in chunk (comes in final chunk for OpenAI-compatible APIs)
                            if (chunk.Usage != null)
                            {
                                streamingUsage = chunk.Usage;
                                streamingModel = chunk.Model ?? request.Model;
                                logger.LogDebug("Captured streaming usage data: {Usage}", JsonSerializer.Serialize(streamingUsage));
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
                                    logger.LogDebug("Emitting streaming metrics");
                                    await sseWriter.WriteMetricsEventAsync(metricsCollector.GetMetrics());
                                }
                            }
                        }
                        
                        // Store usage data for middleware to process
                        if (streamingUsage != null)
                        {
                            httpRequest.HttpContext.Items["StreamingUsage"] = streamingUsage;
                            httpRequest.HttpContext.Items["StreamingModel"] = streamingModel;
                        }

                        // Write final metrics if tracking is enabled
                        if (metricsCollector != null)
                        {
                            var finalMetrics = metricsCollector.GetFinalMetrics();
                            await sseWriter.WriteFinalMetricsEventAsync(finalMetrics);
                        }

                        // Write [DONE] to signal the end of the stream
                        await sseWriter.WriteDoneEventAsync();
                    }
                    catch (Exception streamEx)
                    {
                        logger.LogError(streamEx, "Error in stream processing");
                        await sseWriter.WriteErrorEventAsync(streamEx.Message);
                    }

                    return Results.Empty;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing request");
                return Results.Json(new OpenAIErrorResponse
                {
                    Error = new OpenAIError
                    {
                        Message = ex.Message,
                        Type = "server_error",
                        Code = "internal_error"
                    }
                }, statusCode: 500, options: jsonSerializerOptions);
            }
        });
    }
}

// Helper class for OpenAI-compatible error response
public class OpenAIErrorResponse
{
    [JsonPropertyName("error")]
    public required OpenAIError Error { get; set; }
}

public class OpenAIError
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("param")]
    public string? Param { get; set; }
    [JsonPropertyName("code")]
    public string? Code { get; set; }
}