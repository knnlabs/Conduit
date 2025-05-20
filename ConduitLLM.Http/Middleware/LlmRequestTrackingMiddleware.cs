using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ConduitLLM.Configuration.DTOs;
using ConduitLLM.Configuration.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ConduitLLM.Http.Middleware
{
    /// <summary>
    /// Middleware for tracking LLM request details
    /// </summary>
    public class LlmRequestTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IRequestLogService _requestLogService;
        private readonly ILogger<LlmRequestTrackingMiddleware>? _logger;
        
        /// <summary>
        /// Initializes a new instance of the LlmRequestTrackingMiddleware
        /// </summary>
        /// <param name="next">The next middleware in the pipeline</param>
        /// <param name="requestLogService">Service for logging requests</param>
        /// <param name="logger">Optional logger</param>
        public LlmRequestTrackingMiddleware(
            RequestDelegate next,
            IRequestLogService requestLogService,
            ILogger<LlmRequestTrackingMiddleware>? logger = null)
        {
            _next = next;
            _requestLogService = requestLogService;
            _logger = logger;
        }
        
        /// <summary>
        /// Processes the request
        /// </summary>
        /// <param name="context">The HTTP context</param>
        public async Task InvokeAsync(HttpContext context)
        {
            // Check if this is a request that we want to track
            if (ShouldTrackRequest(context))
            {
                await TrackRequest(context);
            }
            else
            {
                // Just pass through to the next middleware
                await _next(context);
            }
        }
        
        /// <summary>
        /// Determines if the request should be tracked
        /// </summary>
        private bool ShouldTrackRequest(HttpContext context)
        {
            // Check for virtual key header
            var hasVirtualKey = context.Request.Headers.TryGetValue("X-Virtual-Key", out var _);
            
            // Check if this is an API endpoint
            var isApiRequest = context.Request.Path.StartsWithSegments("/api");
            
            // We could add more criteria here
            
            return hasVirtualKey && isApiRequest;
        }
        
        /// <summary>
        /// Tracks the request
        /// </summary>
        private async Task TrackRequest(HttpContext context)
        {
            // Get the virtual key from the header
            var keyValue = context.Request.Headers["X-Virtual-Key"].ToString();
            
            // Get the virtual key ID
            var virtualKeyId = await _requestLogService.GetVirtualKeyIdFromKeyValueAsync(keyValue);
            if (virtualKeyId == null)
            {
                _logger?.LogWarning("Request with invalid virtual key: {KeyValue}", keyValue);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid virtual key");
                return;
            }
            
            // Enable request body reading
            context.Request.EnableBuffering();
            
            // Save the original response body stream
            var originalResponseBody = context.Response.Body;
            
            try
            {
                // Read the request body
                var requestBody = await ReadBodyAsync(context.Request.Body);
                
                // Reset the request body position
                context.Request.Body.Position = 0;
                
                // Create a new memory stream for the response body
                using var responseBody = new MemoryStream();
                context.Response.Body = responseBody;
                
                // Start the timer
                var stopwatch = Stopwatch.StartNew();
                
                // Call the next middleware in the pipeline
                await _next(context);
                
                // Stop the timer
                stopwatch.Stop();
                
                // Read the response body
                responseBody.Position = 0;
                var responseContent = await ReadBodyAsync(responseBody);
                
                // Estimate tokens
                var (inputTokens, outputTokens) = _requestLogService.EstimateTokens(requestBody, responseContent);
                
                // Extract model name from request
                var modelName = ExtractModelName(requestBody);
                
                // Determine request type
                var requestType = DetermineRequestType(context.Request.Path);
                
                // Calculate cost
                var cost = _requestLogService.CalculateCost(modelName, inputTokens, outputTokens);
                
                // Log the request
                var log = new LogRequestDto
                {
                    VirtualKeyId = virtualKeyId.Value,
                    ModelName = modelName,
                    RequestType = requestType,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost,
                    ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds,
                    UserId = context.User?.Identity?.Name,
                    ClientIp = context.Connection.RemoteIpAddress?.ToString(),
                    RequestPath = context.Request.Path,
                    StatusCode = context.Response.StatusCode
                };
                
                await _requestLogService.LogRequestAsync(log);
                
                // Copy the response to the original stream
                responseBody.Position = 0;
                await responseBody.CopyToAsync(originalResponseBody);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error tracking request");
                throw;
            }
            finally
            {
                // Always restore the original response body
                context.Response.Body = originalResponseBody;
            }
        }
        
        /// <summary>
        /// Reads a stream into a string
        /// </summary>
        private static async Task<string> ReadBodyAsync(Stream stream)
        {
            if (stream.CanRead && stream.Length > 0)
            {
                using var reader = new StreamReader(
                    stream,
                    encoding: Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    leaveOpen: true);
                    
                return await reader.ReadToEndAsync();
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// Extracts the model name from the request body
        /// </summary>
        private static string ExtractModelName(string requestBody)
        {
            // This is a simplified implementation - in a real system,
            // you'd use proper JSON parsing
            
            // Look for "model":"..."
            const string modelPattern = "\"model\":";
            var modelIndex = requestBody.IndexOf(modelPattern);
            
            if (modelIndex >= 0)
            {
                var startQuoteIndex = requestBody.IndexOf('"', modelIndex + modelPattern.Length);
                if (startQuoteIndex >= 0)
                {
                    var endQuoteIndex = requestBody.IndexOf('"', startQuoteIndex + 1);
                    if (endQuoteIndex >= 0)
                    {
                        return requestBody.Substring(startQuoteIndex + 1, endQuoteIndex - startQuoteIndex - 1);
                    }
                }
            }
            
            return "unknown";
        }
        
        /// <summary>
        /// Determines the type of request from the path
        /// </summary>
        private static string DetermineRequestType(string path)
        {
            var normalizedPath = path.ToLowerInvariant();
            
            if (normalizedPath.Contains("/chat"))
            {
                return "chat";
            }
            
            if (normalizedPath.Contains("/completion"))
            {
                return "completion";
            }
            
            if (normalizedPath.Contains("/embedding"))
            {
                return "embedding";
            }
            
            return "other";
        }
    }
}
