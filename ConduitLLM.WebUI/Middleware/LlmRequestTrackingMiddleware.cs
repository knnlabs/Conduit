using System.Diagnostics;
using System.Text;
using System.Text.Json;

using ConduitLLM.WebUI.Interfaces;
using ConduitLLM.WebUI.Services;

using Microsoft.Extensions.Logging;

namespace ConduitLLM.WebUI.Middleware;

/// <summary>
/// Middleware to track LLM requests, calculate token usage and update virtual key spending
/// </summary>
public class LlmRequestTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LlmRequestTrackingMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;
    private const string ApiPrefix = "/api/v1/";

    public LlmRequestTrackingMiddleware(
        RequestDelegate next,
        ILogger<LlmRequestTrackingMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IVirtualKeyService virtualKeyService)
    {
        // Only process API requests with a validated virtual key
        if (!context.Request.Path.StartsWithSegments(ApiPrefix) || 
            !context.Items.TryGetValue("ValidatedVirtualKeyId", out var virtualKeyIdObj) ||
            virtualKeyIdObj is not int virtualKeyId)
        {
            await _next(context);
            return;
        }

        string? keyName = context.Items.TryGetValue("ValidatedVirtualKeyName", out var keyNameObj) 
            ? keyNameObj?.ToString() 
            : null;

        string? requestedModel = null;
        string requestType = "Unknown";
        int inputTokenCount = 0;
        int outputTokenCount = 0;
        
        // Start the timer to measure response time
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Enable buffering so we can read the request and response multiple times
            context.Request.EnableBuffering();
            
            // Extract model from request body
            var originalRequestBody = await GetRequestBodyAsync(context.Request);
            context.Request.Body.Position = 0; // Reset for downstream middleware
            
            // Determine request type and extract model
            (requestType, requestedModel, inputTokenCount) = await ExtractRequestDetailsAsync(context.Request);
            
            // Create a wrapper for the response
            var originalBodyStream = context.Response.Body;
            using var responseBodyStream = new MemoryStream();
            context.Response.Body = responseBodyStream;
            
            // Continue down the pipeline
            await _next(context);
            
            // Read the response body
            responseBodyStream.Position = 0;
            var responseBody = await new StreamReader(responseBodyStream).ReadToEndAsync();
            responseBodyStream.Position = 0;
            
            // Extract token usage from response
            outputTokenCount = ExtractResponseTokenUsage(responseBody, requestType);
            
            // Copy the response to the original stream
            await responseBodyStream.CopyToAsync(originalBodyStream);
            
            // Calculate cost based on token usage 
            decimal cost = CalculateCost(requestedModel, inputTokenCount, outputTokenCount, requestType);
            
            // Update virtual key spend
            if (cost > 0)
            {
                await virtualKeyService.UpdateSpendAsync(virtualKeyId, cost);
            }
            
            // Stop the timer
            stopwatch.Stop();
            
            // Get a scoped instance of RequestLogService
            using (var scope = _serviceProvider.CreateScope())
            {
                var requestLogService = scope.ServiceProvider.GetRequiredService<IRequestLogService>();
                
                // Log the request
                await requestLogService.CreateRequestLogAsync(
                    virtualKeyId: virtualKeyId,
                    modelName: requestedModel ?? "unknown",
                    requestType: requestType,
                    inputTokens: inputTokenCount,
                    outputTokens: outputTokenCount,
                    cost: cost,
                    responseTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                    userId: null,
                    clientIp: context.Connection.RemoteIpAddress?.ToString(),
                    requestPath: context.Request.Path,
                    statusCode: context.Response.StatusCode
                );
            }
            
            _logger.LogInformation(
                "API request processed: Key={KeyName} (ID={KeyId}), Model={Model}, Type={RequestType}, " +
                "InputTokens={InputTokens}, OutputTokens={OutputTokens}, Cost=${Cost:F6}, Time={ResponseTime:F0}ms",
                keyName ?? "unknown", 
                virtualKeyId,
                requestedModel ?? "unknown", 
                requestType,
                inputTokenCount, 
                outputTokenCount, 
                cost, 
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request tracking for key ID {KeyId}", virtualKeyId);
            
            // Stop the timer if it's still running
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }
            
            // Log error request with minimal info
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var requestLogService = scope.ServiceProvider.GetRequiredService<IRequestLogService>();
                    
                    await requestLogService.CreateRequestLogAsync(
                        virtualKeyId: virtualKeyId,
                        modelName: requestedModel ?? "unknown",
                        requestType: requestType,
                        inputTokens: inputTokenCount,
                        outputTokens: 0,
                        cost: 0,
                        responseTimeMs: stopwatch.Elapsed.TotalMilliseconds,
                        userId: null,
                        clientIp: context.Connection.RemoteIpAddress?.ToString(),
                        requestPath: context.Request.Path,
                        statusCode: 500
                    );
                }
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log error request for key ID {KeyId}", virtualKeyId);
            }
            
            // Let the exception continue if we couldn't handle it
            throw;
        }
    }

    private async Task<string> GetRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private async Task<(string, string?, int)> ExtractRequestDetailsAsync(HttpRequest request)
    {
        // Extract model from request body
        var requestBody = await GetRequestBodyAsync(request);
        var jsonDoc = JsonDocument.Parse(requestBody);
        
        // Determine request type
        string requestType = "Unknown";
        if (jsonDoc.RootElement.TryGetProperty("type", out var typeElement))
        {
            requestType = typeElement.GetString() ?? "Unknown";
        }
        
        // Extract model
        string? requestedModel = null;
        if (jsonDoc.RootElement.TryGetProperty("model", out var modelElement))
        {
            requestedModel = modelElement.GetString();
        }
        
        // Extract input token count
        int inputTokenCount = 0;
        if (jsonDoc.RootElement.TryGetProperty("input_tokens", out var inputTokensElement))
        {
            inputTokenCount = inputTokensElement.GetInt32();
        }
        
        return (requestType, requestedModel, inputTokenCount);
    }

    private int ExtractResponseTokenUsage(string responseBody, string requestType)
    {
        // Parse JSON response to extract usage information
        var jsonDoc = JsonDocument.Parse(responseBody);
        
        // Extract token usage
        int outputTokenCount = 0;
        if (jsonDoc.RootElement.TryGetProperty("usage", out var usageElement))
        {
            if (usageElement.TryGetProperty("output_tokens", out var outputTokensElement))
            {
                outputTokenCount = outputTokensElement.GetInt32();
            }
        }
        
        return outputTokenCount;
    }

    private decimal CalculateCost(string? model, int inputTokens, int outputTokens, string requestType)
    {
        // These rates are examples and should be adjusted based on the actual LLM provider pricing
        decimal inputRate = 0.0000001M; // $0.0000001 per input token
        decimal outputRate = 0.0000002M; // $0.0000002 per output token
        
        // Apply different rates based on model
        if (!string.IsNullOrEmpty(model))
        {
            // Examples for common models - adjust based on actual pricing
            if (model.Contains("gpt-4-turbo", StringComparison.OrdinalIgnoreCase))
            {
                inputRate = 0.00001M;
                outputRate = 0.00003M;
            }
            else if (model.Contains("gpt-4", StringComparison.OrdinalIgnoreCase))
            {
                inputRate = 0.00003M;
                outputRate = 0.00006M;
            }
            else if (model.Contains("gpt-3.5-turbo", StringComparison.OrdinalIgnoreCase))
            {
                inputRate = 0.000001M;
                outputRate = 0.000002M;
            }
            else if (model.Contains("claude", StringComparison.OrdinalIgnoreCase))
            {
                inputRate = 0.000008M;
                outputRate = 0.000024M;
            }
        }
        
        return (inputTokens * inputRate) + (outputTokens * outputRate);
    }
}

// Extension method for easy registration in Program.cs
public static class LlmRequestTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UseLlmRequestTracking(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<LlmRequestTrackingMiddleware>();
    }
}
