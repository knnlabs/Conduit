using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Polly.CircuitBreaker;

namespace ConduitLLM.WebUI.Middleware
{
    /// <summary>
    /// Middleware that provides enhanced logging and monitoring for resilience events.
    /// </summary>
    public class ResilienceLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ResilienceLoggingMiddleware> _logger;

        public ResilienceLoggingMiddleware(
            RequestDelegate next,
            ILogger<ResilienceLoggingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // Add request ID to logging scope
            using (_logger.BeginScope("RequestId: {RequestId}", requestId))
            {
                try
                {
                    await _next(context);

                    stopwatch.Stop();

                    // Log successful requests that took longer than expected
                    if (stopwatch.ElapsedMilliseconds > 5000)
                    {
                        _logger.LogWarning(
                            "Slow request detected. Path: {Path}, Method: {Method}, Duration: {Duration}ms",
                            context.Request.Path,
                            context.Request.Method,
                            stopwatch.ElapsedMilliseconds);
                    }
                }
                catch (BrokenCircuitException ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex,
                        "Circuit breaker open. Path: {Path}, Method: {Method}, Duration: {Duration}ms",
                        context.Request.Path,
                        context.Request.Method,
                        stopwatch.ElapsedMilliseconds);

                    await HandleCircuitBreakerError(context);
                }
                catch (TaskCanceledException ex) when (stopwatch.ElapsedMilliseconds > 30000)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex,
                        "Request timeout. Path: {Path}, Method: {Method}, Duration: {Duration}ms",
                        context.Request.Path,
                        context.Request.Method,
                        stopwatch.ElapsedMilliseconds);

                    await HandleTimeoutError(context);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogError(ex,
                        "Unhandled exception. Path: {Path}, Method: {Method}, Duration: {Duration}ms",
                        context.Request.Path,
                        context.Request.Method,
                        stopwatch.ElapsedMilliseconds);

                    throw; // Re-throw to let other middleware handle it
                }
            }
        }

        private async Task HandleCircuitBreakerError(HttpContext context)
        {
            context.Response.StatusCode = 503; // Service Unavailable
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(@"{
                ""error"": ""Service temporarily unavailable"",
                ""message"": ""The Admin API is currently experiencing issues. Please try again in a few moments."",
                ""code"": ""CIRCUIT_BREAKER_OPEN""
            }");
        }

        private async Task HandleTimeoutError(HttpContext context)
        {
            context.Response.StatusCode = 504; // Gateway Timeout
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(@"{
                ""error"": ""Request timeout"",
                ""message"": ""The operation took too long to complete. Please try again."",
                ""code"": ""REQUEST_TIMEOUT""
            }");
        }
    }
}
